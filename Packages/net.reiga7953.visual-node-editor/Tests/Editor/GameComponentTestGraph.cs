using UnityEngine;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>
    /// ゲーム向けのコンポーネントのテストに使うグラフ:
    /// Root: Entry → Round ─Over→ Game Over
    ///   Round: Entry → Playing ─Pause→ Paused ─Pause→ Playing、Playing ─Crash→ Exit (Over)
    /// Blackboard: Score（Int、0）、Speed（Float、1.5）
    /// </summary>
    internal sealed class GameComponentTestGraph
    {
        public GameComponentTestGraph()
        {
            Graph = ScriptableObject.CreateInstance<NodeGraphAsset>();
            Graph.name = "Game";
            Score = new GraphParameter("Score", GraphParameterType.Int);
            Speed = new GraphParameter("Speed", GraphParameterType.Float) { FloatValue = 1.5f };
            Graph.AddParameter(Score);
            Graph.AddParameter(Speed);

            var entry = Add(new EntryNode(), string.Empty);
            Round = Add(new ContainerNode { Title = "Round" }, string.Empty);
            Round.RenameExit(Round.Exits[0].Id, "Over");
            GameOver = Add(new StateNode { Title = "Game Over" }, string.Empty);
            Connect(entry, "out", Round);
            Connect(Round, Round.Exits[0].Id, GameOver);

            var inside = Add(new ContainerEntryNode(), Round.Id);
            Playing = Add(new StateNode { Title = "Playing" }, Round.Id);
            Paused = Add(new StateNode { Title = "Paused" }, Round.Id);
            var pause = Add(new EventNode { EventName = "Pause" }, Round.Id);
            var resume = Add(new EventNode { EventName = "Pause" }, Round.Id);
            var crash = Add(new EventNode { EventName = "Crash" }, Round.Id);
            var exit = Add(new ContainerExitNode { ExitId = Round.Exits[0].Id }, Round.Id);
            Connect(inside, "out", Playing);
            Connect(Playing, "out", pause);
            Connect(pause, "out", Paused);
            Connect(Paused, "out", resume);
            Connect(resume, "out", Playing);
            Connect(Playing, "out", crash);
            Connect(crash, "out", exit);
        }

        public NodeGraphAsset Graph { get; }

        public GraphParameter Score { get; }

        public GraphParameter Speed { get; }

        public ContainerNode Round { get; }

        public StateNode Playing { get; }

        public StateNode Paused { get; }

        public StateNode GameOver { get; }

        private T Add<T>(T node, string parentId) where T : NodeData
        {
            node.ParentId = parentId;
            Graph.AddNode(node);
            return node;
        }

        private void Connect(NodeData from, string port, NodeData to) => Graph.AddEdge(new EdgeData(from.Id, port, to.Id, "in"));
    }
}
