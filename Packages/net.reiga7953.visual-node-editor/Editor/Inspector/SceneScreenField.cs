using System.IO;
using Reiga.VisualNodeEditor.Editor.Scenes;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Editor.Inspector
{
    /// <summary>
    /// Scene ノードのインスペクタの Screen の行。シーンが使っている UI Toolkit の画面（UXML）を UI Builder で開く（Edit Screen。2 つ以上ならメニュー）。
    /// 無ければ Create Screen… で作って開く（<see cref="SceneScreens"/>）。見た目は NodeGraphEditor.uss（.vne-scene-screen）。
    /// </summary>
    public sealed class SceneScreenField : VisualElement
    {
        internal const string EditLabel = "Edit Screen";
        internal const string CreateLabel = "Create Screen…";

        private readonly SceneNode _node;

        /// <param name="node">表示する Scene ノード（シーンは押したときに読む）。</param>
        public SceneScreenField(SceneNode node)
        {
            _node = node;
            AddToClassList("vne-scene-screen");
            var label = new Label("Screen") { tooltip = "The UI Toolkit screen (UXML) shown in this scene. Lay it out in UI Builder." };
            label.AddToClassList("vne-scene-screen__label");
            Add(label);
            Rebuild();
        }

        private void Rebuild()
        {
            while (childCount > 1)
            {
                RemoveAt(childCount - 1);
            }

            var scenePath = _node.Scene.Path;
            if (string.IsNullOrEmpty(scenePath) || AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
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
                    var created = SceneScreens.CreateScreen(scenePath, out var problem);
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
