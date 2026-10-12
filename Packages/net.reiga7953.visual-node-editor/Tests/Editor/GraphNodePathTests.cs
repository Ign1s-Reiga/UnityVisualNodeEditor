using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor.Inspector;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>ノードの場所（<see cref="GraphNodePath"/>。UI Toolkit の Graph Node Panel が使う）。</summary>
    public sealed class GraphNodePathTests
    {
        private GameComponentTestGraph _game;
        private GraphRunner _runner;

        [SetUp]
        public void SetUp()
        {
            _game = new GameComponentTestGraph();
            _runner = new GraphRunner(_game.Graph);
        }

        [TearDown]
        public void TearDown()
        {
            _runner.Stop();
            Object.DestroyImmediate(_game.Graph);
        }

        [Test]
        public void APath_MatchesTheNodeOrAContainerAroundIt()
        {
            _runner.Start();

            Assert.That(GraphNodePath.IsAt(_runner, "Playing"), Is.True, "the node's title alone");
            Assert.That(GraphNodePath.IsAt(_runner, "Round/Playing"), Is.True, "with its container");
            Assert.That(GraphNodePath.IsAt(_runner, "Round"), Is.True, "anywhere inside a container");
            Assert.That(GraphNodePath.IsAt(_runner, " Round / Playing "), Is.True, "spaces around parts are ignored");
            Assert.That(GraphNodePath.IsAt(_runner, "Paused"), Is.False);
            Assert.That(GraphNodePath.IsAt(_runner, "Playing/Round"), Is.False, "the order matters");
            Assert.That(GraphNodePath.IsAt(_runner, "playing"), Is.False, "titles are compared exactly");
            Assert.That(GraphNodePath.IsAt(_runner, string.Empty), Is.False);

            _runner.Raise("Pause");
            _runner.Raise("Pause");
            _runner.Raise("Crash");
            Assert.That(GraphNodePath.IsAt(_runner, "Game Over"), Is.True);
            Assert.That(GraphNodePath.IsAt(_runner, "Round"), Is.False);

            _runner.Stop();
            Assert.That(GraphNodePath.IsAt(_runner, "Game Over"), Is.False, "a stopped runner is nowhere");
            Assert.That(GraphNodePath.IsAt(null, "Game Over"), Is.False);
        }

        [Test]
        public void Paths_AreBuiltFromTheContainersOutward()
        {
            var query = new GraphQuery(_game.Graph);

            Assert.That(GraphNodePath.Of(query, _game.Paused), Is.EqualTo("Round/Paused"));
            Assert.That(GraphNodePath.Of(query, _game.GameOver), Is.EqualTo("Game Over"));
            Assert.That(GraphNodePath.Split("A/ B //C"), Is.EqualTo(new[] { "A", "B", "C" }));
        }

        [Test]
        public void Names_FallBackToTheSceneName()
        {
            var scene = new SceneNode { Scene = new SceneReference("guid", "Assets/Levels/Forest.unity") };
            Assert.That(GraphNodePath.NameOf(scene), Is.EqualTo("Forest"));
            scene.Title = " Woods ";
            Assert.That(GraphNodePath.NameOf(scene), Is.EqualTo("Woods"), "a title wins, trimmed");
        }

        [Test]
        public void TheNodeDrawer_OffersWaitNodesAndContainers()
        {
            Assert.That(GraphNodePathDrawer.GetChoices(_game.Graph), Is.EqualTo(new[] { "Game Over", "Round", "Round/Paused", "Round/Playing" }));
            Assert.That(GraphParameterNameDrawer.GetChoices(_game.Graph), Is.EqualTo(new[] { "Score", "Speed" }));
            Assert.That(GraphNodePathDrawer.GetChoices(null), Is.Empty);
        }
    }
}
