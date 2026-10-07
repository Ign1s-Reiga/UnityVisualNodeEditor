using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Tests
{
    public sealed class GraphRunnerTests
    {
        private NodeGraphAsset _graph;
        private FakeSceneLoader _loader;
        private GraphRunner _runner;

        [SetUp]
        public void SetUp()
        {
            _graph = ScriptableObject.CreateInstance<NodeGraphAsset>();
            _graph.name = "TestGraph";
            _loader = new FakeSceneLoader();
        }

        [TearDown]
        public void TearDown()
        {
            _runner?.Stop();
            Object.DestroyImmediate(_graph);
        }

        [Test]
        public void Start_PassesEntryAndStopsAtFirstScene()
        {
            var entry = Add(new EntryNode());
            var title = AddScene("Title");
            Connect(entry, title);

            var entered = new List<NodeData>();
            _runner = new GraphRunner(_graph, _loader);
            _runner.NodeEntered += entered.Add;
            _runner.Start();

            Assert.That(_runner.Current, Is.SameAs(title));
            Assert.That(entered, Is.EqualTo(new NodeData[] { entry, title }));
            Assert.That(_loader.Loaded, Is.EqualTo(new[] { "Assets/Title.unity" }));
        }

        [Test]
        public void Start_WaitsAtEntryWhenItOnlyLeadsToEvents()
        {
            var entry = Add(new EntryNode());
            var begin = Add(new EventNode { EventName = "Begin" });
            var title = AddScene("Title");
            Connect(entry, begin);
            Connect(begin, title);

            _runner = new GraphRunner(_graph, _loader);
            _runner.Start();

            Assert.That(_runner.Current, Is.SameAs(entry));
            Assert.That(_runner.Raise("Begin"), Is.True);
            Assert.That(_runner.Current, Is.SameAs(title));
        }

        [Test]
        public void Raise_FollowsMatchingEventOnly()
        {
            var entry = Add(new EntryNode());
            var title = AddScene("Title");
            var start = Add(new EventNode { EventName = "StartGame" });
            var quit = Add(new EventNode { EventName = "Quit" });
            var game = AddScene("Game");
            var credits = AddScene("Credits");
            Connect(entry, title);
            Connect(title, start);
            Connect(title, quit);
            Connect(start, game);
            Connect(quit, credits);

            _runner = new GraphRunner(_graph, _loader);
            _runner.Start();

            Assert.That(_runner.Raise("Unknown"), Is.False);
            Assert.That(_runner.Current, Is.SameAs(title));
            Assert.That(_runner.Raise("StartGame"), Is.True);
            Assert.That(_runner.Current, Is.SameAs(game));
            Assert.That(_loader.Loaded.Last(), Is.EqualTo("Assets/Game.unity"));
            Assert.That(_runner.Raise("Quit"), Is.False, "Quit is only reachable from Title");
        }

        [Test]
        public void Raise_OnEventWithoutOutgoingEdge_KeepsCurrentNode()
        {
            var entry = Add(new EntryNode());
            var play = Add(new StateNode());
            var coin = Add(new EventNode { EventName = "CoinPicked" });
            Connect(entry, play);
            Connect(play, coin);

            _runner = new GraphRunner(_graph, _loader);
            _runner.Start();

            Assert.That(_runner.Raise("CoinPicked"), Is.True);
            Assert.That(_runner.Current, Is.SameAs(play));
        }

        [Test]
        public void Advance_FollowsFirstNonEventEdge()
        {
            var entry = Add(new EntryNode());
            var splash = AddScene("Splash");
            var skip = Add(new EventNode { EventName = "Skip" });
            var title = AddScene("Title");
            Connect(entry, splash);
            Connect(splash, skip);
            Connect(splash, title);

            _runner = new GraphRunner(_graph, _loader);
            _runner.Start();

            Assert.That(_runner.Advance(), Is.True);
            Assert.That(_runner.Current, Is.SameAs(title));
            Assert.That(_runner.Advance(), Is.False, "Title has no outgoing edge");
        }

        [Test]
        public void Stop_ExitsCurrentNodeAndUnregisters()
        {
            var entry = Add(new EntryNode());
            var state = Add(new StateNode());
            Connect(entry, state);

            _runner = new GraphRunner(_graph, _loader);
            var exited = new List<NodeData>();
            _runner.NodeExited += exited.Add;
            _runner.Start();
            Assert.That(GraphRunner.Running, Has.Member(_runner));

            _runner.Stop();

            Assert.That(_runner.IsRunning, Is.False);
            Assert.That(_runner.Current, Is.Null);
            Assert.That(exited.Last(), Is.SameAs(state));
            Assert.That(GraphRunner.Running, Has.No.Member(_runner));
            Assert.That(_runner.Raise("Anything"), Is.False);
        }

        [Test]
        public void StartedAndStopped_AreReported()
        {
            Add(new EntryNode());
            _runner = new GraphRunner(_graph, _loader);
            var started = new List<GraphRunner>();
            var stopped = new List<GraphRunner>();
            void OnStarted(GraphRunner r) => started.Add(r);
            void OnStopped(GraphRunner r) => stopped.Add(r);
            GraphRunner.Started += OnStarted;
            GraphRunner.Stopped += OnStopped;
            try
            {
                _runner.Start();
                _runner.Stop();
            }
            finally
            {
                GraphRunner.Started -= OnStarted;
                GraphRunner.Stopped -= OnStopped;
            }

            Assert.That(started, Is.EqualTo(new[] { _runner }));
            Assert.That(stopped, Is.EqualTo(new[] { _runner }));
        }

        [Test]
        public void RaiseDuringNodeEntered_RunsAfterCurrentMove()
        {
            var entry = Add(new EntryNode());
            var loading = Add(new StateNode());
            var done = Add(new EventNode { EventName = "Loaded" });
            var title = AddScene("Title");
            Connect(entry, loading);
            Connect(loading, done);
            Connect(done, title);

            _runner = new GraphRunner(_graph, _loader);
            _runner.NodeEntered += node =>
            {
                if (node == loading)
                {
                    _runner.Raise("Loaded");
                }
            };
            _runner.Start();

            Assert.That(_runner.Current, Is.SameAs(title));
        }

        [Test]
        public void EventLoop_StopsWithWarningInsteadOfHanging()
        {
            var entry = Add(new EntryNode());
            var state = Add(new StateNode());
            var a = Add(new EventNode { EventName = "A" });
            var b = Add(new EventNode { EventName = "B" });
            Connect(entry, state);
            Connect(state, a);
            Connect(a, b);
            Connect(b, a);

            _runner = new GraphRunner(_graph, _loader);
            _runner.Start();

            // 無限ループせずに戻ってきて、輪の中のノードで止まっていること（警告がログに出る）
            Assert.That(_runner.Raise("A"), Is.True);
            Assert.That(_runner.Current, Is.SameAs(a).Or.SameAs(b));
        }

        [Test]
        public void StopInNodeEntered_DoesNotLoadTheScene()
        {
            var entry = Add(new EntryNode());
            var title = AddScene("Title");
            Connect(entry, title);

            _runner = new GraphRunner(_graph, _loader);
            _runner.NodeEntered += node =>
            {
                if (node == title)
                {
                    _runner.Stop();
                }
            };
            _runner.Start();

            Assert.That(_runner.IsRunning, Is.False);
            Assert.That(_runner.Current, Is.Null);
            Assert.That(_loader.Loaded, Is.Empty);
        }

        [Test]
        public void StopInNodeEntered_OnEvent_DoesNotTriggerItOrContinue()
        {
            var entry = Add(new EntryNode());
            var play = Add(new StateNode());
            var win = Add(new EventNode { EventName = "Win" });
            var result = AddScene("Result");
            Connect(entry, play);
            Connect(play, win);
            Connect(win, result);

            _runner = new GraphRunner(_graph, _loader);
            var triggered = new List<string>();
            _runner.EventTriggered += triggered.Add;
            _runner.NodeEntered += node =>
            {
                if (node == win)
                {
                    _runner.Stop();
                }
            };
            _runner.Start();

            _runner.Raise("Win");

            Assert.That(triggered, Is.Empty);
            Assert.That(_loader.Loaded, Is.Empty);
            Assert.That(_runner.IsRunning, Is.False);
        }

        [Test]
        public void RestartInHandler_AbandonsOldMoveAndStartsFresh()
        {
            var entry = Add(new EntryNode());
            var a = Add(new StateNode { Title = "A" });
            var go = Add(new EventNode { EventName = "Go" });
            var b = AddScene("B");
            Connect(entry, a);
            Connect(a, go);
            Connect(go, b);

            _runner = new GraphRunner(_graph, _loader);
            var entered = new List<string>();
            var restarted = false;
            _runner.NodeEntered += node =>
            {
                entered.Add(node.Title);
                if (node == go && !restarted)
                {
                    restarted = true;
                    _runner.Stop();
                    _runner.Start();
                }
            };
            _runner.Start();

            _runner.Raise("Go");

            // 再開後は Entry → A で止まり、古い遷移（Go → B）は続かない
            Assert.That(entered, Is.EqualTo(new[] { "Entry", "A", "Event", "Entry", "A" }));
            Assert.That(_runner.Current, Is.SameAs(a));
            Assert.That(_loader.Loaded, Is.Empty);

            // 遷移中の扱いが壊れていないこと: 次の Raise はすぐに実行される
            Assert.That(_runner.Raise("Go"), Is.True);
            Assert.That(_runner.Current, Is.SameAs(b));
        }

        [Test]
        public void Start_WithoutEntry_Throws()
        {
            Add(new StateNode());
            _runner = new GraphRunner(_graph, _loader);

            Assert.Throws<System.InvalidOperationException>(() => _runner.Start());
            Assert.That(_runner.IsRunning, Is.False);
        }

        [Test]
        public void StepRequestedInNodeExitedDuringStop_IsNotRunAfterRestart()
        {
            var entry = Add(new EntryNode());
            var a = Add(new StateNode { Title = "A" });
            var b = Add(new StateNode { Title = "B" });
            Connect(entry, a);
            Connect(a, b);

            _runner = new GraphRunner(_graph, _loader);
            var stopOnA = true;
            bool? advancedDuringStop = null;
            _runner.NodeEntered += node =>
            {
                if (node == a && stopOnA)
                {
                    stopOnA = false;
                    _runner.Stop();
                }
            };
            _runner.NodeExited += node =>
            {
                if (node == a && advancedDuringStop == null)
                {
                    advancedDuringStop = _runner.Advance();
                }
            };
            _runner.Start();
            Assert.That(advancedDuringStop, Is.False, "a stopping runner must refuse instead of queueing");

            _runner.Start();

            Assert.That(_runner.Current, Is.SameAs(a), "no stale Advance after restart");
        }

        [Test]
        public void EventChainEndingInDeadEndEvent_NotifiesAndStaysOnWaitNode()
        {
            var entry = Add(new EntryNode());
            var a = Add(new StateNode { Title = "A" });
            var x = Add(new EventNode { EventName = "X" });
            var y = Add(new EventNode { EventName = "Y" });
            var b = Add(new StateNode { Title = "B" });
            Connect(entry, a);
            Connect(a, x);
            Connect(x, y);
            Connect(a, b);

            _runner = new GraphRunner(_graph, _loader);
            var triggered = new List<string>();
            var entered = new List<NodeData>();
            _runner.EventTriggered += triggered.Add;
            _runner.Start();
            _runner.NodeEntered += entered.Add;

            Assert.That(_runner.Raise("X"), Is.True);

            Assert.That(_runner.Current, Is.SameAs(a), "must not get stuck on an Event node");
            Assert.That(entered, Is.Empty);
            Assert.That(triggered, Is.EqualTo(new[] { "X", "Y" }));
            Assert.That(_runner.Advance(), Is.True, "the graph can still move on");
            Assert.That(_runner.Current, Is.SameAs(b));
        }

        [Test]
        public void ExceptionInHandler_DropsQueuedMoves()
        {
            var entry = Add(new EntryNode());
            var a = Add(new StateNode { Title = "A" });
            var b = Add(new StateNode { Title = "B" });
            var c = Add(new StateNode { Title = "C" });
            var ping = Add(new EventNode { EventName = "Ping" });
            Connect(entry, a);
            Connect(a, b);
            Connect(b, c);
            Connect(b, ping);

            _runner = new GraphRunner(_graph, _loader);
            var throwOnB = true;
            _runner.NodeEntered += node =>
            {
                if (node == b && throwOnB)
                {
                    throwOnB = false;
                    _runner.Advance();
                    throw new System.InvalidOperationException("handler failed");
                }
            };
            _runner.Start();

            Assert.Throws<System.InvalidOperationException>(() => _runner.Advance());
            Assert.That(_runner.Current, Is.SameAs(b));

            // 捨てられた Advance が、無関係な Raise のついでに実行されないこと
            Assert.That(_runner.Raise("Ping"), Is.True);
            Assert.That(_runner.Current, Is.SameAs(b));
        }

        [TestCase(null, "Assets/A.unity", "Assets/A.unity", false)]
        [TestCase(null, "Assets/A.unity", "Assets/B.unity", true)]
        [TestCase("Assets/B.unity", "Assets/A.unity", "Assets/A.unity", true)]
        [TestCase("Assets/A.unity", "Assets/A.unity", "Assets/B.unity", false)]
        public void SceneLoader_ComparesWithLoadingSceneFirst(string loadingKey, string requested, string active, bool expected)
        {
            // 3 行目: B を読み込み中に A へ戻る（アクティブはまだ A）→ A を読み込む必要がある
            // 4 行目: A を読み込み中にもう一度 A → 二重に読み込まない
            var scene = new SceneReference("g", requested);

            Assert.That(SceneManagerSceneLoader.ShouldLoad(scene, loadingKey, active, "x"), Is.EqualTo(expected));
        }

        [Test]
        public void SceneLoader_KeyAndSameSceneDetection()
        {
            var withPath = new SceneReference("g", "Assets/Scenes/Title.unity");
            var nameOnly = new SceneReference(null, null);

            Assert.That(SceneManagerSceneLoader.GetLoadKey(withPath), Is.EqualTo("Assets/Scenes/Title.unity"));
            Assert.That(SceneManagerSceneLoader.GetLoadKey(nameOnly), Is.Empty);
            Assert.That(SceneManagerSceneLoader.IsSameScene(withPath, "Assets/Scenes/Title.unity", "Title"), Is.True);
            Assert.That(SceneManagerSceneLoader.IsSameScene(withPath, "Assets/Other/Title.unity", "Title"), Is.False,
                "same file name in another folder is a different scene");
        }

        private T Add<T>(T node) where T : NodeData
        {
            _graph.AddNode(node);
            return node;
        }

        private SceneNode AddScene(string name) =>
            Add(new SceneNode { Title = name, Scene = new SceneReference("guid-" + name, $"Assets/{name}.unity") });

        private void Connect(NodeData from, NodeData to) => _graph.AddEdge(new EdgeData(from.Id, "out", to.Id, "in"));

        private sealed class FakeSceneLoader : ISceneLoader
        {
            public List<string> Loaded { get; } = new List<string>();

            public void LoadScene(SceneReference scene) => Loaded.Add(scene.Path);
        }
    }
}
