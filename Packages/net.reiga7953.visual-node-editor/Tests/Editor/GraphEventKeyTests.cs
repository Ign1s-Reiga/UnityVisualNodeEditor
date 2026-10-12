using NUnit.Framework;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>キーでイベントを送る（<see cref="GraphEventKey"/>）と、キー入力の窓口（<see cref="GraphInput"/>）。</summary>
    public sealed class GraphEventKeyTests
    {
        private GameComponentTestGraph _game;
        private GraphRunner _runner;
        private GameObject _gameObject;
        private GraphEventKey _key;
        private KeyCode _pressed;

        [SetUp]
        public void SetUp()
        {
            _game = new GameComponentTestGraph();
            _runner = new GraphRunner(_game.Graph);
            _gameObject = new GameObject("Keys");
            _key = _gameObject.AddComponent<GraphEventKey>();
            _key.Graph = _game.Graph;
            _key.Key = KeyCode.Escape;
            _key.EventName = "Pause";
            _pressed = KeyCode.None;
            GraphInput.KeyDownSource = key => key == _pressed;
        }

        [TearDown]
        public void TearDown()
        {
            GraphInput.KeyDownSource = null;
            _runner.Stop();
            Object.DestroyImmediate(_gameObject);
            Object.DestroyImmediate(_game.Graph);
        }

        [Test]
        public void PressingTheKey_RaisesTheEvent()
        {
            _runner.Start();

            Assert.That(_key.CheckKey(), Is.False, "not pressed");
            Assert.That(_runner.Current, Is.SameAs(_game.Playing));

            _pressed = KeyCode.Escape;
            Assert.That(_key.CheckKey(), Is.True);
            Assert.That(_runner.Current, Is.SameAs(_game.Paused));

            Assert.That(_key.CheckKey(), Is.True, "the same key toggles back");
            Assert.That(_runner.Current, Is.SameAs(_game.Playing));
        }

        [Test]
        public void AKeyThatLeadsNowhereHere_DoesNothing()
        {
            // 同じキーをノードごとに別の意味で使えるよう、遷移が無くても何も言わない
            _key.EventName = "Retry";
            _runner.Start();
            _pressed = KeyCode.Escape;

            Assert.That(_key.CheckKey(), Is.False);
            Assert.That(_runner.Current, Is.SameAs(_game.Playing));
        }

        [Test]
        public void Advance_MovesToTheNextNodeWithoutAnEvent()
        {
            var graph = ScriptableObject.CreateInstance<NodeGraphAsset>();
            var entry = new EntryNode();
            var first = new StateNode { Title = "First" };
            var second = new StateNode { Title = "Second" };
            foreach (var node in new NodeData[] { entry, first, second })
            {
                graph.AddNode(node);
            }

            graph.AddEdge(new EdgeData(entry.Id, "out", first.Id, "in"));
            graph.AddEdge(new EdgeData(first.Id, "out", second.Id, "in"));
            var runner = new GraphRunner(graph);
            try
            {
                runner.Start();
                _key.Graph = graph;
                _key.Action = GraphEventKeyAction.Advance;
                _pressed = KeyCode.Escape;

                Assert.That(_key.CheckKey(), Is.True);
                Assert.That(runner.Current, Is.SameAs(second));
            }
            finally
            {
                runner.Stop();
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void WithoutARunner_NothingHappens()
        {
            _pressed = KeyCode.Escape;

            Assert.That(_key.CheckKey(), Is.False);
            Assert.That(_key.TrySend(), Is.False);
        }

        [Test]
        public void GraphInput_UsesTheSource_AndNeverReportsNone()
        {
            _pressed = KeyCode.Space;

            Assert.That(GraphInput.GetKeyDown(KeyCode.Space), Is.True);
            Assert.That(GraphInput.GetKeyDown(KeyCode.Escape), Is.False);
            GraphInput.KeyDownSource = _ => true;
            Assert.That(GraphInput.GetKeyDown(KeyCode.None), Is.False);
        }
    }
}
