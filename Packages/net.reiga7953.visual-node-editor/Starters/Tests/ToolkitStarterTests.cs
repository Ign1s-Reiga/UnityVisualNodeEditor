using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor.Inspector;
using Reiga.VisualNodeEditor.Starters.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Starters.Tests
{
    /// <summary>
    /// ゲームの Starter の UI Toolkit 版: 画面（UXML・USS・Panel Settings）をグラフのノードに入れることと、それを出す 1 つのシーン
    /// （Graph Runner に UIDocument と Graph Screen Host）。画面のコントロールがグラフのイベント・パラメータを指していること。
    /// スクリプトは書かない（テスト中にコンパイルが走るので）。
    /// </summary>
    public sealed class ToolkitStarterTests
    {
        private const string Folder = "Assets/__VneToolkitStarterTemp";

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(Folder); // 前に止めたテストの残り
            AssetDatabase.CreateFolder("Assets", "__VneToolkitStarterTemp");
            PendingStarterComponents.Clear();

            // 無題のシーンが開いていると、Unity は追加でシーンを作れないので、保存したシーンを開いておく
            var host = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(host, Folder + "/Host.unity");
        }

        [TearDown]
        public void TearDown()
        {
            PendingStarterComponents.Clear();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(Folder);
        }

        [Test]
        public void Snake_PutsTheScreensOnTheNodes_AndMakesOneScene()
        {
            var graph = SnakeToolkitStarter.Instance.CreateIn(Folder, withScenes: true, writeScript: false);

            foreach (var file in new[] { "Snake.uss", "Snake Panel Settings.asset", "Runtime Theme.tss", "Snake.unity" })
            {
                Assert.That(File.Exists($"{Folder}/{file}"), Is.True, file);
            }

            Assert.That(File.Exists($"{Folder}/Snake Title.unity") || File.Exists($"{Folder}/Snake Game.unity"), Is.False, "screens are not scenes");
            Assert.That(GraphValidator.Validate(graph), Is.Empty);
            Assert.That(SceneManager.sceneCount, Is.EqualTo(1), "the new scene is closed again");
            Assert.That(ScreensByNode(graph), Is.EquivalentTo(new Dictionary<string, string>
            {
                { "Title", "Snake Title" }, { "Round", "Snake Game" }, { "Ready", "Snake Ready" }, { "Paused", "Snake Paused" }, { "Game Over", "Snake Game Over" },
            }));

            var title = Instantiate(graph, "Title");
            Assert.That(title.Q<GraphButton>("start").EventName, Is.EqualTo(GameStarter.StartEvent));
            Assert.That(title.Q<Label>("heading").text, Is.EqualTo("Snake"));
            var game = Instantiate(graph, "Round");
            Assert.That(game.Q("board"), Is.Not.Null, "the game script draws in it");
            Assert.That(game.Query<GraphParameterLabel>().ToList().Select(label => label.Parameter), Is.EquivalentTo(new[] { "Score", "HighScore" }));
            Assert.That(Instantiate(graph, "Ready").Q("screen").pickingMode, Is.EqualTo(PickingMode.Ignore), "the hint does not cover the board");
            AssertScreensPointIntoTheGraph(graph);

            WithScene(graph, scene =>
            {
                Assert.That(Keys(scene), Is.EquivalentTo(new[] { "Return: Start", "Space: Go", "Escape: Pause", "Return: Retry" }));
            });
        }

        [Test]
        public void Puzzle_PutsItsScreensOnTheNodes()
        {
            var graph = SlidingPuzzleToolkitStarter.Instance.CreateIn(Folder, withScenes: true, writeScript: false);

            Assert.That(GraphValidator.Validate(graph), Is.Empty);
            Assert.That(ScreensByNode(graph), Is.EquivalentTo(new Dictionary<string, string>
            {
                { "Title", "Sliding Puzzle Title" }, { "Levels", "Sliding Puzzle Game" }, { "Paused", "Sliding Puzzle Paused" },
                { "Solved", "Sliding Puzzle Solved" }, { "All Clear", "Sliding Puzzle All Clear" },
            }));
            Assert.That(Instantiate(graph, "Levels").Q("board"), Is.Not.Null);
            Assert.That(Instantiate(graph, "Solved").Q<GraphButton>("next").EventName, Is.EqualTo(SlidingPuzzleStarter.NextEvent));
            AssertScreensPointIntoTheGraph(graph);

            WithScene(graph, scene =>
            {
                Assert.That(Keys(scene), Is.EquivalentTo(new[] { "Return: Start", "Escape: Pause", "Return: Next" }));
            });
        }

        [Test]
        public void TheGameScript_GoesOnTheGraphRunner()
        {
            var graph = SnakeToolkitStarter.Instance.CreateIn(Folder, withScenes: true, writeScript: false);
            var scenePath = graph.Nodes.OfType<SceneNode>().Single(node => node.Title == ToolkitGameStarter.MainSceneNode).Scene.Path;
            PendingStarterComponents.Add(new PendingStarterComponent(scenePath, $"Graph Runner ({graph.name})", "Reiga.VisualNodeEditor.GraphRunnerBehaviour",
                AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(graph))));

            Assert.That(PendingStarterComponents.CompleteAll(), Is.EqualTo(1), "the runner with the UI Document is there to add it to");
        }

        [Test]
        public void AScreenFileInTheWay_IsNotOverwritten_AndNothingHalfMadeIsLeft()
        {
            File.WriteAllText($"{Folder}/Snake Game.uxml", "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\" />");
            AssetDatabase.ImportAsset($"{Folder}/Snake Game.uxml");
            LogAssert.Expect(LogType.Exception, new Regex("already exists"));
            LogAssert.Expect(LogType.Error, new Regex("could not create the game's scene"));

            var graph = SnakeToolkitStarter.Instance.CreateIn(Folder, withScenes: true, writeScript: false);

            Assert.That(File.ReadAllText($"{Folder}/Snake Game.uxml"), Does.Not.Contain("board"), "the file in the way is kept as it was");
            var left = AssetDatabase.FindAssets(string.Empty, new[] { Folder }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path, System.StringComparer.Ordinal);
            Assert.That(left, Is.EqualTo(new[] { $"{Folder}/Host.unity", $"{Folder}/Snake Game.uxml", $"{Folder}/Snake.asset" }));
            Assert.That(graph.Nodes.OfType<SceneNode>().All(node => string.IsNullOrEmpty(node.Scene.Path)), Is.True);
            Assert.That(graph.Nodes.OfType<IHasScreen>().All(node => node.Screen == null), Is.True, "no node points at a deleted screen");
        }

        [Test]
        public void WithoutScenes_TheScreensAreStillMadeAndPutOnTheNodes()
        {
            // シーンを作れないとき（無題のシーン・Play 中）も、画面は作ってノードに入れる。自分のシーンの Graph Runner に Graph Screen Host を足せば動く
            var graph = SlidingPuzzleToolkitStarter.Instance.CreateIn(Folder, withScenes: false, writeScript: false);

            Assert.That(File.Exists($"{Folder}/Sliding Puzzle.unity"), Is.False);
            Assert.That(graph.Nodes.OfType<SceneNode>().All(node => string.IsNullOrEmpty(node.Scene.Path)), Is.True);
            Assert.That(ScreensByNode(graph)["Levels"], Is.EqualTo("Sliding Puzzle Game"));
            Assert.That(EditorUtility.IsDirty(graph), Is.False, "the graph is saved with its screens");
            Assert.That(Instantiate(graph, "Levels").Q("board"), Is.Not.Null);
        }

        // 画面を持つノードのタイトル → 画面の名前
        private static Dictionary<string, string> ScreensByNode(NodeGraphAsset graph) =>
            graph.Nodes.Where(node => node is IHasScreen { Screen: { } }).ToDictionary(node => node.Title, node => ((IHasScreen)node).Screen.name);

        private static VisualElement Instantiate(NodeGraphAsset graph, string nodeTitle) =>
            ((IHasScreen)graph.Nodes.Single(node => node.Title == nodeTitle && node is IHasScreen)).Screen.Instantiate();

        // ボタンのイベントとラベルのパラメータは、どれもグラフにある。画面の出し分けはノードの画面でするので、Graph Node Panel は使わない
        private static void AssertScreensPointIntoTheGraph(NodeGraphAsset graph)
        {
            foreach (var node in graph.Nodes.OfType<IHasScreen>().Where(node => node.Screen != null))
            {
                var screen = node.Screen.Instantiate();
                Assert.That(screen.Query<GraphButton>().ToList().Select(button => button.EventName).Distinct(), Is.SubsetOf(GraphEventNames.CollectRaisable(graph)), node.Screen.name);
                Assert.That(screen.Query<GraphParameterLabel>().ToList().Select(label => label.Parameter).Distinct(), Is.SubsetOf(GraphParameterNameDrawer.GetChoices(graph)), node.Screen.name);
                Assert.That(screen.Query<GraphNodePanel>().ToList(), Is.Empty, node.Screen.name);

                // 同じキーを Graph Event Key で送るので、ボタンはフォーカスを取らない（フォーカスしたボタンが Enter / Space でもう一度送らないように）
                Assert.That(screen.Query<GraphButton>().ToList().All(button => !button.focusable), Is.True, node.Screen.name);
            }
        }

        // Scene ノード Main のシーンを開き、1 つだけの Graph Runner に UIDocument（この Starter の Panel Settings）と Graph Screen Host があることを確かめてから渡す
        private static void WithScene(NodeGraphAsset graph, System.Action<Scene> check)
        {
            var path = graph.Nodes.OfType<SceneNode>().Single(node => node.Title == ToolkitGameStarter.MainSceneNode).Scene.Path;
            Assert.That(path, Is.EqualTo($"{Folder}/{graph.name}.unity"));
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var runner = Find<GraphRunnerBehaviour>(scene).Single();
                Assert.That(runner.Graph, Is.SameAs(graph));
                Assert.That(runner.GetComponent<GraphScreenHost>().Graph, Is.SameAs(graph));
                var document = runner.GetComponent<UIDocument>();
                Assert.That(AssetDatabase.GetAssetPath(document.panelSettings), Is.EqualTo(ToolkitGameStarter.PanelSettingsPath(graph)));
                Assert.That(document.panelSettings.themeStyleSheet, Is.Not.Null);
                Assert.That(document.visualTreeAsset, Is.Null, "the host shows the node's screens");
                Assert.That(Find<UIDocument>(scene), Has.Count.EqualTo(1));
                check(scene);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static List<string> Keys(Scene scene) => Find<GraphEventKey>(scene).Select(key => $"{key.Key}: {key.EventName}").ToList();

        private static List<T> Find<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToList();
    }
}
