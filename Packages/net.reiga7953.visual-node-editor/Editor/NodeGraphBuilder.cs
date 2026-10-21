using System;
using System.Linq;
using Reiga.VisualNodeEditor.Editor.Views;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Editor
{
    /// <summary>
    /// コードからグラフを組み立てる（自前の Starter・テスト・自動の組み立て用。ゲームの Starter もこれを使う）。
    /// ノード・コンテナ（出口と中の Entry 付き）・Event・エッジ・振る舞いを、親のコンテナ・位置・ポート名をまとめて指定して足していく。
    /// <list type="bullet">
    /// <item>繋いだ順が、同じノードからの遷移を調べる順になる（最初に当てはまるものに進む）。条件の無い Event は、同じ名前の条件付きのものより後に繋ぐ</item>
    /// <item>位置は呼び出し側が決める（自動では並べない）。検証はしないので、組み立てたら <see cref="GraphValidator.Validate(NodeGraphAsset)"/> で確かめる</item>
    /// <item>Undo の記録・<c>EditorUtility.SetDirty</c>・アセットの保存はしない（新しいグラフは <c>AssetDatabase.CreateAsset</c> で保存する）</item>
    /// </list>
    /// </summary>
    /// <example>
    /// <code>
    /// var builder = new NodeGraphBuilder();
    /// var affection = builder.AddParameter("Affection", GraphParameterType.Int);
    /// var story = builder.Add(new StateNode { Title = "Story" }, 250f, 0f);
    /// var romance = builder.Add(new StateNode { Title = "Romance" }, 750f, -100f);
    /// var normal = builder.Add(new StateNode { Title = "Normal" }, 750f, 100f);
    /// builder.Connect(builder.Entry, story);
    /// var finishLoved = builder.AddEvent("Finish", 500f, -100f);
    /// finishLoved.AddCondition(new TransitionCondition(affection.Id, ConditionOperator.GreaterOrEqual) { IntValue = 60 });
    /// builder.Connect(story, finishLoved, romance);
    /// builder.Connect(story, builder.AddEvent("Finish", 500f, 100f), normal); // 条件の無いものは最後に
    /// AssetDatabase.CreateAsset(builder.Graph, "Assets/Story.asset");
    /// </code>
    /// </example>
    public sealed class NodeGraphBuilder
    {
        private const string In = NodeView.InputPortName;
        private const string Out = NodeView.OutputPortName;
        private const string NoteHasNoPorts = "A Note has no ports: it only holds text and is not part of the flow.";

        /// <summary>新しいグラフ（<see cref="NodeGraphFactory.CreateNew"/>。Entry とセーブキー入り。まだアセットとして保存していない）を組み立てる。</summary>
        public NodeGraphBuilder()
            : this(NodeGraphFactory.CreateNew())
        {
        }

        /// <summary>既存のグラフ <paramref name="graph"/> に足していく（Undo の記録と SetDirty は呼び出し側が行う）。</summary>
        public NodeGraphBuilder(NodeGraphAsset graph)
        {
            Graph = graph != null ? graph : throw new ArgumentNullException(nameof(graph));
            Entry = graph.Nodes.OfType<EntryNode>().FirstOrDefault(node => node.IsAtRoot);
        }

        /// <summary>組み立てているグラフ。</summary>
        public NodeGraphAsset Graph { get; }

        /// <summary>ルートの Entry。既存のグラフに無ければ null。</summary>
        public EntryNode Entry { get; }

        /// <summary>Blackboard にパラメータを足す。</summary>
        public GraphParameter AddParameter(GraphParameter parameter)
        {
            Graph.AddParameter(parameter ?? throw new ArgumentNullException(nameof(parameter)));
            return parameter;
        }

        /// <summary>Blackboard に、既定値（false・0・空文字）のパラメータを足す。既定値は返したパラメータの IntValue などで変える。</summary>
        public GraphParameter AddParameter(string name, GraphParameterType type) => AddParameter(new GraphParameter(name, type));

        /// <summary><paramref name="parent"/>（null ならルート）の <paramref name="x"/>, <paramref name="y"/> にノードを置く。</summary>
        public T Add<T>(T node, float x, float y, ContainerNode parent = null) where T : NodeData
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            node.ParentId = parent != null ? parent.Id : string.Empty;
            node.Position = new Vector2(x, y);
            Graph.AddNode(node);
            return node;
        }

        /// <summary>
        /// 出口が <paramref name="exits"/>（出力ポートの順）のコンテナと、その中の Entry（左端）を、<paramref name="parent"/>（null ならルート）に置く。
        /// 中の Exit ノードは <see cref="AddExit"/> で置く。
        /// </summary>
        public ContainerNode AddContainer(string title, float x, float y, ContainerNode parent, params string[] exits)
        {
            var container = Add(new ContainerNode { Title = title }, x, y, parent);
            foreach (var exit in container.Exits.ToList())
            {
                container.RemoveExit(exit.Id);
            }

            foreach (var exit in exits ?? Array.Empty<string>())
            {
                container.AddExit(exit);
            }

            Add(new ContainerEntryNode(), 0f, 0f, container);
            return container;
        }

        /// <summary>コンテナの中の Entry。無ければ null。</summary>
        public ContainerEntryNode EntryOf(ContainerNode container)
        {
            if (container == null)
            {
                throw new ArgumentNullException(nameof(container));
            }

            return Graph.Nodes.OfType<ContainerEntryNode>().FirstOrDefault(node => node.ParentId == container.Id);
        }

        /// <summary>コンテナの中に、その出口 <paramref name="exitName"/> から出る Exit ノードを置く。</summary>
        /// <exception cref="ArgumentException">その名前の出口が無い。</exception>
        public ContainerExitNode AddExit(ContainerNode container, string exitName, float x, float y) =>
            Add(new ContainerExitNode { ExitId = FindExit(container, exitName).Id }, x, y, container);

        /// <summary>イベント名 <paramref name="eventName"/> の Event（<paramref name="automatic"/> なら自動の Event。名前は空でよい）を、<paramref name="parent"/>（null ならルート）に置く。</summary>
        public EventNode AddEvent(string eventName, float x, float y, ContainerNode parent = null, bool automatic = false) =>
            Add(new EventNode
            {
                EventName = eventName ?? string.Empty,
                Trigger = automatic ? EventTriggerMode.Automatic : EventTriggerMode.Raise,
            }, x, y, parent);

        /// <summary><paramref name="from"/> の out から <paramref name="to"/> の in へ繋ぐ（そのノードから出ているエッジの最後に足す）。</summary>
        /// <exception cref="ArgumentException">
        /// <paramref name="from"/> に out が無い（コンテナは出口から出るので <see cref="ConnectExit"/> を使う。Exit ノードは出口へ進むだけ。Note はポートを持たない）、
        /// または <paramref name="to"/> に in が無い（Entry・Note）。
        /// </exception>
        public void Connect(NodeData from, NodeData to)
        {
            CheckOutput(from, nameof(from));
            CheckInput(to, nameof(to));
            Graph.AddEdge(new EdgeData(from.Id, Out, to.Id, In));
        }

        /// <summary>
        /// <paramref name="from"/> → <paramref name="via"/>（Event）→ <paramref name="to"/>。
        /// どちらかのエッジが繋げなければ、どちらも足さずに例外にする（片方だけ繋がったグラフを残さない）。
        /// </summary>
        public void Connect(NodeData from, EventNode via, NodeData to)
        {
            CheckOutput(from, nameof(from));
            CheckInput(via, nameof(via));
            CheckInput(to, nameof(to));
            Connect(from, via);
            Connect(via, to);
        }

        /// <summary>コンテナの出口 <paramref name="exitName"/> から <paramref name="to"/> へ繋ぐ。</summary>
        /// <exception cref="ArgumentException">その名前の出口が無い、または <paramref name="to"/> に in が無い。</exception>
        public void ConnectExit(ContainerNode container, string exitName, NodeData to)
        {
            var exit = FindExit(container, exitName);
            CheckInput(to, nameof(to));
            Graph.AddEdge(new EdgeData(container.Id, exit.Id, to.Id, In));
        }

        /// <summary><paramref name="node"/> に入ったとき、<paramref name="changes"/> を行う Set Parameter の振る舞いを付ける。</summary>
        public void SetOnEnter(IBehaviourHost node, params ParameterChange[] changes)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            var behaviour = new SetParameterBehaviour();
            behaviour.Changes.AddRange(changes ?? Array.Empty<ParameterChange>());
            node.AddBehaviour(behaviour);
        }

        // 繋ぐ元に out があるか（Runner が辿らないエッジを作らない）
        private static void CheckOutput(NodeData from, string paramName)
        {
            switch (from)
            {
                case null:
                    throw new ArgumentNullException(paramName);
                case ContainerNode container:
                    throw new ArgumentException(
                        $"Container '{container.Title}' leads out through its exits. Use ConnectExit(container, exitName, to).", paramName);
                case ContainerExitNode _:
                    throw new ArgumentException("An Exit node has no output: the flow leaves the container through the exit it stands for.", paramName);
                case NoteNode _:
                    throw new ArgumentException(NoteHasNoPorts, paramName);
            }
        }

        // 繋ぐ先に in があるか（Entry は流れの始まりなので、入ってくるエッジを持たない）
        private static void CheckInput(NodeData to, string paramName)
        {
            switch (to)
            {
                case null:
                    throw new ArgumentNullException(paramName);
                case EntryNode _:
                    throw new ArgumentException("An Entry has no input: it is where the flow starts.", paramName);
                case ContainerEntryNode _:
                    throw new ArgumentException(
                        "A container's Entry has no input: connect to the container, and the flow goes on from its Entry.", paramName);
                case NoteNode _:
                    throw new ArgumentException(NoteHasNoPorts, paramName);
            }
        }

        private static ContainerExit FindExit(ContainerNode container, string exitName)
        {
            if (container == null)
            {
                throw new ArgumentNullException(nameof(container));
            }

            return container.Exits.FirstOrDefault(exit => exit != null && exit.Name == exitName)
                   ?? throw new ArgumentException($"Container '{container.Title}' has no exit named '{exitName}'.", nameof(exitName));
        }
    }
}
