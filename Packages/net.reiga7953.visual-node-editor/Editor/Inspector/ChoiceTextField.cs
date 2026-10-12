using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Editor.Inspector
{
    /// <summary>
    /// 文字列の欄と、その横の「▾」で選択肢から選べるメニュー（UI Builder の属性など、グラフのアセットの外の欄に使う）。
    /// 選択肢に無い値も打てる。見た目は ChoiceTextField.uss（どのインスペクタでも出るよう、自分で読み込む）。
    /// </summary>
    public sealed class ChoiceTextField : VisualElement
    {
        private const string StyleSheetPath = "VisualNodeEditor/ChoiceTextField";

        /// <param name="property">編集する文字列のプロパティ。</param>
        /// <param name="label">欄の名前。</param>
        /// <param name="choices">「▾」のメニューに出す選択肢（押したときに作る）。</param>
        /// <param name="noChoices">選択肢が無いときにメニューに出す文。</param>
        public ChoiceTextField(SerializedProperty property, string label, Func<IReadOnlyList<string>> choices, string noChoices)
        {
            AddToClassList("vne-choice-text-field");
            var styleSheet = Resources.Load<StyleSheet>(StyleSheetPath);
            if (styleSheet != null)
            {
                styleSheets.Add(styleSheet);
            }

            var text = new TextField(label);
            text.AddToClassList("vne-choice-text-field__text");
            text.AddToClassList(BaseField<string>.alignedFieldUssClassName);
            text.BindProperty(property);
            Add(text);

            var pick = new Button { text = "▾", tooltip = "Pick from the graph" };
            pick.AddToClassList("vne-choice-text-field__pick");
            pick.clicked += () =>
            {
                var menu = new GenericMenu();
                var items = choices();
                if (items.Count == 0)
                {
                    menu.AddDisabledItem(new GUIContent(noChoices));
                }

                foreach (var item in items)
                {
                    var value = item;
                    menu.AddItem(new GUIContent(value.Replace("/", "∕")), value == property.stringValue, () =>
                    {
                        property.serializedObject.Update();
                        property.stringValue = value;
                        property.serializedObject.ApplyModifiedProperties();
                    });
                }

                menu.DropDown(pick.worldBound);
            };
            Add(pick);
        }

        /// <summary>
        /// <paramref name="property"/> と同じ並びにある <c>graph</c>（UI Toolkit のコントロールの属性・コンポーネントのフィールド）のグラフ。
        /// <c>_graph</c> / <c>graph</c> / <c>Graph</c> の順に探す。無ければ null。
        /// </summary>
        public static NodeGraphAsset FindSiblingGraph(SerializedProperty property)
        {
            var path = property.propertyPath;
            var dot = path.LastIndexOf('.');
            var parent = dot < 0 ? string.Empty : path.Substring(0, dot + 1);
            foreach (var name in new[] { "_graph", "graph", "Graph" })
            {
                var sibling = property.serializedObject.FindProperty(parent + name);
                if (sibling != null && sibling.propertyType == SerializedPropertyType.ObjectReference && sibling.objectReferenceValue is NodeGraphAsset graph)
                {
                    return graph;
                }
            }

            return null;
        }
    }
}
