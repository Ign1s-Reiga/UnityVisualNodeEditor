using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Editor.Inspector
{
    /// <summary>
    /// <see cref="GraphParameterNameAttribute"/> の付いた文字列フィールドを、同じ並びの <c>graph</c> のグラフのパラメータ名から選べる欄にする
    /// （UI Builder の Graph Parameter Label の <c>parameter</c> など）。グラフが無ければ入力欄。
    /// IMGUI で描かれるとき（Unity Localization の Local Variables の Graph Parameter など）は、入力欄と、パラメータ名を選ぶ「▾」。
    /// </summary>
    [CustomPropertyDrawer(typeof(GraphParameterNameAttribute))]
    public sealed class GraphParameterNameDrawer : PropertyDrawer
    {
        private const float MenuButtonWidth = 20f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.LabelField(position, label.text, "[GraphParameterName] only works on string fields.");
                return;
            }

            var choices = GetChoices(ChoiceTextField.FindSiblingGraph(property));
            var field = position;
            if (choices.Count > 0)
            {
                field.width -= MenuButtonWidth + 2f;
            }

            label = EditorGUI.BeginProperty(field, label, property);
            EditorGUI.BeginChangeCheck();
            var value = EditorGUI.TextField(field, label, property.stringValue);
            if (EditorGUI.EndChangeCheck())
            {
                property.stringValue = value;
            }

            EditorGUI.EndProperty();
            if (choices.Count == 0)
            {
                return;
            }

            var button = new Rect(field.xMax + 2f, position.y, MenuButtonWidth, EditorGUIUtility.singleLineHeight);
            if (EditorGUI.DropdownButton(button, new GUIContent("▾", "Pick one of the graph's parameters"), FocusType.Passive, EditorStyles.miniButton))
            {
                ShowMenu(button, property, choices);
            }
        }

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                return new Label($"[GraphParameterName] only works on string fields ({property.displayName}).");
            }

            var label = string.IsNullOrEmpty(preferredLabel) ? property.displayName : preferredLabel;
            return new ChoiceTextField(property, label, () => GetChoices(ChoiceTextField.FindSiblingGraph(property)),
                "Set the graph to pick one of its parameters");
        }

        /// <summary><paramref name="graph"/> のパラメータ名（空を除き、グラフの中の順で重なり無し）。グラフが無ければ空。</summary>
        public static List<string> GetChoices(NodeGraphAsset graph) =>
            graph == null
                ? new List<string>()
                : graph.Parameters.Where(p => p != null && !string.IsNullOrEmpty(p.Name)).Select(p => p.Name).Distinct().ToList();

        // 選んだら、その時点の SerializedObject に書く（メニューは後のフレームで閉じるので、プロパティは持ち越さない）
        private static void ShowMenu(Rect button, SerializedProperty property, List<string> choices)
        {
            var serializedObject = property.serializedObject;
            var path = property.propertyPath;
            var current = property.stringValue;
            var menu = new GenericMenu();
            foreach (var choice in choices)
            {
                var picked = choice;
                menu.AddItem(new GUIContent(choice), choice == current, () =>
                {
                    serializedObject.Update();
                    var target = serializedObject.FindProperty(path);
                    if (target == null)
                    {
                        return;
                    }

                    target.stringValue = picked;
                    serializedObject.ApplyModifiedProperties();
                });
            }

            menu.DropDown(button);
        }
    }
}
