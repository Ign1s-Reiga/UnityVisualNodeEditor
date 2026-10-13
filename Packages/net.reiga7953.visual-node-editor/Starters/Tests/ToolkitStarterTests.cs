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
    /// ゲームの Starter の UI Toolkit 版: 画面（UXML・USS・Panel Settings）と、それを出すシーン。画面のコントロールがグラフのイベント・パラメータ・ノードを指していること。
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
        public void Snake_MakesTheScreens_AndScenesThatShowThem()
        {
            var graph = SnakeToolkitStarter.Instance.CreateIn(Folder, withScenes: true, writeScript: false);

            foreach (var file in new[] { "Snake.uss", "Snake Title.uxml", "Snake Game.uxml", "Snake Panel Settings.asset", "Runtime Theme.tss", "Snake Title.unity", "Snake Game.unity" })
            {
                Assert.That(File.Exists($"{Folder}/{file}"), Is.True, file);
            }

            Assert.That(GraphValidator.Validate(graph), Is.Empty);
            Assert.That(SceneManager.sceneCount, Is.EqualTo(1), "the new scenes are closed again");

            WithScene(graph, GameStarter.TitleSceneNode, (scene, screen) =>
            {
                Assert.That(Find<GraphRunnerBehaviour>(scene).Single().Graph, Is.SameAs(graph));
                Assert.That(screen.Q<GraphButton>("start").EventName, Is.EqualTo(GameStarter.StartEvent));
                Assert.That(screen.Q<Label>("heading").text, Is.EqualTo("Snake"));
                Assert.That(Find<GraphEventKey>(scene).Select(key => key.Key), Is.EquivalentTo(new[] { KeyCode.Return, KeyCode.Space }));
            });

            WithScene(graph, GameStarter.GameSceneNode, (scene, screen) =>
            {
                Assert.That(screen.Q("board"), Is.Not.Null, "the game script draws in it");
                AssertPanels(graph, screen, "Ready", "Paused", "Game Over");
                AssertControlsPointIntoTheGraph(graph, screen);
                Assert.That(screen.Query<GraphParameterLabel>().ToList().Select(label => label.Parameter), Is.EquivalentTo(new[] { "Score", "HighScore", "Score" }));
                Assert.That(Find<GraphEventKey>(scene).ToDictionary(key => key.Key, key => key.EventName),
                    Is.EquivalentTo(new Dictionary<KeyCode, string> { { KeyCode.Space, "Go" }, { KeyCode.Escape, "Pause" }, { KeyCode.Return, "Retry" } }));
                Assert.That(Find<GraphRunnerBehaviour>(scene), Is.Empty, "the runner stays from the Title scene");
            });
        }

        [Test]
        public void Puzzle_MakesTheScreens_WithItsPanels()
        {
            var graph = SlidingPuzzleToolkitStarter.Instance.CreateIn(Folder, withScenes: true, writeScript: false);

            WithScene(graph, GameStarter.GameSceneNode, (scene, screen) =>
            {
                Assert.That(screen.Q("board"), Is.Not.Null);
                AssertPanels(graph, screen, "Paused", "Solved", "All Clear");
                AssertControlsPointIntoTheGraph(graph, screen);
                Assert.That(Find<GraphEventKey>(scene).Select(key => key.EventName), Is.EquivalentTo(new[] { "Pause", "Next" }));
            });
        }

        [Test]
        public void TheGameScript_GoesOnTheScreensGameObject()
        {
            var graph = SnakeToolkitStarter.Instance.CreateIn(Folder, withScenes: true, writeScript: false);
            var scenePath = graph.Nodes.OfType<SceneNode>().Single(node => node.Title == GameStarter.GameSceneNode).Scene.Path;
            PendingStarterComponents.Add(new PendingStarterComponent(scenePath, ToolkitGameStarter.DocumentObjectName, "Reiga.VisualNodeEditor.GraphRunnerBehaviour",
                AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(graph))));

            Assert.That(PendingStarterComponents.CompleteAll(), Is.EqualTo(1), "there is a UI object to add it to");
        }

        [Test]
        public void AScreenFileInTheWay_IsNotOverwritten_AndNothingHalfMadeIsLeft()
        {
            File.WriteAllText($"{Folder}/Snake Game.uxml", "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\" />");
            AssetDatabase.ImportAsset($"{Folder}/Snake Game.uxml");
            LogAssert.Expect(LogType.Exception, new Regex("already exists"));
            LogAssert.Expect(LogType.Error, new Regex("could not create the Title and Game scenes"));

            var graph = SnakeToolkitStarter.Instance.CreateIn(Folder, withScenes: true, writeScript: false);

            Assert.That(File.ReadAllText($"{Folder}/Snake Game.uxml"), Does.Not.Contain("GraphNodePanel"), "the file in the way is kept as it was");
            var left = AssetDatabase.FindAssets(string.Empty, new[] { Folder }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path, System.StringComparer.Ordinal);
            Assert.That(left, Is.EqualTo(new[] { $"{Folder}/Host.unity", $"{Folder}/Snake Game.uxml", $"{Folder}/Snake.asset" }));
            Assert.That(graph.Nodes.OfType<SceneNode>().All(node => string.IsNullOrEmpty(node.Scene.Path)), Is.True);
        }

        [Test]
        public void WithoutScenes_TheScreenFilesAreStillMade()
        {
            // シーンを作れないとき（無題のシーン・Play 中）も、画面は作る。自分のシーンに UIDocument を置いて入れれば、スクリプトをそのまま使える
            var graph = SlidingPuzzleToolkitStarter.Instance.CreateIn(Folder, withScenes: false, writeScript: false);

            foreach (var file in new[] { "Sliding Puzzle.uss", "Sliding Puzzle Title.uxml", "Sliding Puzzle Game.uxml", "Sliding Puzzle Panel Settings.asset", "Runtime Theme.tss" })
            {
                Assert.That(File.Exists($"{Folder}/{file}"), Is.True, file);
            }

            Assert.That(File.Exists($"{Folder}/Sliding Puzzle Game.unity"), Is.False);
            Assert.That(graph.Nodes.OfType<SceneNode>().All(node => string.IsNullOrEmpty(node.Scene.Path)), Is.True);
            var screen = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{Folder}/Sliding Puzzle Game.uxml").Instantiate();
            Assert.That(screen.Q("board"), Is.Not.Null);
        }

        // パネルはノードごとに 1 つで、どれもグラフのノードの場所を指す
        private static void AssertPanels(NodeGraphAsset graph, VisualElement screen, params string[] nodes)
        {
            var panels = screen.Query<GraphNodePanel>().ToList();
            Assert.That(panels.Select(panel => panel.Node), Is.EquivalentTo(nodes));
            var places = GraphNodePathDrawer.GetChoices(graph);
            foreach (var panel in panels)
            {
                Assert.That(places.Any(place => place == panel.Node || place.EndsWith("/" + panel.Node, System.StringComparison.Ordinal)), Is.True, panel.Node);
            }
        }

        // ボタンのイベントとラベルのパラメータは、どれもグラフにある
        private static void AssertControlsPointIntoTheGraph(NodeGraphAsset graph, VisualElement screen)
        {
            Assert.That(screen.Query<GraphButton>().ToList().Select(button => button.EventName).Distinct(), Is.SubsetOf(GraphEventNames.CollectRaisable(graph)));
            Assert.That(screen.Query<GraphParameterLabel>().ToList().Select(label => label.Parameter).Distinct(), Is.SubsetOf(GraphParameterNameDrawer.GetChoices(graph)));

            // 同じキーを Graph Event Key で送るので、ボタンはフォーカスを取らない（フォーカスしたボタンが Enter / Space でもう一度送らないように）
            Assert.That(screen.Query<GraphButton>().ToList().All(button => !button.focusable), Is.True);
        }

        // Scene ノードのシーンを開き、その UIDocument（UI）の画面を作って渡す
        private static void WithScene(NodeGraphAsset graph, string sceneNode, System.Action<Scene, VisualElement> check)
        {
            var path = graph.Nodes.OfType<SceneNode>().Single(node => node.Title == sceneNode).Scene.Path;
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var document = Find<UIDocument>(scene).Single();
                Assert.That(document.name, Is.EqualTo(ToolkitGameStarter.DocumentObjectName));
                Assert.That(document.panelSettings, Is.Not.Null);
                Assert.That(document.panelSettings.themeStyleSheet, Is.Not.Null);
                check(scene, document.visualTreeAsset.Instantiate());
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static List<T> Find<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToList();
    }
}
