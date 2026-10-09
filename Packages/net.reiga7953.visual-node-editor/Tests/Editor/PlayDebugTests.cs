using System.Linq;
using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor.Debugging;
using Reiga.VisualNodeEditor.Editor.Views;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>Play 中のデバッグ（Now running パネルの操作、Blackboard の実行中の値、通ったノードの軌跡）。</summary>
    public sealed class PlayDebugTests
    {
        private NodeGraphAsset _graph;
        private StateNode _title;
        private StateNode _game;
        private StateNode _credits;

        [SetUp]
        public void SetUp()
        {
            // Entry → Title ─StartGame→ Game ─Back→ Title
            //               ─Options（行き先なしの通知）
            //         Title → Credits（Advance）
            _graph = ScriptableObject.CreateInstance<NodeGraphAsset>();
            _graph.name = "Debug";
            var entry = Add(new EntryNode());
            _title = Add(new StateNode { Title = "Title" });
            _game = Add(new StateNode { Title = "Game" });
            _credits = Add(new StateNode { Title = "Credits" });
            var start = Add(new EventNode { EventName = "StartGame" });
            var options = Add(new EventNode { EventName = "Options" });
            var unnamed = Add(new EventNode { EventName = "" });
            var back = Add(new EventNode { EventName = "Back" });
            Connect(entry, _title);
            Connect(_title, start);
            Connect(start, _game);
            Connect(_title, options);
            Connect(_title, unnamed);
            Connect(_title, _credits);
            Connect(_game, back);
            Connect(back, _title);
            _graph.AddParameter(new GraphParameter("Lives", GraphParameterType.Int) { IntValue = 3 });
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

        // ---- 起こせる操作 ----

        [Test]
        public void Actions_AreTheOutgoingEventsThenAdvance()
        {
            var actions = RuntimeActions.For(new GraphQuery(_graph), _title);

            Assert.That(actions.Select(a => a.Label), Is.EqualTo(new[] { "StartGame", "Options", "Advance → Credits" }),
                "events without a name cannot be raised, so they are left out");
            Assert.That(actions[0].Kind, Is.EqualTo(RuntimeActionKind.Raise));
            Assert.That(actions[2].Kind, Is.EqualTo(RuntimeActionKind.Advance));
            Assert.That(RuntimeActions.For(new GraphQuery(_graph), null), Is.Empty);
        }

        [Test]
        public void Action_RunsOnTheRunner()
        {
            var runner = new GraphRunner(_graph);
            runner.Start();

            Assert.That(RuntimeAction.Raise("StartGame").Run(runner), Is.True);
            Assert.That(runner.Current, Is.SameAs(_game));
        }

        // ---- Now running パネル ----

        [Test]
        public void Panel_ShowsWhereTheRunnerIsAndWhatCanHappen()
        {
            var runner = new GraphRunner(_graph);
            runner.Start();
            var panel = new RuntimePanel();

            panel.Show(runner);

            Assert.That(panel.ClassListContains("vne-runtime-panel--hidden"), Is.False);
            Assert.That(panel.LocationText, Is.EqualTo("Title"));
            Assert.That(panel.ActionButtons.Select(b => b.text), Is.EqualTo(new[] { "StartGame", "Options", "Advance → Credits" }));

            runner.Raise("StartGame");
            panel.Refresh();
            Assert.That(panel.LocationText, Is.EqualTo("Game"));
            Assert.That(panel.ActionButtons.Select(b => b.text), Is.EqualTo(new[] { "Back" }));

            runner.Stop();
            panel.Refresh();
            Assert.That(panel.ClassListContains("vne-runtime-panel--hidden"), Is.True);
        }

        [Test]
        public void Panel_LocationIncludesTheContainers()
        {
            using var containers = new ContainerTestGraph();
            var runner = new GraphRunner(containers.Asset);
            runner.Start();

            Assert.That(RuntimePanel.GetLocation(runner), Is.EqualTo("Stage › Play"));
        }

        // ---- Blackboard の実行中の値 ----

        [Test]
        public void ParameterText_ShowsTheRuntimeValue()
        {
            Assert.That(RuntimeParameterText.Format(GraphParameterType.Int, true, 3), Is.EqualTo("Int = 3"));
            Assert.That(RuntimeParameterText.Format(GraphParameterType.Float, true, 1.5f), Is.EqualTo("Float = 1.5"));
            Assert.That(RuntimeParameterText.Format(GraphParameterType.Bool, true, true), Is.EqualTo("Bool = true"));
            Assert.That(RuntimeParameterText.Format(GraphParameterType.String, true, "Hard"), Is.EqualTo("String = \"Hard\""));
            Assert.That(RuntimeParameterText.Format(GraphParameterType.Int, false, null), Is.EqualTo("Int"), "not running");
        }

        [Test]
        public void Blackboard_ShowsAndFollowsTheRunnersValues()
        {
            var view = new NodeGraphView();
            view.Populate(_graph);
            var runner = new GraphRunner(_graph);
            runner.Start();
            var field = view.Blackboard.Query<BlackboardField>().First();

            view.Blackboard.ShowRuntimeValues(runner);
            Assert.That(field.typeText, Is.EqualTo("Int = 3"));

            runner.SetInt("Lives", 1);
            view.Blackboard.RefreshRuntimeValues();
            Assert.That(field.typeText, Is.EqualTo("Int = 1"));

            view.Blackboard.ShowRuntimeValues(null);
            Assert.That(field.typeText, Is.EqualTo("Int"));
            Assert.That(_graph.Parameters.Single().IntValue, Is.EqualTo(3), "the asset default does not change");
        }

        [Test]
        public void Blackboard_ShowsNoValueWhenTheRunnersTypeDiffers()
        {
            // Play 中に Lives（Int）を消し、Float の Speed を Lives に改名した。Runner の Lives は Int のまま
            var speed = new GraphParameter("Speed", GraphParameterType.Float) { FloatValue = 2f };
            _graph.AddParameter(speed);
            var runner = new GraphRunner(_graph);
            runner.Start();
            _graph.RemoveParameter(_graph.FindParameterByName("Lives"));
            speed.Name = "Lives";
            var view = new NodeGraphView();
            view.Populate(_graph);

            Assert.DoesNotThrow(() => view.Blackboard.ShowRuntimeValues(runner));
            Assert.That(view.Blackboard.Query<BlackboardField>().First().typeText, Is.EqualTo("Float"));
            Assert.That(runner.HasParameter("Lives", GraphParameterType.Int), Is.True);
            Assert.That(runner.HasParameter("Lives", GraphParameterType.Float), Is.False);
        }

        // ---- 軌跡 ----

        [Test]
        public void Trail_KeepsRecentNodesNewestFirst()
        {
            var trail = new RunTrail(2);

            trail.Visit("a");
            trail.Visit("b");
            trail.Visit("c");
            Assert.That(trail.NodeIds, Is.EqualTo(new[] { "b", "a" }));

            trail.Visit("d");
            Assert.That(trail.NodeIds, Is.EqualTo(new[] { "c", "b" }), "the oldest drops off");

            trail.Visit("b");
            Assert.That(trail.NodeIds, Is.EqualTo(new[] { "d", "c" }), "the current node is not part of the trail");

            trail.Visit(null);
            Assert.That(trail.NodeIds, Is.EqualTo(new[] { "b", "d" }), "stopping keeps the last node in the trail");

            trail.Clear();
            Assert.That(trail.NodeIds, Is.Empty);
        }

        [Test]
        public void Graph_HighlightsTheTrailFaintly()
        {
            var view = new NodeGraphView();
            view.Populate(_graph);

            view.SetRunningNode(_title.Id);
            view.SetRunningNode(_game.Id);

            Assert.That(view.FindNodeView(_game.Id).IsRunning, Is.True);
            Assert.That(view.FindNodeView(_game.Id).IsVisited, Is.False);
            Assert.That(view.FindNodeView(_title.Id).IsVisited, Is.True);
            Assert.That(view.FindNodeView(_title.Id).ClassListContains("vne-node--visited"), Is.True);

            view.ClearTrail();

            Assert.That(view.FindNodeView(_title.Id).IsVisited, Is.False);
            Assert.That(view.FindNodeView(_game.Id).IsRunning, Is.True, "clearing the trail keeps the current node");
        }

        private T Add<T>(T node) where T : NodeData
        {
            _graph.AddNode(node);
            return node;
        }

        private void Connect(NodeData from, NodeData to) => _graph.AddEdge(new EdgeData(from.Id, "out", to.Id, "in"));
    }
}
