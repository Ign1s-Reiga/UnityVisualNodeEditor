using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>
    /// UI Toolkit の画面: UXML に書いた属性がコントロールに入ること（UI Builder で置いたものと同じ）と、Scene ノードの Create Screen… / Edit Screen（<see cref="SceneScreens"/>）。
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
        public void CreateScreen_PutsAUIDocumentInTheScene_AndEditScreenFindsIt()
        {
            // 無題のシーンが開いていると、Unity は追加でシーンを開けないので、保存したシーンを開いておく
            var host = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(host, Folder + "/Host.unity");
            var level = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            EditorSceneManager.SaveScene(level, Folder + "/Level.unity");
            EditorSceneManager.CloseScene(level, true);
            Assert.That(SceneScreens.FindScreens(Folder + "/Level.unity"), Is.Empty);

            var uxml = SceneScreens.CreateScreen(Folder + "/Level.unity", out var problem);

            Assert.That(problem, Is.Null);
            Assert.That(uxml, Is.EqualTo(Folder + "/Level.uxml"));
            Assert.That(SceneScreens.FindScreens(Folder + "/Level.unity"), Is.EqualTo(new[] { uxml }), "Edit Screen finds it");
            Assert.That(SceneManager.sceneCount, Is.EqualTo(1), "the scene was opened, saved and closed again");

            var scene = EditorSceneManager.OpenScene(Folder + "/Level.unity", OpenSceneMode.Additive);
            try
            {
                var document = scene.GetRootGameObjects().Select(root => root.GetComponent<UIDocument>()).Single(found => found != null);
                Assert.That(AssetDatabase.GetAssetPath(document.visualTreeAsset), Is.EqualTo(uxml));
                Assert.That(document.panelSettings, Is.Not.Null);
                Assert.That(document.panelSettings.themeStyleSheet, Is.Not.Null, "default controls are styled");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }

            var screen = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxml).Instantiate();
            Assert.That(screen.Q<Label>("heading").text, Is.EqualTo("Level"));
        }

        [Test]
        public void CreateScreen_ExplainsWhyItCannot()
        {
            Assert.That(SceneScreens.CreateScreen(Folder + "/Missing.unity", out var problem), Is.Null);
            Assert.That(problem, Does.Contain("scene"));
            Assert.That(SceneScreens.FindScreens(string.Empty), Is.Empty);
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
    }
}
