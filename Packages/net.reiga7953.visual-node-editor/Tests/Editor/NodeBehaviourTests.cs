using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>
    /// ノードの振る舞い（<see cref="NodeBehaviour"/>）と、GraphRunner からの呼び出し。
    /// Entry → Title(State) ─Go→ Play(State) ─Back→ Title
    /// </summary>
    public sealed class NodeBehaviourTests
    {
        private NodeGraphAsset _graph;
        private EntryNode _entry;
        private StateNode _title;
        private StateNode _play;
        private Action<Exception> _originalExceptionHandler;

        [SetUp]
        public void SetUp()
        {
            RecordingBehaviour.Log.Clear();
            _originalExceptionHandler = GraphRunner.BehaviourExceptionHandler;

            _graph = ScriptableObject.CreateInstance<NodeGraphAsset>();
            _graph.name = "Behaviours";
            _entry = Add(new EntryNode());
            _title = Add(new StateNode { Title = "Title" });
            _play = Add(new StateNode { Title = "Play" });
            var go = Add(new EventNode { EventName = "Go" });
            var back = Add(new EventNode { EventName = "Back" });
            Connect(_entry, _title);
            Connect(_title, go);
            Connect(go, _play);
            Connect(_play, back);
            Connect(back, _title);
        }

        [TearDown]
        public void TearDown()
        {
            GraphRunner.BehaviourExceptionHandler = _originalExceptionHandler;
            foreach (var runner in GraphRunner.Running.ToList())
            {
                runner.Stop();
            }

            Object.DestroyImmediate(_graph);
        }

        // ---- 呼ばれる順 ----

        [Test]
        public void Lifecycle_EnterUpdateFixedUpdateExit()
        {
            _title.AddBehaviour(new RecordingBehaviour { Tag = "Title" });
            _play.AddBehaviour(new RecordingBehaviour { Tag = "Play" });
            var runner = new GraphRunner(_graph);

            runner.Start();
            runner.Update(0.5f);
            runner.FixedUpdate(0.02f);
            runner.Raise("Go");
            runner.Update(0.25f);
            runner.Stop();

            Assert.That(RecordingBehaviour.Log, Is.EqualTo(new[]
            {
                "Title.Enter", "Title.Update(0.5)", "Title.FixedUpdate(0.02)",
                "Title.Exit", "Play.Enter", "Play.Update(0.25)", "Play.Exit",
            }));
        }

        [Test]
        public void OnEnterComesBeforeNodeEntered_AndOnExitBeforeNodeExited()
        {
            _title.AddBehaviour(new RecordingBehaviour { Tag = "Title" });
            var runner = new GraphRunner(_graph);
            runner.NodeEntered += n => RecordingBehaviour.Log.Add("Entered " + n.Title);
            runner.NodeExited += n => RecordingBehaviour.Log.Add("Exited " + n.Title);

            runner.Start();
            runner.Raise("Go");

            Assert.That(RecordingBehaviour.Log, Is.EqualTo(new[]
            {
                "Entered Entry", "Exited Entry",
                "Title.Enter", "Entered Title",
                "Title.Exit", "Exited Title",
                "Entered Event", "Exited Event", "Entered Play",
            }));
        }

        [Test]
        public void SeveralBehaviours_RunInListOrder()
        {
            _title.AddBehaviour(new RecordingBehaviour { Tag = "A" });
            _title.AddBehaviour(new RecordingBehaviour { Tag = "B" });
            var runner = new GraphRunner(_graph);

            runner.Start();
            runner.Update(1f);
            runner.Stop();

            Assert.That(RecordingBehaviour.Log, Is.EqualTo(new[]
            {
                "A.Enter", "B.Enter", "A.Update(1)", "B.Update(1)", "A.Exit", "B.Exit",
            }));
        }

        [Test]
        public void Update_DoesNothingWhenStoppedOrOnANodeWithoutBehaviours()
        {
            _title.AddBehaviour(new RecordingBehaviour { Tag = "Title" });
            var runner = new GraphRunner(_graph);

            runner.Update(1f);
            runner.Start();
            runner.Raise("Go");
            RecordingBehaviour.Log.Clear();
            runner.Update(1f);
            runner.FixedUpdate(1f);
            runner.Stop();
            runner.Update(1f);

            Assert.That(RecordingBehaviour.Log, Is.Empty);
        }

        [Test]
        public void SceneNodes_CanHaveBehavioursToo()
        {
            var scene = Add(new SceneNode { Title = "Stage" });
            scene.AddBehaviour(new RecordingBehaviour { Tag = "Stage" });
            _graph.RemoveEdge(_graph.Edges.Single(e => e.FromNodeId == _entry.Id));
            Connect(_entry, scene);
            var runner = new GraphRunner(_graph);

            runner.Start();
            runner.Update(1f);

            Assert.That(RecordingBehaviour.Log, Is.EqualTo(new[] { "Stage.Enter", "Stage.Update(1)" }));
        }

        // ---- Runner ごとの複製 ----

        [Test]
        public void EachRunAndEachRunner_GetsFreshCopiesOfTheAssetValues()
        {
            var template = new RecordingBehaviour { Tag = "FromAsset" };
            _title.AddBehaviour(template);
            var first = new GraphRunner(_graph);
            var second = new GraphRunner(_graph);
            first.Start();
            second.Start();

            var copy = (RecordingBehaviour)first.GetBehaviours(_title).Single();
            Assert.That(copy, Is.Not.SameAs(template));
            Assert.That(copy.Tag, Is.EqualTo("FromAsset"), "field values come from the asset");
            Assert.That(second.GetBehaviours(_title).Single(), Is.Not.SameAs(copy), "runners do not share state");

            copy.Tag = "ChangedAtRuntime";
            Assert.That(template.Tag, Is.EqualTo("FromAsset"), "runtime changes are not written back to the asset");

            first.Stop();
            first.Start();
            var restarted = (RecordingBehaviour)first.GetBehaviours(_title).Single();
            Assert.That(restarted, Is.Not.SameAs(copy));
            Assert.That(restarted.Tag, Is.EqualTo("FromAsset"), "every Start begins from the asset values again");
        }

        [Test]
        public void Behaviours_KnowTheirRunnerNodeAndHost()
        {
            _title.AddBehaviour(new RecordingBehaviour());
            var gameObject = new GameObject("Runner");
            try
            {
                var host = gameObject.AddComponent<GraphRunnerBehaviour>();
                var runner = new GraphRunner(_graph) { Host = host };
                runner.Start();

                var behaviour = runner.GetBehaviours(_title).Single();

                Assert.That(behaviour.Runner, Is.SameAs(runner));
                Assert.That(behaviour.Node, Is.SameAs(_title));
                Assert.That(behaviour.Host, Is.SameAs(host));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void GetBehaviours_IsEmptyForNodesWithoutBehaviours()
        {
            var runner = new GraphRunner(_graph);

            Assert.That(runner.GetBehaviours(_entry), Is.Empty);
            Assert.That(runner.GetBehaviours(_play), Is.Empty);
            Assert.That(runner.GetBehaviours(null), Is.Empty);
        }

        // ---- 振る舞いの中からの操作 ----

        [Test]
        public void RaiseInsideOnUpdate_MovesOnAndSkipsTheRestOfTheOldNode()
        {
            _title.AddBehaviour(new ControlBehaviour { RaiseOnUpdate = "Go" });
            _title.AddBehaviour(new RecordingBehaviour { Tag = "Title" });
            _play.AddBehaviour(new RecordingBehaviour { Tag = "Play" });
            var runner = new GraphRunner(_graph);
            runner.Start();
            RecordingBehaviour.Log.Clear();

            runner.Update(1f);

            Assert.That(runner.Current, Is.SameAs(_play));
            Assert.That(RecordingBehaviour.Log, Is.EqualTo(new[] { "Title.Exit", "Play.Enter" }),
                "Title's second behaviour is not updated after the node was left");
        }

        [Test]
        public void RaiseBackToTheSameNodeInsideOnUpdate_AlsoStopsTheTick()
        {
            // Title ─Retry→ Title（自分へ戻る遷移）
            var retry = Add(new EventNode { EventName = "Retry" });
            Connect(_title, retry);
            Connect(retry, _title);
            _title.AddBehaviour(new ControlBehaviour { RaiseOnUpdate = "Retry" });
            _title.AddBehaviour(new RecordingBehaviour { Tag = "Title" });
            var runner = new GraphRunner(_graph);
            runner.Start();
            RecordingBehaviour.Log.Clear();

            runner.Update(1f);

            Assert.That(runner.Current, Is.SameAs(_title));
            Assert.That(RecordingBehaviour.Log, Is.EqualTo(new[] { "Title.Exit", "Title.Enter" }),
                "the re-entered behaviours are updated from the next Update, not right after their OnEnter");
        }

        [Test]
        public void StopInsideOnExit_ExitsEachBehaviourOnceAndDoesNotEnterTheNextNode()
        {
            _title.AddBehaviour(new ControlBehaviour { StopOnExit = true });
            _title.AddBehaviour(new RecordingBehaviour { Tag = "Title" });
            _play.AddBehaviour(new RecordingBehaviour { Tag = "Play" });
            var runner = new GraphRunner(_graph);
            runner.Start();

            runner.Raise("Go");

            Assert.That(runner.IsRunning, Is.False);
            Assert.That(RecordingBehaviour.Log, Is.EqualTo(new[] { "Title.Enter", "Title.Exit" }));
        }

        [Test]
        public void StopInsideOnEnter_DoesNotEnterOrExitTheBehavioursAfterIt()
        {
            _title.AddBehaviour(new ControlBehaviour { StopOnEnter = true });
            _title.AddBehaviour(new RecordingBehaviour { Tag = "Title" });
            var runner = new GraphRunner(_graph);
            var entered = new List<string>();
            runner.NodeEntered += n => entered.Add(n.Title);

            runner.Start();

            Assert.That(runner.IsRunning, Is.False);
            Assert.That(RecordingBehaviour.Log, Is.Empty);
            Assert.That(entered, Is.EqualTo(new[] { "Entry" }), "NodeEntered is not raised for the node that stopped the runner");
        }

        [Test]
        public void ExceptionInABehaviour_IsReported_AndTheOthersStillRun()
        {
            var errors = new List<Exception>();
            GraphRunner.BehaviourExceptionHandler = errors.Add;
            _title.AddBehaviour(new ThrowingBehaviour());
            _title.AddBehaviour(new RecordingBehaviour { Tag = "Title" });
            var runner = new GraphRunner(_graph);
            runner.Start();

            runner.Update(1f);

            Assert.That(errors.Single(), Is.TypeOf<InvalidOperationException>());
            Assert.That(RecordingBehaviour.Log, Has.Member("Title.Update(1)"));
            Assert.That(runner.IsRunning, Is.True);
        }

        // ---- 読めない振る舞い・リストの操作 ----

        [Test]
        public void MissingBehaviour_IsSkippedAtRuntimeAndWarnedAbout()
        {
            _title.AddBehaviour(new RecordingBehaviour { Tag = "Title" });
            // 型が削除・改名されると、SerializeReference のリストの要素は null になる
            BehaviourList(_title).Insert(0, null);
            var runner = new GraphRunner(_graph);

            runner.Start();
            runner.Update(1f);

            Assert.That(runner.GetBehaviours(_title), Has.Count.EqualTo(1));
            Assert.That(RecordingBehaviour.Log, Is.EqualTo(new[] { "Title.Enter", "Title.Update(1)" }));
            var issue = GraphValidator.Validate(_graph).Single(i => i.NodeId == _title.Id);
            Assert.That(issue.Severity, Is.EqualTo(GraphIssueSeverity.Warning));
            Assert.That(issue.Message, Does.Contain("could not be loaded"));
        }

        [Test]
        public void HostList_AddRemoveAndMove()
        {
            var a = new RecordingBehaviour { Tag = "A" };
            var b = new RecordingBehaviour { Tag = "B" };
            _title.AddBehaviour(a);
            _title.AddBehaviour(b);

            Assert.Throws<ArgumentNullException>(() => _title.AddBehaviour(null));
            Assert.That(_title.MoveBehaviour(1, 0), Is.True);
            Assert.That(_title.Behaviours, Is.EqualTo(new NodeBehaviour[] { b, a }));
            Assert.That(_title.MoveBehaviour(0, 99), Is.True, "clamped to the end");
            Assert.That(_title.Behaviours, Is.EqualTo(new NodeBehaviour[] { a, b }));
            Assert.That(_title.RemoveBehaviourAt(5), Is.False);
            Assert.That(_title.RemoveBehaviourAt(0), Is.True);
            Assert.That(_title.Behaviours, Is.EqualTo(new NodeBehaviour[] { b }));
        }

        [Test]
        public void Behaviours_SurviveTheAssetsSerialization()
        {
            _title.AddBehaviour(new RecordingBehaviour { Tag = "Saved" });

            var copy = ScriptableObject.CreateInstance<NodeGraphAsset>();
            try
            {
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(_graph), copy);

                var title = (StateNode)copy.FindNode(_title.Id);
                Assert.That(((RecordingBehaviour)title.Behaviours.Single()).Tag, Is.EqualTo("Saved"));
            }
            finally
            {
                Object.DestroyImmediate(copy);
            }
        }

        private T Add<T>(T node) where T : NodeData
        {
            _graph.AddNode(node);
            return node;
        }

        private void Connect(NodeData from, NodeData to) => _graph.AddEdge(new EdgeData(from.Id, "out", to.Id, "in"));

        private static List<NodeBehaviour> BehaviourList(StateNode node) =>
            (List<NodeBehaviour>)typeof(StateNode)
                .GetField("_behaviours", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(node);
    }
}
