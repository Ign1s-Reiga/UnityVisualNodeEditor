using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>
    /// エディタ側のコンテナのテストで使う 2 段のグラフ。
    /// <code>
    /// Root:  Entry → Stage(Clear, GameOver) ─Clear→ Result
    ///   Stage: StageEntry → Play → ClearExit(Clear)、Inner(Next)、グループ（Play を含む）、付箋
    ///     Inner: InnerEntry → Boss、InnerExit(Next)
    /// </code>
    /// </summary>
    internal sealed class ContainerTestGraph : IDisposable
    {
        public ContainerTestGraph()
        {
            Asset = ScriptableObject.CreateInstance<NodeGraphAsset>();
            Asset.name = "ContainerTestGraph";

            Entry = Add(new EntryNode { Position = new Vector2(0f, 0f) });
            Stage = Add(new StageTestContainer { Title = "Stage", Position = new Vector2(200f, 0f) });
            Result = Add(new StateNode { Title = "Result", Position = new Vector2(400f, 0f) });

            StageEntry = AddChild(Stage, new ContainerEntryNode { Position = new Vector2(0f, 0f) });
            Play = AddChild(Stage, new StateNode { Title = "Play", Position = new Vector2(200f, 0f) });
            Inner = AddChild(Stage, new ContainerNode { Title = "Inner", Position = new Vector2(200f, 200f) });
            ClearExit = AddChild(Stage, new ContainerExitNode { ExitId = ExitId(Stage, "Clear"), Position = new Vector2(400f, 0f) });

            InnerEntry = AddChild(Inner, new ContainerEntryNode { Position = new Vector2(0f, 0f) });
            Boss = AddChild(Inner, new StateNode { Title = "Boss", Position = new Vector2(200f, 0f) });
            InnerExit = AddChild(Inner, new ContainerExitNode { ExitId = Inner.Exits[0].Id, Position = new Vector2(400f, 0f) });

            Connect(Entry, "out", Stage);
            Connect(Stage, ExitId(Stage, "Clear"), Result);
            Connect(StageEntry, "out", Play);
            Connect(Play, "out", ClearExit);
            Connect(InnerEntry, "out", Boss);

            StageGroup = new GroupData { Title = "Gameplay", ParentId = Stage.Id, Position = new Vector2(180f, -20f) };
            StageGroup.AddNode(Play.Id);
            Asset.AddGroup(StageGroup);

            StageNote = new StickyNoteData { Title = "Note", ParentId = Stage.Id, Rect = new Rect(0f, 300f, 200f, 160f) };
            Asset.AddStickyNote(StageNote);
        }

        public NodeGraphAsset Asset { get; }

        public EntryNode Entry { get; }

        public StageTestContainer Stage { get; }

        public StateNode Result { get; }

        public ContainerEntryNode StageEntry { get; }

        public StateNode Play { get; }

        public ContainerNode Inner { get; }

        public ContainerExitNode ClearExit { get; }

        public ContainerEntryNode InnerEntry { get; }

        public StateNode Boss { get; }

        public ContainerExitNode InnerExit { get; }

        public GroupData StageGroup { get; }

        public StickyNoteData StageNote { get; }

        /// <summary>名前から出口の ID を引く（無ければ例外）。</summary>
        public static string ExitId(ContainerNode container, string name) =>
            container.TryGetExit(name, out var exit) ? exit.Id : throw new ArgumentException(name);

        public void Dispose() => Object.DestroyImmediate(Asset);

        private T Add<T>(T node) where T : NodeData
        {
            Asset.AddNode(node);
            return node;
        }

        private T AddChild<T>(NodeData parent, T node) where T : NodeData
        {
            node.ParentId = parent.Id;
            return Add(node);
        }

        private void Connect(NodeData from, string fromPort, NodeData to) =>
            Asset.AddEdge(new EdgeData(from.Id, fromPort, to.Id, "in"));
    }
}
