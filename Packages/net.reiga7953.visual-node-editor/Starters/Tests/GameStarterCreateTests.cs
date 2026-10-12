using System.Linq;
using NUnit.Framework;
using Reiga.VisualNodeEditor.Starters.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Reiga.VisualNodeEditor.Starters.Tests
{
    /// <summary>
    /// Starter で作る（グラフ・シーン）と、書き出したスクリプトを後で盤に付ける（<see cref="PendingStarterComponents"/>）。
    /// テスト中にスクリプトを書くとコンパイルが走るので、スクリプトは書かず、付けるクラスには Graph Runner（<c>_graph</c> を持つ）を使う。
    /// </summary>
    public sealed class GameStarterCreateTests
    {
        private const string Folder = "Assets/__VneGameStarterTemp";
        private const string StandInClass = "Reiga.VisualNodeEditor.GraphRunnerBehaviour";

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(Folder); // 前に止めたテストの残り
            AssetDatabase.CreateFolder("Assets", "__VneGameStarterTemp");
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
        public void CreateIn_WithScenes_PutsTheScenesIntoTheGraph_WithoutSwitchingTheOpenScene()
        {
            var graph = SnakeStarter.Instance.CreateIn(Folder, withScenes: true, writeScript: false);

            Assert.That(AssetDatabase.GetAssetPath(graph), Is.EqualTo(Folder + "/Snake.asset"));
            var title = graph.Nodes.OfType<SceneNode>().Single(node => node.Title == GameStarter.TitleSceneNode);
            var game = graph.Nodes.OfType<SceneNode>().Single(node => node.Title == GameStarter.GameSceneNode);
            Assert.That(title.Scene.Path, Is.EqualTo(Folder + "/Snake Title.unity"));
            Assert.That(game.Scene.Path, Is.EqualTo(Folder + "/Snake Game.unity"));
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(game.Scene.Path), Is.Not.Null);
            Assert.That(SceneManager.sceneCount, Is.EqualTo(1), "the new scenes are closed again");
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(Folder + "/Host.unity"));
            Assert.That(GraphValidator.Validate(graph), Is.Empty, "the scenes are set, so there is nothing to fix");
            Assert.That(PendingStarterComponents.Count, Is.Zero, "no script was written, so nothing waits to be added");

            // 2 つ目は番号付きの名前になる
            var second = SlidingPuzzleStarter.Instance.CreateIn(Folder, withScenes: false, writeScript: false);
            Assert.That(second.name, Is.EqualTo("Sliding Puzzle"));
            Assert.That(SnakeStarter.Instance.CreateIn(Folder, withScenes: false, writeScript: false).name, Is.EqualTo("Snake 1"));
        }

        [Test]
        public void TheGameScript_IsAddedToTheBoard_OnceItCompiles()
        {
            var graph = SlidingPuzzleStarter.Instance.CreateIn(Folder, withScenes: true, writeScript: false);
            var scenePath = graph.Nodes.OfType<SceneNode>().Single(node => node.Title == GameStarter.GameSceneNode).Scene.Path;
            PendingStarterComponents.Add(new PendingStarterComponent(scenePath, "Canvas/Board", StandInClass, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(graph))));
            PendingStarterComponents.Add(new PendingStarterComponent(scenePath, "Canvas/Board", "NotCompiledYetGame", string.Empty));

            Assert.That(PendingStarterComponents.CompleteAll(), Is.EqualTo(1));

            Assert.That(PendingStarterComponents.Count, Is.EqualTo(1), "the class that is not compiled yet waits for the next reload");
            Assert.That(SceneManager.sceneCount, Is.EqualTo(1), "the scene was opened, saved and closed again");
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                var board = PendingStarterComponents.Find(scene, "Canvas/Board");
                Assert.That(board.GetComponent<GraphRunnerBehaviour>().Graph, Is.SameAs(graph), "its Graph field is set");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void Attach_ToAnOpenScene_LeavesItUnsavedForTheUser()
        {
            var graph = SnakeStarter.Instance.CreateIn(Folder, withScenes: true, writeScript: false);
            var scenePath = graph.Nodes.OfType<SceneNode>().Single(node => node.Title == GameStarter.GameSceneNode).Scene.Path;
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                Assert.That(PendingStarterComponents.Attach(scenePath, "Canvas/Board", typeof(GraphRunnerBehaviour), graph), Is.Null);

                Assert.That(scene.isDirty, Is.True);
                Assert.That(PendingStarterComponents.Find(scene, "Canvas/Board").GetComponent<GraphRunnerBehaviour>(), Is.Not.Null);
                Assert.That(PendingStarterComponents.Attach(scenePath, "Canvas/Nowhere", typeof(GraphRunnerBehaviour), graph), Does.Contain("no 'Canvas/Nowhere'"));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }

            Assert.That(PendingStarterComponents.Attach(Folder + "/Gone.unity", "Canvas/Board", typeof(GraphRunnerBehaviour), graph), Does.Contain("gone"));
        }

        [Test]
        public void ComponentTypes_AreFoundByFullName()
        {
            Assert.That(PendingStarterComponents.FindComponentType(StandInClass), Is.EqualTo(typeof(GraphRunnerBehaviour)));
            Assert.That(PendingStarterComponents.FindComponentType("NotCompiledYetGame"), Is.Null);
        }

        [Test]
        public void AMissingTemplate_CreatesNothing()
        {
            // ひな形は何かを作る前に読むので、読めなければグラフもシーンも残らない
            Assert.That(() => new MissingTemplateStarter().CreateIn(Folder, withScenes: true), Throws.InvalidOperationException);

            var left = AssetDatabase.FindAssets(string.Empty, new[] { Folder }).Select(AssetDatabase.GUIDToAssetPath);
            Assert.That(left, Is.EqualTo(new[] { Folder + "/Host.unity" }));
            Assert.That(PendingStarterComponents.Count, Is.Zero);
        }
    }
}
