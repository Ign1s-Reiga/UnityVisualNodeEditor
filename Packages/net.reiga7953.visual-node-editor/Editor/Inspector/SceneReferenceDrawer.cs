using System.Linq;
using Reiga.VisualNodeEditor.Editor.Scenes;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Editor.Inspector
{
    /// <summary>
    /// <see cref="SceneReference"/> を SceneAsset の ObjectField として表示する。
    /// GUID を正とし、シーンが移動・改名されていたら Path を引き直す。
    /// </summary>
    [CustomPropertyDrawer(typeof(SceneReference))]
    public sealed class SceneReferenceDrawer : PropertyDrawer
    {
        // SceneReference の private フィールド名（EditMode テストで存在を検証している）
        internal const string GuidPropertyName = "_guid";
        internal const string PathPropertyName = "_path";

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var guidProperty = property.FindPropertyRelative(GuidPropertyName);
            var pathProperty = property.FindPropertyRelative(PathPropertyName);

            // PropertyField に渡されたラベル（インスペクタは英語で統一している）を優先する
            var label = string.IsNullOrEmpty(preferredLabel) ? property.displayName : preferredLabel;

            var root = new VisualElement();
            var field = new ObjectField(label)
            {
                objectType = typeof(SceneAsset),
                allowSceneObjects = false,
            };
            field.AddToClassList(BaseField<UnityEngine.Object>.alignedFieldUssClassName);
            var help = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
            root.Add(field);
            root.Add(help);

            // シーンが空のときの次の一手: 一覧から選ぶ / 新しく作る（見た目は NodeGraphEditor.uss の .vne-scene-field__actions）
            var actions = new VisualElement();
            actions.AddToClassList(ActionsClassName);
            var pick = new Button { text = PickLabel, tooltip = "Pick a scene from this project" };
            pick.clicked += () => ScenePicker.ShowMenu(pick.worldBound, picked => field.value = picked, GetSuggestedName(property));
            var create = new Button { text = CreateLabel, tooltip = "Create a new empty scene and use it here" };
            create.clicked += () =>
            {
                var created = ScenePicker.CreateScene(GetSuggestedName(property));
                if (created != null)
                {
                    field.value = created;
                }
            };
            actions.Add(pick);
            actions.Add(create);
            root.Add(actions);

            var scene = Resolve(guidProperty.stringValue, pathProperty.stringValue);
            field.SetValueWithoutNotify(scene);
            SyncMovedScene(scene, pathProperty);
            UpdateHelp(help, guidProperty.stringValue, scene);
            UpdateActions(actions, scene);

            field.RegisterValueChangedCallback(evt =>
            {
                var path = evt.newValue != null ? AssetDatabase.GetAssetPath(evt.newValue) : string.Empty;
                guidProperty.stringValue = string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
                pathProperty.stringValue = path;
                property.serializedObject.ApplyModifiedProperties();
                UpdateHelp(help, guidProperty.stringValue, evt.newValue as SceneAsset);
                UpdateActions(actions, evt.newValue as SceneAsset);
            });

            return root;
        }

        /// <summary>シーンが空のときに出す、Pick… と Create Scene… の行の USS クラス。</summary>
        internal const string ActionsClassName = "vne-scene-field__actions";

        internal const string PickLabel = "Pick…";
        internal const string CreateLabel = "Create Scene…";

        private static void UpdateActions(VisualElement actions, SceneAsset scene) =>
            actions.style.display = scene == null ? DisplayStyle.Flex : DisplayStyle.None;

        // 新しいシーンの名前の候補: シーンの欄を持つノードのタイトル（付けていれば）
        private static string GetSuggestedName(SerializedProperty property)
        {
            var path = property.propertyPath;
            var dot = path.LastIndexOf('.');
            var title = dot > 0 ? property.serializedObject.FindProperty(path.Substring(0, dot) + "." + NodeInspectorView.TitlePropertyName) : null;
            return ScenePicker.ToFileName(title?.propertyType == SerializedPropertyType.String ? title.stringValue : null);
        }

        private static SceneAsset Resolve(string guid, string path)
        {
            var resolvedPath = string.IsNullOrEmpty(guid) ? path : AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(resolvedPath) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(resolvedPath);
        }

        private static void SyncMovedScene(SceneAsset scene, SerializedProperty pathProperty)
        {
            if (scene == null)
            {
                return;
            }

            var currentPath = AssetDatabase.GetAssetPath(scene);
            if (currentPath != pathProperty.stringValue)
            {
                pathProperty.stringValue = currentPath;
                pathProperty.serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void UpdateHelp(HelpBox help, string guid, SceneAsset scene)
        {
            string message = null;
            if (!string.IsNullOrEmpty(guid) && scene == null)
            {
                message = "The referenced scene no longer exists.";
            }
            else if (scene != null && !EditorBuildSettings.scenes.Any(s => s.enabled && s.guid.ToString() == guid))
            {
                message = "This scene is not enabled in Build Settings, so it cannot be loaded at runtime.";
            }

            help.text = message ?? string.Empty;
            help.style.display = message != null ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
