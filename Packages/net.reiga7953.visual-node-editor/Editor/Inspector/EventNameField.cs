using System.Collections.Generic;
using System.Linq;
using Reiga.VisualNodeEditor.Editor.Views;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Editor.Inspector
{
    /// <summary>
    /// Event ノードのインスペクタの Event Name 欄。入力欄の横の「▾」で、グラフで既に使っているイベント名から選べる
    /// （同じ文字列を打ち直させない。新しい名前は入力欄に打つ）。見た目は NodeGraphEditor.uss（.vne-event-name-field）。
    /// </summary>
    public sealed class EventNameField : VisualElement
    {
        /// <summary><see cref="EventNode"/> のイベント名のフィールド名（EditMode テストで存在を検証している）。</summary>
        internal const string EventNamePropertyName = "_eventName";

        private readonly SerializedProperty _property;
        private readonly NodeGraphAsset _graph;

        /// <param name="property">Event ノードの <c>_eventName</c>。</param>
        /// <param name="graph">選択肢を取るグラフ。</param>
        public EventNameField(SerializedProperty property, NodeGraphAsset graph)
        {
            _property = property;
            _graph = graph;
            AddToClassList("vne-event-name-field");

            // 値はインスペクタ全体の Bind で結び付く
            var text = new TextField(NodeDisplay.GetFieldLabel(property.name)) { bindingPath = property.propertyPath };
            text.AddToClassList("vne-event-name-field__text");
            text.AddToClassList(BaseField<string>.alignedFieldUssClassName);
            Add(text);

            var pick = new Button { text = "▾", tooltip = "Pick an event name already used in this graph" };
            pick.AddToClassList("vne-event-name-field__pick");
            pick.clicked += () => ShowMenu(pick);
            Add(pick);
        }

        /// <summary>選べるイベント名（グラフにある名前のうち、今の名前以外）。</summary>
        internal List<string> Choices
        {
            get
            {
                _property.serializedObject.Update();
                var current = _property.stringValue;
                return GraphEventNames.Collect(_graph).Where(name => name != current).ToList();
            }
        }

        /// <summary>イベント名を <paramref name="name"/> にする（Undo 可）。</summary>
        internal void Pick(string name)
        {
            _property.serializedObject.Update();
            _property.stringValue = name;
            _property.serializedObject.ApplyModifiedProperties();
        }

        private void ShowMenu(VisualElement anchor)
        {
            var menu = new GenericMenu();
            var choices = Choices;
            if (choices.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("No other event names in this graph yet"));
            }

            foreach (var name in choices)
            {
                menu.AddItem(new GUIContent(name), false, () => Pick(name));
            }

            menu.DropDown(anchor.worldBound);
        }
    }
}
