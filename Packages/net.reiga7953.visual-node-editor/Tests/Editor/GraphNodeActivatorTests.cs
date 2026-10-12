using NUnit.Framework;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>グラフが指定したノードにいる間だけ GameObject を表示する（<see cref="GraphNodeActivator"/>）。</summary>
    public sealed class GraphNodeActivatorTests
    {
        private GameComponentTestGraph _game;
        private GraphRunner _runner;
        private GameObject _canvas;
        private GameObject _panel;
        private GraphNodeActivator _activator;

        [SetUp]
        public void SetUp()
        {
            _game = new GameComponentTestGraph();
            _runner = new GraphRunner(_game.Graph);
            _canvas = new GameObject("Canvas");
            _panel = new GameObject("Paused Panel");
            _panel.transform.SetParent(_canvas.transform);
            _activator = _canvas.AddComponent<GraphNodeActivator>();
            _activator.Graph = _game.Graph;
            _activator.Targets.Add(_panel);
        }

        [TearDown]
        public void TearDown()
        {
            _activator.Disable();
            _runner.Stop();
            Object.DestroyImmediate(_canvas);
            Object.DestroyImmediate(_game.Graph);
        }

        [Test]
        public void Targets_AreShownOnlyWhileTheGraphIsOnTheNode()
        {
            _activator.NodeId = _game.Paused.Id;
            _activator.Enable();
            Assert.That(_panel.activeSelf, Is.False, "no runner yet");

            // シーンの物が有効になった後で Runner が始まる
            _runner.Start();
            Assert.That(_panel.activeSelf, Is.False, "on Playing");

            _runner.Raise("Pause");
            Assert.That(_panel.activeSelf, Is.True, "on Paused");
            Assert.That(_activator.IsInNode, Is.True);

            _runner.Raise("Pause");
            Assert.That(_panel.activeSelf, Is.False, "back on Playing");

            _runner.Raise("Pause");
            _runner.Stop();
            Assert.That(_panel.activeSelf, Is.False, "the runner stopped");
        }

        [Test]
        public void AContainer_ShowsTheTargetsAnywhereInsideIt()
        {
            _activator.NodeId = _game.Round.Id;
            _runner.Start();
            _activator.Enable();
            Assert.That(_panel.activeSelf, Is.True, "on Playing, inside Round");

            _runner.Raise("Pause");
            Assert.That(_panel.activeSelf, Is.True, "on Paused, inside Round");

            _runner.Raise("Pause");
            _runner.Raise("Crash");
            Assert.That(_runner.Current, Is.SameAs(_game.GameOver));
            Assert.That(_panel.activeSelf, Is.False, "left Round");
        }

        [Test]
        public void ChangingTheNode_WhileEnabled_ChecksAgain()
        {
            _runner.Start();
            _activator.NodeId = _game.GameOver.Id;
            _activator.Enable();
            Assert.That(_panel.activeSelf, Is.False);

            _activator.NodeId = _game.Playing.Id;
            Assert.That(_panel.activeSelf, Is.True);
        }

        [Test]
        public void WhileDisabled_TheTargetsAreLeftAlone()
        {
            _activator.NodeId = _game.Paused.Id;
            _runner.Start();
            _activator.Enable();
            _activator.Disable();

            _runner.Raise("Pause");

            Assert.That(_panel.activeSelf, Is.False, "it stopped following the graph");
        }

        [Test]
        public void ItselfAndItsParents_AreNeverHidden()
        {
            // 隠すと自分も無効になり、二度と表示できない
            var root = new GameObject("Root");
            _canvas.transform.SetParent(root.transform);
            _activator.Targets.Add(_canvas);
            _activator.Targets.Add(root);
            _activator.Targets.Add(null);
            _activator.NodeId = _game.Paused.Id;
            _runner.Start();

            _activator.Enable();

            Assert.That(_canvas.activeSelf && root.activeSelf, Is.True);
            Assert.That(_panel.activeSelf, Is.False);
            Assert.That(GraphNodeActivator.CanTarget(_canvas, _panel), Is.True);
            Assert.That(GraphNodeActivator.CanTarget(_canvas, _canvas), Is.False);
            Assert.That(GraphNodeActivator.CanTarget(_canvas, root), Is.False);
            _canvas.transform.SetParent(null);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void AnotherGraphsRunner_IsIgnored()
        {
            var other = new GameComponentTestGraph();
            var otherRunner = new GraphRunner(other.Graph);
            try
            {
                _activator.NodeId = _game.Playing.Id;
                _activator.Enable();
                otherRunner.Start();

                Assert.That(_panel.activeSelf, Is.False);
            }
            finally
            {
                otherRunner.Stop();
                Object.DestroyImmediate(other.Graph);
            }
        }

        [Test]
        public void WithoutAGraph_NothingIsShown()
        {
            _activator.Graph = null;
            _activator.NodeId = _game.Playing.Id;
            _panel.SetActive(true);
            _runner.Start();

            _activator.Enable();

            Assert.That(_panel.activeSelf, Is.False, "the node is in no graph, so the graph is never on it");
        }
    }
}
