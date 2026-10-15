using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor.Inspector;
using Reiga.VisualNodeEditor.Editor.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>
    /// UI Toolkit の画面: UXML に書いた属性がコントロールに入ること（UI Builder で置いたものと同じ）と、ノードの画面の Create Screen…（<see cref="NodeScreens"/>）と、それを出す Graph Screen Host を Play で付けること。
    /// </summary>
    public sealed class UIToolkitScreenTests
    {
        private const string Folder = "Assets/__VneUIToolkitTemp";

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(Folder); // 前に止めたテストの残り
            AssetDatabase.CreateFolder("Assets", "__VneUIToolkitTemp");
        }

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(Folder);
        }

        [Test]
        public void UxmlAttributes_AreReadIntoTheControls()
        {
            var graph = ScriptableObject.CreateInstance<NodeGraphAsset>();
            AssetDatabase.CreateAsset(graph, Folder + "/Game.asset");
            var guid = AssetDatabase.AssetPathToGUID(Folder + "/Game.asset");
            var uxml =
                "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\" xmlns:vne=\"Reiga.VisualNodeEditor\">\n" +
                $"  <vne:GraphButton name=\"continue\" text=\"Continue\" action=\"Load\" event-name=\"Start\" save-slot=\"auto\" disable-without-save=\"false\" graph=\"project://database/{Folder}/Game.asset?fileID=11400000&amp;guid={guid}&amp;type=2#Game\" />\n" +
                "  <vne:GraphParameterLabel name=\"score\" parameter=\"Score\" format=\"Score: {0}\" text=\"Score: 0\" />\n" +
                "  <vne:GraphNodePanel name=\"paused\" node=\"Round/Paused\">\n" +
                "    <ui:Label text=\"Paused\" />\n" +
                "  </vne:GraphNodePanel>\n" +
                "</ui:UXML>\n";
            File.WriteAllText(Folder + "/Screen.uxml", uxml);
            AssetDatabase.ImportAsset(Folder + "/Screen.uxml");

            var root = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Folder + "/Screen.uxml").Instantiate();

            var button = root.Q<GraphButton>("continue");
            Assert.That(button, Is.Not.Null);
            Assert.That(button.text, Is.EqualTo("Continue"));
            Assert.That(button.Action, Is.EqualTo(GraphEventButtonAction.Load));
            Assert.That(button.EventName, Is.EqualTo("Start"));
            Assert.That(button.Slot, Is.EqualTo("auto"));
            Assert.That(button.DisableWithoutSave, Is.False);
            Assert.That(button.Graph, Is.SameAs(graph), "a graph picked in UI Builder is an asset reference");

            var label = root.Q<GraphParameterLabel>("score");
            Assert.That(label.Parameter, Is.EqualTo("Score"));
            Assert.That(label.Format, Is.EqualTo("Score: {0}"));
            Assert.That(label.text, Is.EqualTo("Score: 0"));

            var panel = root.Q<GraphNodePanel>("paused");
            Assert.That(panel.Node, Is.EqualTo("Round/Paused"));
            Assert.That(panel.Graph, Is.Null, "empty: any running graph");
            Assert.That(panel.Q<Label>().text, Is.EqualTo("Paused"), "a panel holds other elements");
        }

        [TestCase(typeof(GraphButton), typeof(EventNameAttribute))]
        [TestCase(typeof(GraphParameterLabel), typeof(GraphParameterNameAttribute))]
        [TestCase(typeof(GraphNodePanel), typeof(GraphNodePathAttribute))]
        public void UIBuilderFields_CarryTheDropdownAttributes(Type control, Type attribute)
        {
            // UI Builder は UXML の属性を、Unity が作る UxmlSerializedData のフィールドとして描く。そこに印があれば、グラフから選ぶ欄になる
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var data = control.GetNestedType("UxmlSerializedData", all);
            Assert.That(data, Is.Not.Null, "Unity generated the UXML data");
            Assert.That(data.GetFields(all).Any(field => field.IsDefined(attribute, true)), Is.True);
        }

        [Test]
        public void CreateScreen_WritesAUxmlNextToTheGraph_AndTheFieldAssignsIt()
        {
            var graph = ScriptableObject.CreateInstance<NodeGraphAsset>();
            var node = new StateNode { Title = "Title" };
            graph.AddNode(node);
            AssetDatabase.CreateAsset(graph, Folder + "/Game.asset");
            var property = new SerializedObject(graph).FindProperty("_nodes").GetArrayElementAtIndex(0)
                .FindPropertyRelative(NodeScreenField.ScreenPropertyName);
            var field = new NodeScreenField(property, graph, node.Id);
            Assert.That(field.ButtonText, Is.EqualTo(NodeScreenField.CreateLabel));

            Undo.IncrementCurrentGroup();
            var screen = field.CreateAndAssign(out var problem);

            Assert.That(problem, Is.Null);
            Assert.That(AssetDatabase.GetAssetPath(screen), Is.EqualTo(Folder + "/Game Title.uxml"));
            Assert.That(((StateNode)graph.FindNode(node.Id)).Screen, Is.SameAs(screen));
            Assert.That(field.ButtonText, Is.EqualTo(NodeScreenField.EditLabel));
            Assert.That(screen.Instantiate().Q<Label>("heading").text, Is.EqualTo("Title"));

            Undo.PerformUndo();
            Assert.That(((StateNode)graph.FindNode(node.Id)).Screen, Is.Null, "assigning can be undone");
        }

        [Test]
        public void CreateScreen_NeedsASavedGraph()
        {
            var graph = ScriptableObject.CreateInstance<NodeGraphAsset>();
            try
            {
                var node = new StateNode();
                graph.AddNode(node);

                Assert.That(NodeScreens.CreateScreen(graph, node, out var problem), Is.Null);
                Assert.That(problem, Does.Contain("saved"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void ScreenFileNames_AreTheGraphAndTheNode()
        {
            var graph = ScriptableObject.CreateInstance<NodeGraphAsset>();
            try
            {
                graph.name = "Game";

                Assert.That(NodeScreens.GetFileName(graph, new ContainerNode { Title = "Round/Paused" }), Is.EqualTo("Game Round_Paused"));
                Assert.That(NodeScreens.GetFileName(graph, new SceneNode()), Is.EqualTo("Game Scene"), "untitled nodes use the type's name");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void ARunner_GetsAScreenHost_OnlyWhenNodesHaveScreens()
        {
            // シーンを先に作る（新しいシーンを開くと、保存前のアセットは消される）
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var graph = ScriptableObject.CreateInstance<NodeGraphAsset>();
            var node = new StateNode();
            graph.AddNode(node);
            AssetDatabase.CreateAsset(graph, Folder + "/Game.asset");

            var plain = Reiga.VisualNodeEditor.Editor.Play.PlaySetup.CreateRunner(scene, graph);
            Assert.That(plain.GetComponent<GraphScreenHost>(), Is.Null, "no screens: no host");
            Assert.That(Reiga.VisualNodeEditor.Editor.Play.PlaySetup.FindScreenHost(scene, graph), Is.Null);

            File.WriteAllText(Folder + "/Screen.uxml", NodeScreens.BuildDefaultScreen("Title"));
            AssetDatabase.ImportAsset(Folder + "/Screen.uxml");
            node.Screen = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Folder + "/Screen.uxml");
            var runner = Reiga.VisualNodeEditor.Editor.Play.PlaySetup.CreateRunner(scene, graph);

            var host = runner.GetComponent<GraphScreenHost>();
            Assert.That(host, Is.Not.Null);
            Assert.That(host.Graph, Is.SameAs(graph));
            Assert.That(runner.GetComponent<UIDocument>().panelSettings, Is.Not.Null);
            Assert.That(Reiga.VisualNodeEditor.Editor.Play.PlaySetup.FindScreenHost(scene, graph), Is.SameAs(host));

            // 前からある Runner にも付けられる
            var added = Reiga.VisualNodeEditor.Editor.Play.PlaySetup.AddScreenHost(plain.gameObject, graph, true);
            Assert.That(plain.GetComponent<GraphScreenHost>(), Is.SameAs(added));
            Assert.That(plain.GetComponent<UIDocument>().panelSettings, Is.Not.Null);
        }

        [Test]
        public void PanelSettings_AreMadeWithTheDefaultTheme()
        {
            var settings = ScreenAssets.CreatePanelSettings(Folder + "/Panel Settings.asset");

            Assert.That(settings.scaleMode, Is.EqualTo(PanelScaleMode.ScaleWithScreenSize));
            Assert.That(settings.referenceResolution, Is.EqualTo(new Vector2Int(1920, 1080)));
            Assert.That(AssetDatabase.GetAssetPath(settings.themeStyleSheet), Is.EqualTo(Folder + "/Runtime Theme.tss"));
            Assert.That(ScreenAssets.FindOrCreateTheme(Folder), Is.SameAs(settings.themeStyleSheet), "made once per folder");
        }

        [Test]
        public void ThePlayDialogs_SayWhatTheScreenHostAdds()
        {
            // Panel Settings を作ったら Undo では消えないので、作るときは先に書く
            var graph = ScriptableObject.CreateInstance<NodeGraphAsset>();
            try
            {
                graph.name = "Game";
                var text = Reiga.VisualNodeEditor.Editor.Play.PlaySetup.DescribeScreenHost(graph, "It also gets");
                Assert.That(text, Does.StartWith("It also gets a UI Document and a Graph Screen Host"));
                Assert.That(text.Contains("Panel Settings.asset"), Is.EqualTo(ScreenAssets.FindPanelSettings() == null));

                ScreenAssets.CreatePanelSettings(Folder + "/Panel Settings.asset");
                Assert.That(Reiga.VisualNodeEditor.Editor.Play.PlaySetup.DescribeScreenHost(graph, "It gets"), Does.Not.Contain("Panel Settings.asset"),
                    "the project's Panel Settings are used");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(graph);
            }
        }
    }
}
