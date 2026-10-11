using UnityEditor;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Editor.Inspector
{
    /// <summary>
    /// <see cref="EventNameAttribute"/> の付いた文字列フィールドを、グラフで使っているイベント名から選べる欄（<see cref="EventNameField"/>）にする。
    /// グラフのアセットの中のフィールドでなければ（選ぶ元が無いので）普通の入力欄。
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

            return property.serializedObject.targetObject is NodeGraphAsset graph
                ? new EventNameField(property, graph)
                : new UnityEditor.UIElements.PropertyField(property);
        }
    }
}
