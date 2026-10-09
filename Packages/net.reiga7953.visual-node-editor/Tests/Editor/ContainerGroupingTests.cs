using System.Linq;
using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor.Views;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>選択したノードをコンテナにまとめる（Group into Container）。シナリオ B の「Game の流れをコンテナにする」。</summary>
    public sealed class ContainerGroupingTests
    {
        private NodeGraphAsset _graph;
        private EntryNode _entry;
        private StateNode _title;
        private EventNode _startGame;
        private StateNode _game;
        private EventNode _finish;
        private StateNode _result;

        [SetUp]
        public void SetUp()
        {
            // Entry → Title ─StartGame→ Game ─Finish→ Result
            _graph = ScriptableObject.CreateInstance<NodeGraphAsset>();
            _graph.name = "Flow";
            _entry = Add(new EntryNode { Position = new Vector2(0f, 0f) });
            _title = Add(new StateNode { Title = "Title", Position = new Vector2(200f, 0f) });
            _startGame = Add(new EventNode { EventName = "StartGame", Position = new Vector2(400f, 0f) });
            _game = Add(new StateNode { Title = "Game", Position = new Vector2(600f, 0f) });
            _finish = Add(new EventNode { EventName = "Finish", Position = new Vector2(800f, 0f) });
            _result = Add(new StateNode { Title = "Result", Position = new Vector2(1000f, 0f) });
            Connect(_entry, _title);
            Connect(_title, _startGame);
            Connect(_startGame, _game);
            Connect(_game, _finish);
            Connect(_finish, _result);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var runner in GraphRunner.Running.ToList())
            {
                runner.Stop();
            }

            Object.DestroyImmediate(_graph);
        }

        [Test]
        public void Grouping_MovesTheNodesAndKeepsTheFlowConnected()
        {
            var container = ContainerGrouping.Apply(_graph, new[] { _game.Id, _finish.Id }, string.Empty, out _);

            Assert.That(_game.ParentId, Is.EqualTo(container.Id));
            Assert.That(_finish.ParentId, Is.EqualTo(container.Id));
            Assert.That(container.IsAtRoot, Is.True);
            Assert.That(container.Exits.Select(e => e.Name), Is.EqualTo(new[] { "Finish" }), "named after the Event that leaves");

            var entry = _graph.GetChildren(container.Id).OfType<ContainerEntryNode>().Single();
            var exitNode = _graph.GetChildren(container.Id).OfType<ContainerExitNode>().Single();
            var edges = _graph.Edges.Select(e => (e.FromNodeId, e.FromPort, e.ToNodeId)).ToList();
            Assert.That(edges, Has.Member((_startGame.Id, "out", container.Id)), "outside: StartGame → container");
            Assert.That(edges, Has.Member((entry.Id, "out", _game.Id)), "inside: Entry → Game");
            Assert.That(edges, Has.Member((_game.Id, "out", _finish.Id)), "the edge between moved nodes stays");
            Assert.That(edges, Has.Member((_finish.Id, "out", exitNode.Id)), "inside: Finish → Exit");
            Assert.That(edges, Has.Member((container.Id, container.Exits[0].Id, _result.Id)), "outside: container.Finish → Result");
            Assert.That(GraphValidator.Validate(_graph).Where(i => i.Severity == GraphIssueSeverity.Error), Is.Empty);
        }

        [Test]
        public void Grouping_RunsTheSameWay()
        {
            ContainerGrouping.Apply(_graph, new[] { _game.Id, _finish.Id }, string.Empty, out _);
            var runner = new GraphRunner(_graph);
            runner.Start();

            runner.Raise("StartGame");
            Assert.That(runner.Current, Is.SameAs(_game), "enters the container and waits on Game");

            runner.Raise("Finish");
            Assert.That(runner.Current, Is.SameAs(_result), "leaves through the Finish exit");
        }

        [Test]
        public void Grouping_WithoutOutgoingEdges_KeepsTheDefaultExit()
        {
            var container = ContainerGrouping.Apply(_graph, new[] { _result.Id }, string.Empty, out _);

            Assert.That(container.Exits.Select(e => e.Name), Is.EqualTo(new[] { "Next" }));
            Assert.That(_graph.GetChildren(container.Id).OfType<ContainerExitNode>().Single().ExitId, Is.EqualTo(container.Exits[0].Id));
            Assert.That(_graph.Edges.Any(e => e.FromNodeId == _finish.Id && e.ToNodeId == container.Id), Is.True);
        }

        [Test]
        public void Grouping_MakesOneExitPerLeavingPort()
        {
            // Game からは Finish（中）と Credits（外）へ出ている
            var credits = Add(new StateNode { Title = "Credits", Position = new Vector2(600f, 200f) });
            Connect(_game, credits);

            var container = ContainerGrouping.Apply(_graph, new[] { _game.Id, _finish.Id }, string.Empty, out _);

            Assert.That(container.Exits.Select(e => e.Name), Is.EqualTo(new[] { "Finish", "Credits" }),
                "named after the leaving Event, or after the target when the source is not an Event");
            Assert.That(_graph.GetChildren(container.Id).OfType<ContainerExitNode>().Count(), Is.EqualTo(2));
        }

        [Test]
        public void Grouping_TakesTheEventsTheNodesRaise()
        {
            // Game だけを選んでも、Game から起こす Finish は一緒に入る（Raise は直接繋がった Event だけを探すので、境界で分けると起こせない）
            var container = ContainerGrouping.Apply(_graph, new[] { _game.Id }, string.Empty, out var problem);

            Assert.That(problem, Is.Null);
            Assert.That(_finish.ParentId, Is.EqualTo(container.Id));
            Assert.That(container.Exits.Select(e => e.Name), Is.EqualTo(new[] { "Finish" }));

            var runner = new GraphRunner(_graph);
            runner.Start();
            runner.Raise("StartGame");
            Assert.That(runner.Advance(), Is.False, "Game still waits for Finish instead of walking out");
            Assert.That(runner.Raise("Finish"), Is.True);
            Assert.That(runner.Current, Is.SameAs(_result));
        }

        [Test]
        public void Grouping_LeavesEventsRaisedFromOutside()
        {
            // StartGame は外の Title から起こすので、選んでいても外に残る
            var container = ContainerGrouping.Apply(_graph, new[] { _startGame.Id, _game.Id, _finish.Id }, string.Empty, out _);

            Assert.That(_startGame.IsAtRoot, Is.True);
            Assert.That(_graph.Edges.Any(e => e.FromNodeId == _startGame.Id && e.ToNodeId == container.Id), Is.True);

            var runner = new GraphRunner(_graph);
            runner.Start();
            Assert.That(runner.Raise("StartGame"), Is.True);
            Assert.That(runner.Current, Is.SameAs(_game));
        }

        [Test]
        public void Grouping_RefusesAnEventRaisedFromBothSides()
        {
            // Back は Game（中）からも Result（外）からも起こす
            var back = Add(new EventNode { EventName = "Back", Position = new Vector2(800f, 200f) });
            Connect(_game, back);
            Connect(_result, back);
            Connect(back, _title);

            var container = ContainerGrouping.Apply(_graph, new[] { _game.Id, _finish.Id }, string.Empty, out var problem);

            Assert.That(container, Is.Null);
            Assert.That(problem, Does.Contain("'Back'"));
            Assert.That(_graph.Nodes.OfType<ContainerNode>(), Is.Empty, "nothing changes");
            Assert.That(_game.IsAtRoot, Is.True);
        }

        [Test]
        public void Grouping_RefusesSeveralWaysIn()
        {
            // Game へは StartGame から、Credits へは Options から入る。コンテナの入口は 1 つなので保てない
            var options = Add(new EventNode { EventName = "Options", Position = new Vector2(400f, 200f) });
            var credits = Add(new StateNode { Title = "Credits", Position = new Vector2(600f, 200f) });
            Connect(_title, options);
            Connect(options, credits);

            var container = ContainerGrouping.Apply(_graph, new[] { _game.Id, credits.Id }, string.Empty, out var problem);

            Assert.That(container, Is.Null);
            Assert.That(problem, Does.Contain("'Game'").And.Contain("'Credits'"));
            Assert.That(_graph.Nodes.OfType<ContainerNode>(), Is.Empty);
        }

        [Test]
        public void Grouping_KeepsWhereAdvanceGoesFromOutside()
        {
            // Title の Advance は、Event 以外への最初のエッジ（Shop）へ進む。Shop をまとめても、コンテナへのエッジが同じ位置に残る
            var shop = Add(new StateNode { Title = "Shop", Position = new Vector2(400f, 200f) });
            var map = Add(new StateNode { Title = "Map", Position = new Vector2(400f, 400f) });
            Connect(_title, shop);
            Connect(_title, map);

            ContainerGrouping.Apply(_graph, new[] { shop.Id }, string.Empty, out _);

            var runner = new GraphRunner(_graph);
            runner.Start();
            Assert.That(runner.Advance(), Is.True);
            Assert.That(runner.Current, Is.SameAs(shop), "enters the container, not Map");
        }

        [Test]
        public void Grouping_KeepsWhereAdvanceGoesFromInside()
        {
            // Game の Advance は Credits（外）へ進み、Bonus（中）へは進まない。まとめても Exit ノードへのエッジが Credits の位置に残る
            var credits = Add(new StateNode { Title = "Credits", Position = new Vector2(800f, 200f) });
            var bonus = Add(new StateNode { Title = "Bonus", Position = new Vector2(800f, 400f) });
            Connect(_game, credits);
            Connect(_game, bonus);

            ContainerGrouping.Apply(_graph, new[] { _game.Id, bonus.Id }, string.Empty, out _);

            var runner = new GraphRunner(_graph);
            runner.Start();
            runner.Raise("StartGame");
            Assert.That(runner.Advance(), Is.True);
            Assert.That(runner.Current, Is.SameAs(credits), "leaves through the exit, not into Bonus");
        }

        [Test]
        public void Grouping_WithoutAWayIn_StartsAtTheLeftmostState()
        {
            // 外から入ってこないとき、ポートの無い Note からは始めない
            var note = Add(new NoteNode { Position = new Vector2(-200f, 400f) });
            var lonely = Add(new StateNode { Title = "Lonely", Position = new Vector2(0f, 400f) });

            var container = ContainerGrouping.Apply(_graph, new[] { note.Id, lonely.Id }, string.Empty, out _);

            var entry = _graph.GetChildren(container.Id).OfType<ContainerEntryNode>().Single();
            var fromEntry = _graph.Edges.Where(e => e.FromNodeId == entry.Id).Select(e => (e.ToNodeId, e.ToPort)).ToList();
            Assert.That(fromEntry, Is.EqualTo(new[] { (lonely.Id, "in") }));
        }

        [Test]
        public void EntryAndContainerEndpoints_AreNeverMoved()
        {
            Assert.That(ContainerGrouping.Apply(_graph, new[] { _entry.Id }, string.Empty, out var problem), Is.Null);
            Assert.That(problem, Does.StartWith("Nothing to group"));
            Assert.That(ContainerGrouping.CanGroup(new ContainerEntryNode()), Is.False);
            Assert.That(ContainerGrouping.CanGroup(new ContainerExitNode()), Is.False);
            Assert.That(ContainerGrouping.CanGroup(new StateNode()), Is.True);
            Assert.That(ContainerGrouping.Apply(_graph, new string[0], string.Empty, out _), Is.Null);
        }

        [Test]
        public void GraphView_GroupsTheSelectionInOneUndoStep()
        {
            var view = new NodeGraphView();
            view.Populate(_graph);
            view.AddToSelection(view.FindNodeView(_game.Id));
            view.AddToSelection(view.FindNodeView(_finish.Id));
            Undo.IncrementCurrentGroup();

            var container = view.GroupSelectionIntoContainer();

            Assert.That(view.FindNodeView(container.Id), Is.Not.Null);
            Assert.That(view.FindNodeView(_game.Id), Is.Null, "now inside the container");
            Assert.That(view.selection.OfType<NodeView>().Single().NodeId, Is.EqualTo(container.Id));

            Undo.PerformUndo();
            var game = _graph.FindNode(_game.Id);
            Assert.That(game.IsAtRoot, Is.True);
            Assert.That(_graph.Nodes.OfType<ContainerNode>(), Is.Empty);
        }

        [Test]
        public void GraphView_TellsWhyItCannotGroup()
        {
            var credits = Add(new StateNode { Title = "Credits" });
            var options = Add(new EventNode { EventName = "Options" });
            Connect(_title, options);
            Connect(options, credits);
            var view = new NodeGraphView();
            view.Populate(_graph);
            string message = null;
            view.NotificationRequested += m => message = m;
            view.AddToSelection(view.FindNodeView(_game.Id));
            view.AddToSelection(view.FindNodeView(credits.Id));

            Assert.That(view.GroupSelectionIntoContainer(), Is.Null);
            Assert.That(message, Does.StartWith("Cannot group"));
            Assert.That(view.FindNodeView(_game.Id), Is.Not.Null, "still at this level");
        }

        private T Add<T>(T node) where T : NodeData
        {
            _graph.AddNode(node);
            return node;
        }

        private void Connect(NodeData from, NodeData to) => _graph.AddEdge(new EdgeData(from.Id, "out", to.Id, "in"));
    }
}
