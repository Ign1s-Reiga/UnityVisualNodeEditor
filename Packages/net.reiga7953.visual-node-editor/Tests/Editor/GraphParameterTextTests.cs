using NUnit.Framework;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>
    /// パラメータの値をテキストに出す（<see cref="GraphParameterText"/>）。TextMesh は uGUI の Text と同じく public な string の text を持つ（名前で探す）。
    /// </summary>
    public sealed class GraphParameterTextTests
    {
        private GameComponentTestGraph _game;
        private GraphRunner _runner;
        private GameObject _gameObject;
        private TextMesh _text;
        private GraphParameterText _parameterText;

        [SetUp]
        public void SetUp()
        {
            _game = new GameComponentTestGraph();
            _runner = new GraphRunner(_game.Graph);
            _gameObject = new GameObject("Score");
            _text = _gameObject.AddComponent<TextMesh>();
            _text.text = "Score: -";
            _parameterText = _gameObject.AddComponent<GraphParameterText>();
            _parameterText.Graph = _game.Graph;
            _parameterText.Parameter = "Score";
            _parameterText.Format = "Score: {0}";
        }

        [TearDown]
        public void TearDown()
        {
            _parameterText.Disable();
            _runner.Stop();
            Object.DestroyImmediate(_gameObject);
            Object.DestroyImmediate(_game.Graph);
        }

        [Test]
        public void TheText_FollowsTheParameter()
        {
            _parameterText.Enable();
            Assert.That(_text.text, Is.EqualTo("Score: -"), "no runner yet: the text stays as it was placed");

            _runner.Start();
            Assert.That(_text.text, Is.EqualTo("Score: 0"));

            _runner.SetInt("Score", 12);
            Assert.That(_text.text, Is.EqualTo("Score: 12"));

            _runner.SetFloat("Speed", 3f);
            Assert.That(_text.text, Is.EqualTo("Score: 12"), "other parameters do not change it");
        }

        [Test]
        public void WithoutAGraph_AnyRunningGraphIsUsed()
        {
            _parameterText.Graph = null;
            _parameterText.Parameter = "Speed";
            _parameterText.Format = "{0:0.00}";
            _runner.Start();

            _parameterText.Enable();

            Assert.That(_text.text, Is.EqualTo("1.50"));
        }

        [Test]
        public void ChangingTheSettings_WhileEnabled_RewritesTheText()
        {
            _runner.Start();
            _parameterText.Enable();

            _parameterText.Format = "{0} pts";

            Assert.That(_text.text, Is.EqualTo("0 pts"));
        }

        [Test]
        public void AMissingParameter_LeavesTheText()
        {
            _parameterText.Parameter = "Lives";
            _runner.Start();

            _parameterText.Enable();

            Assert.That(_text.text, Is.EqualTo("Score: -"));
        }

        [Test]
        public void Formats_UseTheInvariantCulture_AndABadFormatShowsTheValue()
        {
            Assert.That(GraphParameterText.TryFormat("{0:0.0}", 1.25f, out var text), Is.True);
            Assert.That(text, Is.EqualTo("1.3").Or.EqualTo("1.2"), "a dot, whatever the system language");
            Assert.That(GraphParameterText.TryFormat("", 7, out text), Is.True);
            Assert.That(text, Is.EqualTo("7"));
            Assert.That(GraphParameterText.TryFormat("Score: {1}", 7, out text), Is.False);
            Assert.That(text, Is.EqualTo("7"));
        }

        [Test]
        public void TheText_IsFoundByName()
        {
            Assert.That(GraphParameterText.FindTextTarget(_gameObject), Is.SameAs(_text));
            var empty = new GameObject("Empty");
            try
            {
                Assert.That(GraphParameterText.FindTextTarget(empty), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(empty);
            }
        }
    }
}
