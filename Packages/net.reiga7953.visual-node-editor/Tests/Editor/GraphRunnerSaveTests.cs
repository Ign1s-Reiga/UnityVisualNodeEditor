using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>Runner のセーブ（<see cref="GraphRunner.CaptureState"/>）と再開（<see cref="GraphRunner.RestoreState"/>）。</summary>
    public sealed class GraphRunnerSaveTests
    {
        private NodeGraphAsset _graph;
        private FakeLoader _loader;
        private readonly List<GraphRunner> _runners = new();
        private EntryNode _entry;
        private SceneNode _story;
        private StateNode _chapter;
        private StateNode _ending;

        [SetUp]
        public void SetUp()
        {
            _graph = ScriptableObject.CreateInstance<NodeGraphAsset>();
            _graph.name = "SaveGraph";
            _graph.AddParameter(new GraphParameter("Affection", GraphParameterType.Int));
            _graph.AddParameter(new GraphParameter("Route", GraphParameterType.String));
            _loader = new FakeLoader();

            // Entry → Story（シーン）─(Go)→ Chapter ─(End)→ Ending
            _entry = Add(new EntryNode());
            _story = Add(new SceneNode { Title = "Story", Scene = new SceneReference("guid-story", "Assets/Story.unity") });
            _chapter = Add(new StateNode { Title = "Chapter" });
            _ending = Add(new StateNode { Title = "Ending" });
            Connect(_entry, _story);
            Connect(_story, Event("Go"), _chapter);
            Connect(_chapter, Event("End"), _ending);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var runner in _runners)
            {
                runner.Stop();
            }

            Object.DestroyImmediate(_graph);
        }

        [Test]
        public void Capture_RecordsNodeSceneParametersAndExtras()
        {
            var runner = Started();
            runner.Raise("Go");
            runner.SetInt("Affection", 4);
            runner.Extras.SetFloat("$Yarn.Internal.Visiting.Chapter1", 2f);

            var state = runner.CaptureState();

            Assert.That(state.NodeId, Is.EqualTo(_chapter.Id));
            Assert.That(state.GraphName, Is.EqualTo("SaveGraph"));
            Assert.That(state.Scene.Path, Is.EqualTo("Assets/Story.unity"), "the scene the flow is in, although Chapter has none");
            Assert.That(state.Parameters.Single(p => p.Key == "Affection").Value, Is.EqualTo(4));
            Assert.That(state.Extras.Single().Value, Is.EqualTo(2f));
        }

        [Test]
        public void Capture_NeedsARunningGraph()
        {
            Assert.Throws<InvalidOperationException>(() => New().CaptureState());
        }

        [Test]
        public void Json_RoundTrips()
        {
            var runner = Started();
            runner.Raise("Go");
            runner.SetInt("Affection", 7);
            runner.SetString("Route", "Alice");
            runner.Extras.SetBool("Seen", true);

            var copy = GraphRunnerState.FromJson(runner.CaptureState().ToJson());

            Assert.That(copy.NodeId, Is.EqualTo(_chapter.Id));
            Assert.That(copy.Scene.Guid, Is.EqualTo("guid-story"));
            Assert.That(copy.Parameters.Select(p => p.Value), Is.EquivalentTo(new object[] { 7, "Alice" }));
            Assert.That(copy.Extras.Single().Value, Is.EqualTo(true));
            Assert.That(copy.Version, Is.EqualTo(GraphRunnerState.CurrentVersion));
        }

        [TestCase("")]
        [TestCase("{}")]
        [TestCase("not json")]
        public void Json_RefusesSomethingThatIsNotASave(string json)
        {
            Assert.Throws<ArgumentException>(() => GraphRunnerState.FromJson(json));
        }

        [Test]
        public void Restore_StartsAStoppedRunnerAtTheSavedNode()
        {
            var saved = SavedAtChapter(affection: 5);
            var runner = New();
            var started = 0;
            var entered = new List<NodeData>();
            GraphRunner.Started += OnStarted;
            runner.NodeEntered += entered.Add;
            try
            {
                Assert.That(runner.RestoreState(saved), Is.True);
            }
            finally
            {
                GraphRunner.Started -= OnStarted;
            }

            Assert.That(runner.IsRunning, Is.True);
            Assert.That(started, Is.EqualTo(1));
            Assert.That(runner.Current, Is.SameAs(_chapter));
            Assert.That(entered, Is.EqualTo(new NodeData[] { _chapter }), "enters only the saved node, not the way there");
            Assert.That(runner.GetInt("Affection"), Is.EqualTo(5));
            Assert.That(_loader.Loaded, Is.EqualTo(new[] { "Assets/Story.unity" }), "goes back to the scene of the save");

            void OnStarted(GraphRunner r)
            {
                if (r == runner)
                {
                    started++;
                }
            }
        }

        [Test]
        public void Restore_LeavesTheCurrentNodeFirst()
        {
            var saved = SavedAtChapter(affection: 1);
            var runner = Started();
            runner.SetInt("Affection", 9);
            var exited = new List<NodeData>();
            runner.NodeExited += exited.Add;

            runner.RestoreState(saved);

            Assert.That(exited, Is.EqualTo(new NodeData[] { _story }));
            Assert.That(runner.Current, Is.SameAs(_chapter));
            Assert.That(runner.GetInt("Affection"), Is.EqualTo(1));
        }

        [Test]
        public void Restore_ReplacesExtras()
        {
            var source = Started();
            source.Extras.SetInt("Kept", 3);
            var saved = source.CaptureState();
            var runner = Started();
            runner.Extras.SetInt("Dropped", 1);

            runner.RestoreState(saved);

            Assert.That(runner.Extras.Keys, Is.EqualTo(new[] { "Kept" }));
        }

        [Test]
        public void Restore_OfARemovedNode_ChangesNothing()
        {
            var saved = SavedAtChapter(affection: 5);
            _graph.RemoveNode(_chapter);
            var runner = Started();
            runner.SetInt("Affection", 2);

            Assert.That(runner.RestoreState(saved), Is.False);
            Assert.That(runner.Current, Is.SameAs(_story));
            Assert.That(runner.GetInt("Affection"), Is.EqualTo(2));
        }

        [Test]
        public void Restore_KeepsDefaultsForParametersThatChangedSinceTheSave()
        {
            var saved = SavedAtChapter(affection: 5);
            _graph.RemoveParameter(_graph.FindParameterByName("Affection"));
            _graph.AddParameter(new GraphParameter("Affection", GraphParameterType.Float) { FloatValue = 0.5f });

            var runner = New();
            runner.RestoreState(saved);

            Assert.That(runner.GetFloat("Affection"), Is.EqualTo(0.5f), "the saved Int does not fit the Float parameter");
        }

        [Test]
        public void Restore_ChecksAutomaticEvents()
        {
            var saved = SavedAtChapter(affection: 5);
            var auto = Add(new EventNode { Trigger = EventTriggerMode.Automatic });
            auto.AddCondition(new TransitionCondition(_graph.FindParameterByName("Affection").Id, ConditionOperator.GreaterOrEqual) { IntValue = 5 });
            Connect(_chapter, auto, _ending);
            var runner = New();

            runner.RestoreState(saved);

            Assert.That(runner.Current, Is.SameAs(_ending));
        }

        [Test]
        public void Start_ClearsExtrasAndTheLastScene()
        {
            var runner = Started();
            runner.Extras.SetString("Note", "x");
            runner.Stop();

            runner.Start();

            Assert.That(runner.Extras.Count, Is.EqualTo(0));
            Assert.That(runner.LastScene.Path, Is.EqualTo("Assets/Story.unity"), "set again by entering Story");
        }

        // ---- ロードするとやり直すノード ----

        [Test]
        public void RestartNode_IsSavedWithTheValuesFromBeforeItWasEntered()
        {
            // 会話の中で増やした値は、やり直すときにまた増えるので、入る前の値で保存する
            var dialogue = Add(new RestartTestNode { Title = "Dialogue" });
            Connect(_story, Event("Talk"), dialogue);
            var runner = Started();
            runner.SetInt("Affection", 1);
            runner.Raise("Talk");
            runner.SetInt("Affection", 2);
            runner.Extras.SetInt("Visited", 1);

            var state = runner.CaptureState();

            Assert.That(state.NodeId, Is.EqualTo(dialogue.Id));
            Assert.That(state.Parameters.Single(p => p.Key == "Affection").Value, Is.EqualTo(1));
            Assert.That(state.Extras, Is.Empty);
        }

        [Test]
        public void OtherNodes_AreSavedWithTheirCurrentValues()
        {
            var runner = Started();
            runner.Raise("Go");
            runner.SetInt("Affection", 3);

            Assert.That(runner.CaptureState().Parameters.Single(p => p.Key == "Affection").Value, Is.EqualTo(3));
        }

        // ---- 組み立て ----

        private GraphRunnerState SavedAtChapter(int affection)
        {
            var runner = Started();
            runner.Raise("Go");
            runner.SetInt("Affection", affection);
            var state = runner.CaptureState();
            runner.Stop();
            _loader.Loaded.Clear();
            return state;
        }

        private GraphRunner New()
        {
            var runner = new GraphRunner(_graph, _loader);
            _runners.Add(runner);
            return runner;
        }

        private GraphRunner Started()
        {
            var runner = New();
            runner.Start();
            return runner;
        }

        private EventNode Event(string name) => Add(new EventNode { EventName = name });

        private T Add<T>(T node) where T : NodeData
        {
            _graph.AddNode(node);
            return node;
        }

        private void Connect(NodeData from, NodeData to) => _graph.AddEdge(new EdgeData(from.Id, "out", to.Id, "in"));

        private void Connect(NodeData from, EventNode via, NodeData to)
        {
            Connect(from, via);
            Connect(via, to);
        }

        private sealed class FakeLoader : ISceneLoader
        {
            public List<string> Loaded { get; } = new();

            public void LoadScene(SceneReference scene) => Loaded.Add(scene.Path);
        }
    }
}
