using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Reiga.VisualNodeEditor.YarnSpinner.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Yarn.Unity;

namespace Reiga.VisualNodeEditor.YarnSpinner.Tests
{
    /// <summary>ADV のひな形（<see cref="AdvStarter"/>）: グラフがそのまま流れ、Yarn スクリプトがコンパイルできる。</summary>
    public sealed class AdvStarterTests
    {
        private const string Folder = "Assets/__VneAdvStarterTemp";

        [Test]
        public void Prefix_SkipsNamesThatAreAlreadyTaken()
        {
            // 別のフォルダのひな形も同じファイル名（ADV）になりうるので、頭に付けた名前も使われていれば番号を足す
            var taken = new HashSet<string> { "Chapter1", "ADV_Chapter1", "ADV2_GoodEnd" };

            Assert.That(AdvStarter.ChoosePrefix("ADV", taken.Contains), Is.EqualTo("ADV3_"));
            Assert.That(AdvStarter.ChoosePrefix("ADV", new HashSet<string> { "Chapter2" }.Contains), Is.EqualTo("ADV_"));
            Assert.That(AdvStarter.ChoosePrefix("ADV", _ => false), Is.Empty, "no prefix while the names are free");
        }

        [Test]
        public void ChooseProject_PrefersTheNearestProjectThatHasTheStarter()
        {
            // Assets/Game の Game.yarnproject と Assets/Game/Adv の Adv.yarnproject の両方がフォルダを含む
            var projects = new[] { "Assets/Game/Game.yarnproject", "Assets/Game/Adv/Adv.yarnproject" };

            Assert.That(AdvStarter.ChooseProject(projects, _ => true), Is.EqualTo("Assets/Game/Adv/Adv.yarnproject"), "the nearest");
            Assert.That(AdvStarter.ChooseProject(projects, path => path.StartsWith("Assets/Game/Game")), Is.EqualTo("Assets/Game/Game.yarnproject"),
                "the nearest one excludes the starter's script, so take the one that has it");
            Assert.That(AdvStarter.ChooseProject(projects, _ => false), Is.EqualTo("Assets/Game/Adv/Adv.yarnproject"), "none has it yet: still the nearest");
            Assert.That(AdvStarter.ChooseProject(new string[0], _ => true), Is.Null);
        }

        [Test]
        public void Graph_HasNoErrors()
        {
            var graph = AdvStarter.BuildGraph();
            try
            {
                var errors = GraphValidator.Validate(graph).Where(i => i.Severity == GraphIssueSeverity.Error).ToList();

                Assert.That(errors, Is.Empty, string.Join("\n", errors));
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [TestCase(5, "GoodEnd")]
        [TestCase(1, "NormalEnd")]
        public void Graph_PlaysThroughToAnEnding(int affection, string ending)
        {
            var graph = AdvStarter.BuildGraph();
            var runner = new GraphRunner(graph);
            var player = new FakeDialoguePlayer();
            var flow = new DialogueFlow(player) { Warn = message => Assert.Fail(message) };
            try
            {
                runner.Start();
                flow.Attach(runner);
                Assert.That(runner.Current.Title, Is.EqualTo("Title"));

                // NewGame → Story のシーン → 自動で Story のコンテナ → Chapter1
                runner.Raise("NewGame");
                flow.Tick();
                Assert.That(player.Started, Is.EqualTo(new[] { "Chapter1" }));

                flow.OnDialogueCompleted();
                flow.Tick();
                runner.SetInt("Affection", affection);
                flow.OnDialogueCompleted();
                flow.Tick();
                Assert.That(player.Started.Last(), Is.EqualTo(ending));

                // エンディングの後はコンテナを出てタイトルへ
                flow.OnDialogueCompleted();
                flow.Tick();
                Assert.That(runner.Current.Title, Is.EqualTo("Title"));
            }
            finally
            {
                flow.Detach();
                runner.Stop();
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void CreateIn_WritesAGraphAndAYarnProjectThatCompiles()
        {
            AssetDatabase.DeleteAsset(Folder); // 前に止めたテストの残り
            AssetDatabase.CreateFolder("Assets", "__VneAdvStarterTemp");
            try
            {
                var graph = AdvStarter.CreateIn(Folder);

                Assert.That(AssetDatabase.GetAssetPath(graph), Is.EqualTo(Folder + "/ADV.asset"));
                var project = AssetDatabase.LoadAssetAtPath<YarnProject>(Folder + "/ADV.yarnproject");
                Assert.That(project, Is.Not.Null);
                Assert.That(project.NodeNames, Is.SupersetOf(new[] { "Chapter1", "Chapter2", "GoodEnd", "NormalEnd" }));
                Assert.That(project.InitialValues.Keys, Has.Member("$Affection"));

                // もう 1 つ作ると、番号付きの名前で、同じ Yarn プロジェクトに入る（2 つ目のプロジェクトは作らない）。
                // ノード名が重なるとプロジェクトがコンパイルできないので、頭にファイル名を付ける
                var second = AdvStarter.CreateIn(Folder);
                Assert.That(AssetDatabase.GetAssetPath(second), Is.EqualTo(Folder + "/ADV 1.asset"));
                Assert.That(AssetDatabase.FindAssets("t:YarnProject", new[] { Folder }), Has.Length.EqualTo(1));
                Assert.That(second.Nodes.OfType<DialogueNode>().Select(n => n.YarnNode), Has.Member("ADV1_Chapter1"));
                project = AssetDatabase.LoadAssetAtPath<YarnProject>(Folder + "/ADV.yarnproject");
                Assert.That(project.NodeNames, Is.SupersetOf(new[] { "Chapter1", "ADV1_Chapter1", "ADV1_NormalEnd" }));
            }
            finally
            {
                AssetDatabase.DeleteAsset(Folder);
            }
        }

        [Test]
        public void CreateIn_WithScenes_PutsPlayableScenesIntoTheGraph_WithoutSwitchingTheOpenScene()
        {
            AssetDatabase.DeleteAsset(Folder); // 前に止めたテストの残り
            AssetDatabase.CreateFolder("Assets", "__VneAdvStarterTemp");
            try
            {
                // Yarn Spinner の Dialogue System は TextMesh Pro の読み込みのウィンドウを開くので、同じ作りの代わりのプレハブを使う
                AdvStarterScenes.DialogueSystemPrefabOverride = TestDialogueSystemPrefab.Create(Folder + "/Dialogue System.prefab");

                // 無題のシーンが開いていると、Unity は追加でシーンを作れないので、保存したシーンを開いておく
                var host = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(host, Folder + "/Host.unity");
                Assert.That(AdvStarterScenes.GetProblem(), Is.Null);

                var graph = AdvStarter.CreateIn(Folder, withScenes: true);

                var title = graph.Nodes.OfType<SceneNode>().Single(node => node.Title == AdvStarterScenes.TitleSceneNode);
                var story = graph.Nodes.OfType<SceneNode>().Single(node => node.Title == AdvStarterScenes.StorySceneNode);
                Assert.That(title.Scene.Path, Is.EqualTo(Folder + "/ADV Title.unity"));
                Assert.That(story.Scene.Path, Is.EqualTo(Folder + "/ADV Story.unity"));
                Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(title.Scene.Path), Is.Not.Null);
                Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(story.Scene.Path), Is.Not.Null);
                Assert.That(SceneManager.sceneCount, Is.EqualTo(1), "the new scenes are closed again");
                Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(Folder + "/Host.unity"), "the open scene is not switched");

                // 保存したシーンにも、組み立てたものが入っている
                var titleScene = EditorSceneManager.OpenScene(title.Scene.Path, OpenSceneMode.Additive);
                var runner = titleScene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GraphRunnerBehaviour>(true)).Single();
                Assert.That(runner.Graph, Is.SameAs(graph));
                EditorSceneManager.CloseScene(titleScene, true);
                var storyScene = EditorSceneManager.OpenScene(story.Scene.Path, OpenSceneMode.Additive);
                var bridge = storyScene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<YarnGraphBridge>(true)).Single();
                Assert.That(bridge.Graph, Is.SameAs(graph));
                EditorSceneManager.CloseScene(storyScene, true);
            }
            finally
            {
                AdvStarterScenes.DialogueSystemPrefabOverride = null;
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(Folder);
            }
        }
    }
}
