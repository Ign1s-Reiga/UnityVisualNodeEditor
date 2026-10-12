using System.Linq;
using Reiga.VisualNodeEditor.Editor;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Starters.Editor
{
    /// <summary>
    /// Starter のグラフを組み立てる小さな道具。新しいグラフ（<see cref="NodeGraphFactory.CreateNew"/>。Entry とセーブキー入り）に、
    /// ノード・コンテナ（出口と中の Entry 付き）・Event・エッジ・振る舞いを足していく。
    /// </summary>
    internal sealed class StarterGraphBuilder
    {
        private const string In = "in";
        private const string Out = "out";

        public StarterGraphBuilder()
        {
            Graph = NodeGraphFactory.CreateNew();
            Entry = Graph.Nodes.OfType<EntryNode>().Single();
        }

        /// <summary>組み立てているグラフ（まだアセットとして保存していない）。</summary>
        public NodeGraphAsset Graph { get; }

        /// <summary>ルートの Entry。</summary>
        public EntryNode Entry { get; }

        /// <summary>Blackboard にパラメータを足す。</summary>
        public GraphParameter AddParameter(GraphParameter parameter)
        {
            Graph.AddParameter(parameter);
            return parameter;
        }

        /// <summary><paramref name="parent"/>（null ならルート）の <paramref name="x"/>, <paramref name="y"/> にノードを置く。</summary>
        public T Add<T>(T node, float x, float y, ContainerNode parent = null) where T : NodeData
        {
            node.ParentId = parent != null ? parent.Id : string.Empty;
            node.Position = new Vector2(x, y);
            Graph.AddNode(node);
            return node;
        }

        /// <summary>出口が <paramref name="exits"/> のコンテナと、その中の Entry（左端）を置く。</summary>
        public ContainerNode AddContainer(string title, float x, float y, ContainerNode parent, params string[] exits)
        {
            var container = Add(new ContainerNode { Title = title }, x, y, parent);
            foreach (var exit in container.Exits.ToList())
            {
                container.RemoveExit(exit.Id);
            }

            foreach (var exit in exits)
            {
                container.AddExit(exit);
            }

            Add(new ContainerEntryNode(), 0f, 0f, container);
            return container;
        }

        /// <summary>コンテナの中の Entry。</summary>
        public ContainerEntryNode EntryOf(ContainerNode container) =>
            Graph.Nodes.OfType<ContainerEntryNode>().Single(node => node.ParentId == container.Id);

        /// <summary>コンテナの中に、その出口 <paramref name="exitName"/> から出る Exit ノードを置く。</summary>
        public ContainerExitNode AddExit(ContainerNode container, string exitName, float x, float y) =>
            Add(new ContainerExitNode { ExitId = container.Exits.Single(exit => exit.Name == exitName).Id }, x, y, container);

        /// <summary>イベント名 <paramref name="eventName"/> の Event（<paramref name="automatic"/> なら自動の Event。名前は空でよい）。</summary>
        public EventNode AddEvent(string eventName, float x, float y, ContainerNode parent = null, bool automatic = false) =>
            Add(new EventNode
            {
                EventName = eventName ?? string.Empty,
                Trigger = automatic ? EventTriggerMode.Automatic : EventTriggerMode.Raise,
            }, x, y, parent);

        /// <summary><paramref name="from"/> の out から <paramref name="to"/> の in へ繋ぐ。</summary>
        public void Connect(NodeData from, NodeData to) => Graph.AddEdge(new EdgeData(from.Id, Out, to.Id, In));

        /// <summary><paramref name="from"/> → <paramref name="via"/>（Event）→ <paramref name="to"/>。</summary>
        public void Connect(NodeData from, EventNode via, NodeData to)
        {
            Connect(from, via);
            Connect(via, to);
        }

        /// <summary>コンテナの出口 <paramref name="exitName"/> から <paramref name="to"/> へ繋ぐ。</summary>
        public void ConnectExit(ContainerNode container, string exitName, NodeData to) =>
            Graph.AddEdge(new EdgeData(container.Id, container.Exits.Single(exit => exit.Name == exitName).Id, to.Id, In));

        /// <summary><paramref name="node"/> に入ったとき、<paramref name="changes"/> を行う Set Parameter の振る舞いを付ける。</summary>
        public void SetOnEnter(IBehaviourHost node, params ParameterChange[] changes)
        {
            var behaviour = new SetParameterBehaviour();
            behaviour.Changes.AddRange(changes);
            node.AddBehaviour(behaviour);
        }

        /// <summary><paramref name="node"/> にいる間 Time.timeScale を <paramref name="timeScale"/> にする振る舞いを付ける。</summary>
        public void TimeScaleWhileOn(IBehaviourHost node, float timeScale) => node.AddBehaviour(new TimeScaleBehaviour { TimeScale = timeScale });
    }
}
