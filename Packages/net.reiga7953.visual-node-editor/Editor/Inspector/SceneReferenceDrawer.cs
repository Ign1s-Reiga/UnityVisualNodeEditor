using System.Linq;
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

            var root = new VisualElement();
            var field = new ObjectField(property.displayName)
            {
                objectType = typeof(SceneAsset),
                allowSceneObjects = false,
            };
            field.AddToClassList(BaseField<UnityEngine.Object>.alignedFieldUssClassName);
            var help = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
            root.Add(field);
            root.Add(help);

            var scene = Resolve(guidProperty.stringValue, pathProperty.stringValue);
            field.SetValueWithoutNotify(scene);
            SyncMovedScene(scene, pathProperty);
            UpdateHelp(help, guidProperty.stringValue, scene);

            field.RegisterValueChangedCallback(evt =>
            {
                var path = evt.newValue != null ? AssetDatabase.GetAssetPath(evt.newValue) : string.Empty;
                guidProperty.stringValue = string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
                pathProperty.stringValue = path;
                property.serializedObject.ApplyModifiedProperties();
                UpdateHelp(help, guidProperty.stringValue, evt.newValue as SceneAsset);
            });

            return root;
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
