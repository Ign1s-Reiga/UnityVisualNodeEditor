using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>
    /// Scene ノードの Reload: 同じシーンにいるまま入り直したら読み込み直す（やり直し）。始めたとき・ほかのシーンから来たときは、ふつうに読み込む。
    /// Entry → Stage（Scene）─(自動)→ Playing ─Died→ Stage、Playing ─Next→ Boss（Scene）
    /// </summary>
    public sealed class SceneReloadTests
    {
        private NodeGraphAsset _graph;
        private SceneNode _stage;
        private RecordingLoader _loader;
        private GraphRunner _runner;

        [SetUp]
        public void SetUp()
        {
            _graph = ScriptableObject.CreateInstance<NodeGraphAsset>();
            var entry = Add(new EntryNode());
            _stage = Add(new SceneNode { Title = "Stage", Scene = new SceneReference("stage-guid", "Assets/Stage.unity"), Reload = true });
            var playing = Add(new StateNode { Title = "Playing" });
            var boss = Add(new SceneNode { Title = "Boss", Scene = new SceneReference("boss-guid", "Assets/Boss.unity") });
            var automatic = Add(new EventNode { Trigger = EventTriggerMode.Automatic });
            Connect(entry, _stage);
            Connect(_stage, automatic);
            Connect(automatic, playing);
            Connect(playing, "Died", _stage);
            Connect(playing, "Next", boss);
            _loader = new RecordingLoader();
            _runner = new GraphRunner(_graph, _loader);
        }

        [TearDown]
        public void TearDown()
        {
            _runner.Stop();
            Object.DestroyImmediate(_graph);
        }

        [Test]
        public void ComingBackFromTheSameScene_ReloadsIt()
        {
            _runner.Start();
            Assert.That(_loader.Calls, Is.EqualTo(new[] { "load Assets/Stage.unity" }), "starting does not reload the scene Play was pressed in");
            Assert.That(_runner.Current.Title, Is.EqualTo("Playing"));

            _runner.Raise("Died");

            Assert.That(_loader.Calls, Is.EqualTo(new[] { "load Assets/Stage.unity", "reload Assets/Stage.unity" }));
            Assert.That(_runner.Current.Title, Is.EqualTo("Playing"));
        }

        [Test]
        public void WithoutReload_ComingBackOnlyLoads()
        {
            _stage.Reload = false;
            _runner.Start();
            _runner.Raise("Died");

            Assert.That(_loader.Calls, Is.EqualTo(new[] { "load Assets/Stage.unity", "load Assets/Stage.unity" }), "the loader skips the active scene");
        }

        [Test]
        public void ALoaderWithoutReload_StillLoads()
        {
            // ReloadScene は既定の実装で LoadScene を呼ぶので、前からある読み込み方もそのまま動く
            var loads = new List<string>();
            _runner = new GraphRunner(_graph, new OldLoader(loads));
            _runner.Start();
            _runner.Raise("Died");

            Assert.That(loads, Is.EqualTo(new[] { "Assets/Stage.unity", "Assets/Stage.unity" }));
        }

        [Test]
        public void SameScene_ComparesGuidsThenPaths()
        {
            var stage = new SceneReference("g", "Assets/Stage.unity");
            Assert.That(stage.IsSameSceneAs(new SceneReference("g", "Assets/Moved/Stage.unity")), Is.True, "the same GUID after a move");
            Assert.That(stage.IsSameSceneAs(new SceneReference("other", "Assets/Stage.unity")), Is.False);
            Assert.That(new SceneReference(null, "Assets/Stage.unity").IsSameSceneAs(stage), Is.True, "no GUID: the path");
            Assert.That(stage.IsSameSceneAs(new SceneReference()), Is.False);
            Assert.That(stage.IsSameSceneAs(null), Is.False);
        }

        [Test]
        public void TheSceneManagerLoader_ReloadsUnlessItIsAlreadyLoadingIt()
        {
            var stage = new SceneReference("g", "Assets/Stage.unity");
            Assert.That(SceneManagerSceneLoader.ShouldReload(stage, null), Is.True, "even when it is the active scene");
            Assert.That(SceneManagerSceneLoader.ShouldReload(stage, "Assets/Stage.unity"), Is.False);
            Assert.That(SceneManagerSceneLoader.ShouldReload(stage, "Assets/Boss.unity"), Is.True);
            Assert.That(SceneManagerSceneLoader.ShouldReload(new SceneReference(), null), Is.False);
        }

        private T Add<T>(T node) where T : NodeData
        {
            _graph.AddNode(node);
            return node;
        }

        private void Connect(NodeData from, NodeData to) => _graph.AddEdge(new EdgeData(from.Id, "out", to.Id, "in"));

        private void Connect(NodeData from, string eventName, NodeData to)
        {
            var raised = Add(new EventNode { EventName = eventName });
            Connect(from, raised);
            Connect(raised, to);
        }

        private sealed class RecordingLoader : ISceneLoader
        {
            public List<string> Calls { get; } = new List<string>();

            public void LoadScene(SceneReference scene) => Calls.Add("load " + scene.Path);

            public void ReloadScene(SceneReference scene) => Calls.Add("reload " + scene.Path);
        }

        // ReloadScene を持たない、前からある読み込み方
        private sealed class OldLoader : ISceneLoader
        {
            private readonly List<string> _loads;

            public OldLoader(List<string> loads) => _loads = loads;

            public void LoadScene(SceneReference scene) => _loads.Add(scene.Path);
        }
    }
}
