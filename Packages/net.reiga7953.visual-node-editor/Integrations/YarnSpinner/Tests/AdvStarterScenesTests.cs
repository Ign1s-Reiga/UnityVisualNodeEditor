using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Reiga.VisualNodeEditor.YarnSpinner.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Yarn.Unity;
using Object = UnityEngine.Object;

namespace Reiga.VisualNodeEditor.YarnSpinner.Tests
{
    /// <summary>
    /// ADV Starter のシーン（<see cref="AdvStarterScenes"/>）: Title のボタンと Graph Runner、Story の Dialogue System の繋ぎ方。
    /// 開いているシーンに触らないよう、プレビュー用のシーンに組み立てて確かめる。
    /// </summary>
    public sealed class AdvStarterScenesTests
    {
        private const string Folder = "Assets/__VneAdvStarterScenesTemp";

        private NodeGraphAsset _graph;
        private YarnProject _project;
        private Scene _scene;

        [SetUp]
        public void SetUp()
        {
            _graph = AdvStarter.BuildGraph();
            _graph.name = "ADV";
            _project = ScriptableObject.CreateInstance<YarnProject>();
            _scene = EditorSceneManager.NewPreviewScene();
            AssetDatabase.CreateFolder("Assets", "__VneAdvStarterScenesTemp");
            AdvStarterScenes.DialogueSystemPrefabOverride = TestDialogueSystemPrefab.Create(Folder + "/Dialogue System.prefab");
        }

        [TearDown]
        public void TearDown()
        {
            AdvStarterScenes.DialogueSystemPrefabOverride = null;
            EditorSceneManager.ClosePreviewScene(_scene);
            AssetDatabase.DeleteAsset(Folder);
            Object.DestroyImmediate(_project);
            Object.DestroyImmediate(_graph);
        }

        [Test]
        public void YarnsDialogueSystemPrefab_IsFound_AndComesWithTheDefaultStorage()
        {
            // 本物のプレハブは置かずに確かめる（TextMesh Pro が Essential Resources の読み込みのウィンドウを開くため）
            var prefab = AdvStarterScenes.LoadYarnDialogueSystemPrefab();
            Assert.That(prefab, Is.Not.Null);
            var runner = prefab.GetComponent<DialogueRunner>();
            Assert.That(runner, Is.Not.Null);

            var storage = new SerializedObject(runner).FindProperty(YarnBridgeSetup.VariableStoragePropertyName).objectReferenceValue;
            Assert.That(storage, Is.Not.Null);
            Assert.That(storage.GetType(), Is.EqualTo(typeof(InMemoryVariableStorage)), "this is why Yarn Graph Bridge has to replace it");
            Assert.That(YarnBridgeSetup.IsReplaceable(storage.GetType()), Is.True);
        }

        [Test]
        public void Title_HasNewGameAndContinueButtons_AndTheGraphRunner()
        {
            AdvStarterScenes.BuildTitle(_scene, _graph);

            Assert.That(Find<GraphRunnerBehaviour>().Single().Graph, Is.SameAs(_graph));
            Assert.That(Find<Camera>(), Has.Count.EqualTo(1));
            Assert.That(Find<EventSystem>().Single().GetComponent<BaseInputModule>(), Is.Not.Null, "the buttons can be clicked");

            var buttons = Find<GraphEventButton>();
            var newGame = buttons.Single(button => button.Action == GraphEventButtonAction.Raise);
            Assert.That(newGame.EventName, Is.EqualTo(AdvStarterScenes.NewGameEvent));
            Assert.That(GraphEventNames.Collect(_graph), Has.Member(newGame.EventName), "the graph has that event");
            var load = buttons.Single(button => button.Action == GraphEventButtonAction.Load);
            Assert.That(load.Slot, Is.EqualTo(AdvStarterScenes.AutoSaveSlot));
            foreach (var button in buttons)
            {
                Assert.That(button.Graph, Is.SameAs(_graph));
                Assert.That(button.GetComponent<Button>(), Is.Not.Null);
            }
        }

        [Test]
        public void Story_HasTheDialogueSystem_ConnectedToTheGraph()
        {
            AdvStarterScenes.BuildStory(_scene, _graph, _project);

            var runner = Find<DialogueRunner>().Single();
            AssertConnected(runner);
        }

        [Test]
        public void ConnectToGraph_SetsTheProjectBridgeAndVariableStorage()
        {
            var gameObject = new GameObject("Dialogue System");
            SceneManager.MoveGameObjectToScene(gameObject, _scene);
            var runner = gameObject.AddComponent<DialogueRunner>();

            AdvStarterScenes.ConnectToGraph(runner, _graph, _project);

            AssertConnected(runner);
        }

        private void AssertConnected(DialogueRunner runner)
        {
            var serializedRunner = new SerializedObject(runner);
            Assert.That(serializedRunner.FindProperty(AdvStarterScenes.YarnProjectPropertyName).objectReferenceValue, Is.SameAs(_project));
            Assert.That(serializedRunner.FindProperty(YarnBridgeSetup.VariableStoragePropertyName).objectReferenceValue, Is.InstanceOf<GraphVariableStorage>(),
                "Yarn's $Affection is the graph's Affection");

            var bridge = runner.GetComponent<YarnGraphBridge>();
            Assert.That(bridge, Is.Not.Null);
            Assert.That(bridge.Graph, Is.SameAs(_graph));
            Assert.That(bridge.AutoSaveSlot, Is.EqualTo(AdvStarterScenes.AutoSaveSlot));
        }

        private List<T> Find<T>() where T : Component =>
            _scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToList();
    }
}
