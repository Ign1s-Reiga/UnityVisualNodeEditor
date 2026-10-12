using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>
    /// コンテナを 5 段に入れ子にしても（コンテナの中のコンテナの中の…）、入って、いちばん奥から出て、ルートへ戻れる。
    /// Root: Entry → Level1 ─Out→ Result
    ///   Level1〜4: Entry → 次の Level ─Out→ Exit(Out)
    ///     Level5: Entry → Play ─Done→ Exit(Out)
    /// </summary>
    public sealed class ContainerNestingTests
    {
        private const int Depth = 5;

        private NodeGraphAsset _graph;
        private readonly List<ContainerNode> _levels = new List<ContainerNode>();
        private StateNode _play;
        private StateNode _result;
        private GraphRunner _runner;

        [SetUp]
        public void SetUp()
        {
            _graph = ScriptableObject.CreateInstance<NodeGraphAsset>();
            _graph.name = "Nested";
            _levels.Clear();
            var entry = Add(new EntryNode(), string.Empty);
            _result = Add(new StateNode { Title = "Result" }, string.Empty);

            // 外側から順に、コンテナを 1 つ下の階層へ作っていく
            var parentId = string.Empty;
            NodeData previousEntry = entry;
            for (var i = 1; i <= Depth; i++)
            {
                var level = Add(new ContainerNode { Title = "Level" + i }, parentId);
                level.RenameExit(level.Exits[0].Id, "Out");
                _levels.Add(level);
                Connect(previousEntry, "out", level);
                previousEntry = Add(new ContainerEntryNode(), level.Id);
                parentId = level.Id;
            }

            // いちばん奥: Entry → Play ─Done→ Exit
            var deepest = _levels[Depth - 1];
            _play = Add(new StateNode { Title = "Play" }, deepest.Id);
            var done = Add(new EventNode { EventName = "Done" }, deepest.Id);
            Connect(previousEntry, "out", _play);
            Connect(_play, "out", done);
            Connect(done, "out", Add(new ContainerExitNode { ExitId = deepest.Exits[0].Id }, deepest.Id));

            // 各段の出口を、1 つ外の段の Exit（ルートでは Result）へ繋ぐ
            for (var i = Depth - 1; i >= 0; i--)
            {
                var level = _levels[i];
                var target = i == 0
                    ? (NodeData)_result
                    : Add(new ContainerExitNode { ExitId = _levels[i - 1].Exits[0].Id }, _levels[i - 1].Id);
                Connect(level, level.Exits[0].Id, target);
            }

            _runner = new GraphRunner(_graph);
        }

        [TearDown]
        public void TearDown()
        {
            _runner.Stop();
            Object.DestroyImmediate(_graph);
        }

        [Test]
        public void FiveLevelsOfContainers_AreValid()
        {
            Assert.That(GraphValidator.Validate(_graph), Is.Empty);
            Assert.That(_runner.Query.GetContainerPath(_play), Is.EqualTo(_levels), "Play is five containers deep");
        }

        [Test]
        public void Start_GoesDownThroughAllFiveContainers()
        {
            _runner.Start();

            Assert.That(_runner.Current, Is.SameAs(_play));
            Assert.That(_runner.ContainerPath, Is.EqualTo(_levels), "outermost first");
            Assert.That(_levels.All(level => GraphNodeActivator.IsIn(_runner, level.Id)), Is.True, "inside every level at once");
        }

        [Test]
        public void LeavingTheDeepestContainer_ComesBackOutThroughEveryLevel()
        {
            _runner.Start();
            var entered = new List<NodeData>();
            _runner.NodeEntered += node => entered.Add(node);

            Assert.That(_runner.Raise("Done"), Is.True);

            Assert.That(_runner.Current, Is.SameAs(_result));
            Assert.That(_runner.ContainerPath, Is.Empty);
            Assert.That(entered.Count(node => node is ContainerExitNode), Is.EqualTo(Depth), "one Exit per level");
            Assert.That(_levels.Any(level => GraphNodeActivator.IsIn(_runner, level.Id)), Is.False);
        }

        private T Add<T>(T node, string parentId) where T : NodeData
        {
            node.ParentId = parentId;
            _graph.AddNode(node);
            return node;
        }

        private void Connect(NodeData from, string port, NodeData to) => _graph.AddEdge(new EdgeData(from.Id, port, to.Id, "in"));
    }
}
