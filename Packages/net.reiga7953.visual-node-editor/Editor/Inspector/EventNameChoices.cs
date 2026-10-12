using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Editor.Inspector
{
    /// <summary>
    /// コンポーネントのインスペクタで、送るイベント名を選ぶ欄（Graph Event Button・Graph Event Key が共有する）。
    /// グラフに Raise できる Event があれば、その名前から選ぶドロップダウン（同じ文字列を打たせない）。無ければ名前の入力欄。
    /// </summary>
    internal static class EventNameChoices
    {
        /// <summary>
        /// <paramref name="serializedObject"/> の <paramref name="propertyName"/>（文字列）を編集する欄。<paramref name="graph"/> が null なら入力欄。
        /// 今の値がグラフに無ければ、選択肢に残したまま（消さない。注意はヒントで出す）。
        /// </summary>
        internal static VisualElement CreateField(SerializedObject serializedObject, string propertyName, NodeGraphAsset graph)
        {
            var eventName = serializedObject.FindProperty(propertyName);
            var choices = graph != null ? GraphEventNames.CollectRaisable(graph) : null;
            if (choices == null || choices.Count == 0)
            {
                // 一覧が作れないときは名前を直接入力する
                var text = new PropertyField(eventName, "Event");
                text.Bind(serializedObject);
                return text;
            }

            var current = eventName.stringValue;
            if (!string.IsNullOrEmpty(current) && !choices.Contains(current))
            {
                choices.Add(current);
            }

            var dropdown = new DropdownField("Event", choices, string.IsNullOrEmpty(current) ? -1 : choices.IndexOf(current));
            dropdown.AddToClassList(BaseField<string>.alignedFieldUssClassName);
            dropdown.RegisterValueChangedCallback(evt =>
            {
                serializedObject.Update();
                serializedObject.FindProperty(propertyName).stringValue = evt.newValue;
                serializedObject.ApplyModifiedProperties();
            });
            return dropdown;
        }
    }
}
