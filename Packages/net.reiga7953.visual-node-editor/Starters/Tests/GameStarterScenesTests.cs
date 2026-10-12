using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Reiga.VisualNodeEditor.Starters.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Reiga.VisualNodeEditor.Starters.Tests
{
    /// <summary>
    /// ゲームの Starter のシーン: Title（Start のボタン・キー、Graph Runner）と Game（盤、パラメータの表示、ノードにいる間だけ出すパネル、キー）。
    /// 開いているシーンに触らないよう、プレビュー用のシーンに組み立てて確かめる。
    /// </summary>
    public sealed class GameStarterScenesTests
    {
        private NodeGraphAsset _graph;
        private Scene _scene;

        [SetUp]
        public void SetUp() => _scene = EditorSceneManager.NewPreviewScene();

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.ClosePreviewScene(_scene);
            Object.DestroyImmediate(_graph);
        }

        [TestCase(typeof(SnakeStarter))]
        [TestCase(typeof(SlidingPuzzleStarter))]
        public void Title_StartsTheGame_ByButtonOrKey_AndHasTheGraphRunner(System.Type starterType)
        {
            var starter = Starter(starterType);
            _graph = starter.BuildGraph();
            _graph.name = starter.BaseName;

            StarterScenes.BuildGameTitle(_scene, _graph, starter.Controls, GameStarter.StartEvent);

            Assert.That(Find<GraphRunnerBehaviour>().Single().Graph, Is.SameAs(_graph));
            Assert.That(Find<EventSystem>().Single().GetComponent<BaseInputModule>(), Is.Not.Null, "the button can be clicked");
            var start = Find<GraphEventButton>().Single();
            Assert.That(start.EventName, Is.EqualTo(GameStarter.StartEvent));
            Assert.That(start.GetComponent<Button>(), Is.Not.Null);
            Assert.That(Find<GraphEventKey>().Select(key => key.Key), Is.EquivalentTo(new[] { KeyCode.Return, KeyCode.Space }));
            Assert.That(Find<GraphEventKey>().All(key => key.EventName == GameStarter.StartEvent), Is.True);
            Assert.That(Find<Text>().Any(text => text.text == starter.BaseName), Is.True, "the heading is the graph's name");
        }

        [Test]
        public void SnakeGame_HasTheBoard_ScoreTexts_PanelsAndKeys()
        {
            _graph = SnakeStarter.Instance.BuildGraph();

            var board = SnakeStarter.Instance.BuildGame(_scene, _graph);

            Assert.That(StarterScenes.GetPath(board.transform), Is.EqualTo("Canvas/Board"));
            AssertTexts(new Dictionary<string, string> { { "Score", "Score: {0}" }, { "HighScore", "Best: {0}" } });
            AssertPanels(new[] { "Ready", "Paused", "Game Over" });
            AssertKeys(new Dictionary<KeyCode, string> { { KeyCode.Space, "Go" }, { KeyCode.Escape, "Pause" }, { KeyCode.Return, "Retry" } });
            AssertEventsExist();
        }

        [Test]
        public void PuzzleGame_HasTheBoard_LevelAndMoves_PanelsAndKeys()
        {
            _graph = SlidingPuzzleStarter.Instance.BuildGraph();

            var board = SlidingPuzzleStarter.Instance.BuildGame(_scene, _graph);

            Assert.That(StarterScenes.GetPath(board.transform), Is.EqualTo("Canvas/Board"));
            AssertTexts(new Dictionary<string, string> { { "Level", "Level {0}" }, { "Moves", "Moves: {0}" } });
            AssertPanels(new[] { "Paused", "Solved", "All Clear" });
            AssertKeys(new Dictionary<KeyCode, string> { { KeyCode.Escape, "Pause" }, { KeyCode.Return, "Next" } });
            AssertEventsExist();
        }

        // パラメータの表示: 名前ごとに書式と、シーンに置いた文字（既定値で書いたもの）
        private void AssertTexts(Dictionary<string, string> formats)
        {
            var texts = Find<GraphParameterText>();
            foreach (var pair in formats)
            {
                var text = texts.First(t => t.Parameter == pair.Key && t.Format == pair.Value);
                Assert.That(text.Graph, Is.SameAs(_graph));
                Assert.That(GraphParameterText.FindTextTarget(text.gameObject), Is.InstanceOf<Text>());
                GraphParameterText.TryFormat(pair.Value, _graph.FindParameterByName(pair.Key).DefaultValue, out var initial);
                Assert.That(text.GetComponent<Text>().text, Is.EqualTo(initial));
            }

            Assert.That(texts.All(t => _graph.FindParameterByName(t.Parameter) != null), Is.True, "every text shows a parameter of the graph");
        }

        // パネル: ノードごとに 1 つ。いつも有効な Canvas の Graph Node Activator が出し、エディタでは隠れている
        private void AssertPanels(string[] nodeTitles)
        {
            var activators = Find<GraphNodeActivator>();
            Assert.That(activators.Select(activator => _graph.FindNode(activator.NodeId)?.Title), Is.EquivalentTo(nodeTitles));
            foreach (var activator in activators)
            {
                Assert.That(activator.Graph, Is.SameAs(_graph));
                Assert.That(activator.name, Is.EqualTo("Canvas"));
                var panel = activator.Targets.Single();
                Assert.That(GraphNodeActivator.CanTarget(activator.gameObject, panel), Is.True);
                Assert.That(panel.activeSelf, Is.False, "hidden until the graph is on its node");
            }
        }

        private void AssertKeys(Dictionary<KeyCode, string> keys)
        {
            var eventKeys = Find<GraphEventKey>();
            Assert.That(eventKeys.ToDictionary(key => key.Key, key => key.EventName), Is.EquivalentTo(keys));
            Assert.That(eventKeys.All(key => key.Graph == _graph), Is.True);
        }

        // ボタンとキーが起こすイベントは、どれもグラフにある
        private void AssertEventsExist()
        {
            var events = GraphEventNames.CollectRaisable(_graph);
            Assert.That(Find<GraphEventButton>().Select(button => button.EventName), Is.SubsetOf(events));
            Assert.That(Find<GraphEventKey>().Select(key => key.EventName), Is.SubsetOf(events));
            Assert.That(Find<GraphEventButton>().All(button => button.Graph == _graph && button.GetComponent<Button>() != null), Is.True);
        }

        private static GameStarter Starter(System.Type type) =>
            type == typeof(SnakeStarter) ? (GameStarter)SnakeStarter.Instance : SlidingPuzzleStarter.Instance;

        private List<T> Find<T>() where T : Component =>
            _scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToList();
    }
}
