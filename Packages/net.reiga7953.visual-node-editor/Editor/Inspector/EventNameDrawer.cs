using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Editor.Inspector
{
    /// <summary>
    /// <see cref="EventNameAttribute"/> の付いた文字列フィールドを、グラフで使っているイベント名から選べる欄にする。
    /// グラフのアセットの中のフィールドなら <see cref="EventNameField"/>（そのグラフの名前）。
    /// それ以外（UI Builder の UI Toolkit のコントロールの属性など）は、同じ並びの <c>graph</c> のグラフの Raise できる名前から選べる欄。グラフが無ければ入力欄。
    /// </summary>
    [CustomPropertyDrawer(typeof(EventNameAttribute))]
    public sealed class EventNameDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                return new Label($"[EventName] only works on string fields ({property.displayName}).");
            }

            if (property.serializedObject.targetObject is NodeGraphAsset graph)
            {
                return new EventNameField(property, graph);
            }

            var label = string.IsNullOrEmpty(preferredLabel) ? property.displayName : preferredLabel;
            return new ChoiceTextField(property, label,
                () =>
                {
                    var sibling = ChoiceTextField.FindSiblingGraph(property);
                    return sibling != null ? GraphEventNames.CollectRaisable(sibling) : new List<string>();
                },
                "Set the graph to pick one of its events");
        }
    }
}
