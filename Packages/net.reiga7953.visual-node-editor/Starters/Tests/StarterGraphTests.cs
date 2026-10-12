using System.Linq;
using NUnit.Framework;
using Reiga.VisualNodeEditor.Starters.Editor;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Starters.Tests
{
    /// <summary>ゲームの Starter のグラフ: 問題が無く、ゲームのスクリプトとボタン・キーが起こすイベントで、思ったとおりに進む。</summary>
    public sealed class StarterGraphTests
    {
        private NodeGraphAsset _graph;
        private GraphRunner _runner;
        private float _timeScale;

        [SetUp]
        public void SetUp() => _timeScale = Time.timeScale;

        [TearDown]
        public void TearDown()
        {
            _runner?.Stop();
            Object.DestroyImmediate(_graph);
            Time.timeScale = _timeScale;
        }

        [Test]
        public void SnakeGraph_HasNoProblems()
        {
            _graph = SnakeStarter.Instance.BuildGraph();

            // シーンはシーンを作るときに入れる（ここではまだ空なので、その注意だけが出る）
            Assert.That(GraphValidator.Validate(_graph).Where(issue => issue.Kind != GraphIssueKind.SceneNotSet).Select(issue => issue.Message), Is.Empty);
            Assert.That(_graph.Nodes.OfType<SceneNode>().Select(node => node.Title), Is.EquivalentTo(new[] { "Title", "Game" }));
        }

        [Test]
        public void SnakeGraph_PlaysARound_PausesCrashesAndRetries()
        {
            _graph = SnakeStarter.Instance.BuildGraph();
            _runner = new GraphRunner(_graph);
            _runner.Start();
            Assert.That(Current, Is.EqualTo("Title"));

            _runner.Raise("Start");
            Assert.That(Current, Is.EqualTo("Ready"), "the Game scene goes straight into a round");
            Assert.That(Path, Is.EqualTo("Round"));

            _runner.SetInt("Score", 7);
            Assert.That(_runner.Raise("Go"), Is.True);
            Assert.That(Current, Is.EqualTo("Playing"));

            Assert.That(_runner.Raise("Pause"), Is.True);
            Assert.That(Current, Is.EqualTo("Paused"));
            Assert.That(Time.timeScale, Is.EqualTo(0f));
            Assert.That(_runner.Raise("Pause"), Is.True);
            Assert.That(Current, Is.EqualTo("Playing"));
            Assert.That(Time.timeScale, Is.EqualTo(_timeScale));

            Assert.That(_runner.Raise("Crash"), Is.True);
            Assert.That(Current, Is.EqualTo("Game Over"));
            Assert.That(_runner.GetInt("Score"), Is.EqualTo(7), "the game over panel shows the score");

            Assert.That(_runner.Raise("Retry"), Is.True);
            Assert.That(Current, Is.EqualTo("Ready"));
            Assert.That(_runner.GetInt("Score"), Is.EqualTo(0), "a new round starts from 0");

            _runner.Raise("Go");
            _runner.Raise("Crash");
            Assert.That(_runner.Raise("Title"), Is.True);
            Assert.That(Current, Is.EqualTo("Title"));
        }

        [Test]
        public void SnakeGraph_QuitFromPause_GoesToTheTitle()
        {
            _graph = SnakeStarter.Instance.BuildGraph();
            _runner = new GraphRunner(_graph);
            _runner.Start();
            _runner.Raise("Start");
            _runner.Raise("Go");
            _runner.Raise("Pause");

            Assert.That(_runner.Raise("Quit"), Is.True);

            Assert.That(Current, Is.EqualTo("Title"));
            Assert.That(Time.timeScale, Is.EqualTo(_timeScale), "leaving Paused gives the time scale back");
        }

        [Test]
        public void PuzzleGraph_HasNoProblems_AndNestsTwoContainers()
        {
            _graph = SlidingPuzzleStarter.Instance.BuildGraph();

            // シーンはシーンを作るときに入れる（ここではまだ空なので、その注意だけが出る）
            Assert.That(GraphValidator.Validate(_graph).Where(issue => issue.Kind != GraphIssueKind.SceneNotSet).Select(issue => issue.Message), Is.Empty);
            var playing = _graph.Nodes.Single(node => node.Title == "Playing");
            Assert.That(new GraphQuery(_graph).GetContainerPath(playing).Select(container => container.Title), Is.EqualTo(new[] { "Levels", "Level" }));
        }

        [Test]
        public void PuzzleGraph_GoesThroughTheLevels_ToAllClear()
        {
            _graph = SlidingPuzzleStarter.Instance.BuildGraph();
            _runner = new GraphRunner(_graph);
            _runner.Start();
            _runner.Raise("Start");
            Assert.That(Current, Is.EqualTo("Playing"), "Shuffle passes straight into Playing");
            Assert.That(Path, Is.EqualTo("Levels › Level"));
            Assert.That(_runner.GetInt("Level"), Is.EqualTo(1));

            for (var level = 1; level < SlidingPuzzleStarter.LastLevel; level++)
            {
                _runner.SetInt("Moves", 12);
                Assert.That(_runner.Raise("Solved"), Is.True);
                Assert.That(Current, Is.EqualTo("Solved"));
                Assert.That(_runner.Raise("Next"), Is.True);
                Assert.That(Current, Is.EqualTo("Playing"), $"level {level + 1}");
                Assert.That(_runner.GetInt("Level"), Is.EqualTo(level + 1));
                Assert.That(_runner.GetInt("Moves"), Is.EqualTo(0), "each board starts at 0 moves");
            }

            _runner.Raise("Solved");
            Assert.That(_runner.Raise("Next"), Is.True);
            Assert.That(Current, Is.EqualTo("All Clear"));
            Assert.That(Path, Is.Empty);

            Assert.That(_runner.Raise("Title"), Is.True);
            Assert.That(Current, Is.EqualTo("Title"));
        }

        [Test]
        public void PuzzleGraph_RetryReshuffles_AndANewGameStartsAtLevelOne()
        {
            _graph = SlidingPuzzleStarter.Instance.BuildGraph();
            _runner = new GraphRunner(_graph);
            _runner.Start();
            _runner.Raise("Start");
            _runner.Raise("Solved");
            _runner.Raise("Next");
            _runner.SetInt("Moves", 5);

            _runner.Raise("Pause");
            Assert.That(Time.timeScale, Is.EqualTo(0f));
            var entered = new System.Collections.Generic.List<string>();
            _runner.NodeEntered += node => entered.Add(node.Title);
            Assert.That(_runner.Raise("Retry"), Is.True);
            Assert.That(entered, Does.Contain("Shuffle"));
            Assert.That(Current, Is.EqualTo("Playing"));
            Assert.That(_runner.GetInt("Moves"), Is.EqualTo(0));
            Assert.That(_runner.GetInt("Level"), Is.EqualTo(2), "retrying keeps the level");
            Assert.That(Time.timeScale, Is.EqualTo(_timeScale));

            _runner.Raise("Pause");
            _runner.Raise("Quit");
            Assert.That(Current, Is.EqualTo("Title"));
            _runner.Raise("Start");
            Assert.That(_runner.GetInt("Level"), Is.EqualTo(1), "the Game scene node starts again from level 1");
        }

        private string Current => _runner.Current?.Title;

        private string Path => string.Join(" › ", _runner.ContainerPath.Select(container => container.Title));
    }
}
