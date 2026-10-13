using System.IO;
using Reiga.VisualNodeEditor.Editor.Scenes;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Editor.Inspector
{
    /// <summary>
    /// Scene ノードのインスペクタの Screen の行。シーンが使っている UI Toolkit の画面（UXML）を UI Builder で開く（Edit Screen。2 つ以上ならメニュー）。
    /// 無ければ Create Screen… で作って開く（<see cref="SceneScreens"/>）。Scene の欄を変えたら作り直し、ボタンは押したときのシーンを使う。
    /// 見た目は NodeGraphEditor.uss（.vne-scene-screen）。
    /// </summary>
    public sealed class SceneScreenField : VisualElement
    {
        /// <summary>Scene ノードのシーンのフィールド名（EditMode テストで存在を検証している）。</summary>
        internal const string ScenePropertyName = "_scene";

        internal const string EditLabel = "Edit Screen";
        internal const string CreateLabel = "Create Screen…";

        private readonly SerializedProperty _sceneProperty;

        /// <param name="sceneProperty">Scene ノードの <c>_scene</c>（<see cref="SceneReference"/>）。</param>
        public SceneScreenField(SerializedProperty sceneProperty)
        {
            _sceneProperty = sceneProperty;
            AddToClassList("vne-scene-screen");
            var label = new Label("Screen") { tooltip = "The UI Toolkit screen (UXML) shown in this scene. Lay it out in UI Builder." };
            label.AddToClassList("vne-scene-screen__label");
            Add(label);
            Rebuild();

            // Scene の欄を変えたら（Pick… / Create Scene… / Undo も）、そのシーンの画面で作り直す
            this.TrackPropertyValue(sceneProperty, _ => Rebuild());
        }

        /// <summary>今のシーンのパス（GUID から引き直す。シーンが無ければ空）。</summary>
        internal string ScenePath
        {
            get
            {
                _sceneProperty.serializedObject.Update();
                var guid = _sceneProperty.FindPropertyRelative(SceneReferenceDrawer.GuidPropertyName)?.stringValue;
                var path = string.IsNullOrEmpty(guid) ? null : AssetDatabase.GUIDToAssetPath(guid);
                return !string.IsNullOrEmpty(path) && AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null ? path : string.Empty;
            }
        }

        /// <summary>今の行のボタン（テストが確かめる）。シーンが無ければ空。</summary>
        internal string ButtonText => this.Q<Button>()?.text ?? string.Empty;

        private void Rebuild()
        {
            while (childCount > 1)
            {
                RemoveAt(childCount - 1);
            }

            var scenePath = ScenePath;
            if (scenePath.Length == 0)
            {
                var none = new Label("Assign a scene first.");
                none.AddToClassList("vne-scene-screen__none");
                Add(none);
                return;
            }

            var screens = SceneScreens.FindScreens(scenePath);
            if (screens.Count == 0)
            {
                Add(CreateButton(CreateLabel, "Create a UXML screen for this scene and open it in UI Builder", () =>
                {
                    var created = SceneScreens.CreateScreen(ScenePath, out var problem);
                    if (created == null)
                    {
                        EditorUtility.DisplayDialog("Create Screen", $"Could not create the screen: {problem}.", "OK");
                        return;
                    }

                    SceneScreens.Open(created);
                    Rebuild();
                }));
                return;
            }

            Button edit = null;
            edit = CreateButton(EditLabel, screens.Count == 1 ? $"Open {Path.GetFileName(screens[0])} in UI Builder" : "Choose the screen to open in UI Builder", () =>
            {
                if (screens.Count == 1)
                {
                    SceneScreens.Open(screens[0]);
                    return;
                }

                var menu = new GenericMenu();
                foreach (var screen in screens)
                {
                    var path = screen;
                    menu.AddItem(new GUIContent(Path.GetFileName(path)), false, () => SceneScreens.Open(path));
                }

                menu.DropDown(edit.worldBound);
            });
            Add(edit);
        }

        private static Button CreateButton(string text, string tooltip, System.Action clicked)
        {
            var button = new Button(clicked) { text = text, tooltip = tooltip };
            button.AddToClassList("vne-scene-screen__button");
            return button;
        }
    }
}
