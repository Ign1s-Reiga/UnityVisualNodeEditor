using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>
    /// 2 段の入れ子:
    /// Root: Entry → Outer(Container) ─Done→ Result(State)
    ///   Outer: OuterEntry → Inner(Container) ─Clear→ OuterExit(Done)
    ///                                        ─GameOver→ Retry(State)
    ///     Inner: InnerEntry → Play(State) ─Win→ WinEvent → InnerExit(Clear)
    ///                                     ─Lose→ LoseEvent → LoseExit(GameOver)
    /// </summary>
    public sealed class ContainerTraversalTests
    {
        private NodeGraphAsset _graph;
        private EntryNode _rootEntry;
        private ContainerNode _outer;
        private ContainerNode _inner;
        private ContainerEntryNode _outerEntry;
        private ContainerEntryNode _innerEntry;
        private ContainerExitNode _outerExit;
        private ContainerExitNode _innerExit;
        private ContainerExitNode _loseExit;
        private StateNode _play;
        private StateNode _retry;
        private StateNode _result;

        [SetUp]
        public void SetUp()
        {
            _graph = ScriptableObject.CreateInstance<NodeGraphAsset>();
            _graph.name = "Containers";
            _rootEntry = Add(new EntryNode());
            _outer = Add(new ContainerNode { Title = "Outer" });
            _outer.RenameExit(_outer.Exits[0].Id, "Done");
            _result = Add(new StateNode { Title = "Result" });

            _outerEntry = AddChild(_outer, new ContainerEntryNode { Title = "OuterEntry" });
            _inner = AddChild(_outer, new ContainerNode { Title = "Inner" });
            _inner.RenameExit(_inner.Exits[0].Id, "Clear");
            _inner.AddExit("GameOver");
            _outerExit = AddChild(_outer, new ContainerExitNode { Title = "OuterExit", ExitId = Exit(_outer, "Done") });
            _retry = AddChild(_outer, new StateNode { Title = "Retry" });

            _innerEntry = AddChild(_inner, new ContainerEntryNode { Title = "InnerEntry" });
            _play = AddChild(_inner, new StateNode { Title = "Play" });
            var win = AddChild(_inner, new EventNode { EventName = "Win" });
            var lose = AddChild(_inner, new EventNode { EventName = "Lose" });
            _innerExit = AddChild(_inner, new ContainerExitNode { Title = "InnerExit", ExitId = Exit(_inner, "Clear") });
            _loseExit = AddChild(_inner, new ContainerExitNode { Title = "LoseExit", ExitId = Exit(_inner, "GameOver") });

            Connect(_rootEntry, "out", _outer);
            Connect(_outer, Exit(_outer, "Done"), _result);
            Connect(_outerEntry, "out", _inner);
            Connect(_inner, Exit(_inner, "Clear"), _outerExit);
            Connect(_inner, Exit(_inner, "GameOver"), _retry);
            Connect(_innerEntry, "out", _play);
            Connect(_play, "out", win);
            Connect(_play, "out", lose);
            Connect(win, "out", _innerExit);
            Connect(lose, "out", _loseExit);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_graph);

        [Test]
        public void GraphIsValid()
        {
            Assert.That(GraphValidator.Validate(_graph), Is.Empty);
        }

        [Test]
        public void Start_EntersBothContainersDownToTheFirstWaitNode()
        {
            var runner = new GraphRunner(_graph);
            var entered = new List<string>();
            runner.NodeEntered += n => entered.Add(n.Title);

            runner.Start();

            Assert.That(entered, Is.EqualTo(new[] { "Entry", "Outer", "OuterEntry", "Inner", "InnerEntry", "Play" }));
            Assert.That(runner.Current, Is.SameAs(_play));
            Assert.That(runner.ContainerPath, Is.EqualTo(new[] { _outer, _inner }), "stack: outermost first");
        }

        [Test]
        public void ExitingTwoLevels_ContinuesFromTheMatchingPortsUpToTheRoot()
        {
            var runner = new GraphRunner(_graph);
            runner.Start();
            var entered = new List<string>();
            runner.NodeEntered += n => entered.Add(n.Title);

            Assert.That(runner.Raise("Win"), Is.True);

            Assert.That(entered, Is.EqualTo(new[] { "Event", "InnerExit", "OuterExit", "Result" }));
            Assert.That(runner.Current, Is.SameAs(_result));
            Assert.That(runner.ContainerPath, Is.Empty);
        }

        [Test]
        public void ExitingByAnotherExit_UsesThatExitsPort()
        {
            var runner = new GraphRunner(_graph);
            runner.Start();

            runner.Raise("Lose");

            Assert.That(runner.Current, Is.SameAs(_retry), "GameOver port of Inner leads to Retry inside Outer");
            Assert.That(runner.ContainerPath, Is.EqualTo(new[] { _outer }));
        }

        [Test]
        public void UnconnectedExitPort_StaysOnTheWaitNodeForNow()
        {
            _graph.RemoveEdge(_graph.Edges.Single(e => e.FromNodeId == _inner.Id && e.FromPort == Exit(_inner, "GameOver")));
            var runner = new GraphRunner(_graph);
            runner.Start();
            var triggered = new List<string>();
            runner.EventTriggered += triggered.Add;

            Assert.That(runner.Raise("Lose"), Is.True);

            Assert.That(runner.Current, Is.SameAs(_play), "provisional: stay where we were (open question)");
            Assert.That(triggered, Is.EqualTo(new[] { "Lose" }), "the Event on the way still notifies");
        }

        [Test]
        public void ContainerWithoutEntry_IsNotEntered()
        {
            _graph.RemoveNode(_innerEntry);
            var runner = new GraphRunner(_graph);

            var entered = new List<string>();
            runner.NodeEntered += n => entered.Add(n.Title);

            runner.Start();

            Assert.That(runner.Current, Is.SameAs(_rootEntry), "nothing to wait on yet, so it stays on the root Entry");
            Assert.That(entered, Is.EqualTo(new[] { "Entry" }), "does not enter the containers on the way");
            Assert.That(runner.IsRunning, Is.True);
        }

        [Test]
        public void ContainerWithoutEntry_MidRun_StaysOnTheWaitNode()
        {
            // Inner の Clear の先を、Entry の無いコンテナに繋ぎ替える
            var broken = AddChild(_outer, new ContainerNode { Title = "Broken" });
            _graph.RemoveEdge(_graph.Edges.Single(e => e.FromNodeId == _inner.Id && e.FromPort == Exit(_inner, "Clear")));
            Connect(_inner, Exit(_inner, "Clear"), broken);
            var runner = new GraphRunner(_graph);
            runner.Start();

            Assert.That(runner.Raise("Win"), Is.True);

            Assert.That(runner.Current, Is.SameAs(_play));
        }

        [Test]
        public void DataApi_ResolvesPortsEntriesAndExits()
        {
            var query = new GraphQuery(_graph);

            Assert.That(query.Entry, Is.SameAs(_rootEntry));
            Assert.That(query.GetNextNode(_inner.Id, Exit(_inner, "GameOver")), Is.SameAs(_retry));
            Assert.That(query.GetNextNode(_inner.Id, "unknown-port"), Is.Null);
            Assert.That(query.GetContainerEntry(_inner), Is.SameAs(_innerEntry));
            Assert.That(query.GetExitTarget(_innerExit), Is.SameAs(_outerExit));
            Assert.That(query.GetExitTarget(_outerExit), Is.SameAs(_result));
            Assert.That(query.GetParentContainer(_play), Is.SameAs(_inner));
            Assert.That(query.GetParentContainer(_result), Is.Null);
            Assert.That(query.GetContainerPath(_play), Is.EqualTo(new[] { _outer, _inner }));
        }

        private T Add<T>(T node) where T : NodeData
        {
            _graph.AddNode(node);
            return node;
        }

        private T AddChild<T>(NodeData parent, T node) where T : NodeData
        {
            node.ParentId = parent.Id;
            return Add(node);
        }

        private static string Exit(ContainerNode container, string name) =>
            container.TryGetExit(name, out var exit) ? exit.Id : throw new System.ArgumentException(name);

        private void Connect(NodeData from, string fromPort, NodeData to) =>
            _graph.AddEdge(new EdgeData(from.Id, fromPort, to.Id, "in"));
    }
}
