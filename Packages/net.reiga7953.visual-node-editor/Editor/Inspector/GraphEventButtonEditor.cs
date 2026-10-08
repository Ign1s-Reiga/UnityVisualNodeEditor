using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Editor.Inspector
{
    /// <summary>
    /// <see cref="GraphEventButton"/> のインスペクタ。送り先のグラフが決まっていれば、イベント名をグラフの Event ノードの名前から選ばせる
    /// （同じ文字列を打たせない）。設定に問題があれば、直し方を添えて出す。見た目は GraphEventButtonInspector.uss。
    /// </summary>
    [CustomEditor(typeof(GraphEventButton))]
    public sealed class GraphEventButtonEditor : UnityEditor.Editor
    {
        // GraphEventButton の private フィールド名（EditMode テストで存在を検証している）
        internal const string GraphPropertyName = "_graph";
        internal const string ActionPropertyName = "_action";
        internal const string EventNamePropertyName = "_eventName";
        internal const string SendOnClickPropertyName = "_sendOnClick";

        private const string StyleSheetPath = "VisualNodeEditor/GraphEventButtonInspector";

        private VisualElement _eventField;
        private VisualElement _hints;

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.AddToClassList("vne-event-button-inspector");
            var styleSheet = Resources.Load<StyleSheet>(StyleSheetPath);
            if (styleSheet != null)
            {
                root.styleSheets.Add(styleSheet);
            }

            var graph = serializedObject.FindProperty(GraphPropertyName);
            var action = serializedObject.FindProperty(ActionPropertyName);
            root.Add(new PropertyField(graph, "Graph"));
            root.Add(new PropertyField(action, "Action"));

            _eventField = new VisualElement();
            _eventField.AddToClassList("vne-event-button-inspector__event");
            root.Add(_eventField);
            root.Add(new PropertyField(serializedObject.FindProperty(SendOnClickPropertyName), "Send On Click"));

            _hints = new VisualElement();
            _hints.AddToClassList("vne-event-button-inspector__hints");
            root.Add(_hints);

            // グラフと操作が変わったときだけ欄を作り直す（入力中に作り直すとフォーカスが外れる）
            RebuildEventField();
            root.TrackPropertyValue(graph, _ => RebuildEventField());
            root.TrackPropertyValue(action, _ => RebuildEventField());
            root.TrackSerializedObjectValue(serializedObject, _ => RefreshHints());
            RefreshHints();
            return root;
        }

        private void RebuildEventField()
        {
            serializedObject.Update();
            _eventField.Clear();
            var eventName = serializedObject.FindProperty(EventNamePropertyName);
            var action = (GraphEventButtonAction)serializedObject.FindProperty(ActionPropertyName).enumValueIndex;
            if (action != GraphEventButtonAction.Raise)
            {
                return;
            }

            var graph = serializedObject.FindProperty(GraphPropertyName).objectReferenceValue as NodeGraphAsset;
            var choices = graph != null ? GraphEventNames.Collect(graph) : null;
            if (choices == null || choices.Count == 0)
            {
                // 一覧が作れないときは名前を直接入力する
                var text = new PropertyField(eventName, "Event");
                text.Bind(serializedObject);
                _eventField.Add(text);
                return;
            }

            // 今の値がグラフに無ければ、それも選択肢に残して（警告を出しつつ）消さない
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
                serializedObject.FindProperty(EventNamePropertyName).stringValue = evt.newValue;
                serializedObject.ApplyModifiedProperties();
            });
            _eventField.Add(dropdown);
        }

        private void RefreshHints()
        {
            if (_hints == null || target == null)
            {
                return;
            }

            serializedObject.Update();
            var button = (GraphEventButton)target;
            var clickEvent = GraphEventButton.FindClickEvent(button.gameObject);
            var graph = serializedObject.FindProperty(GraphPropertyName).objectReferenceValue as NodeGraphAsset;
            var hints = GraphEventButtonHints.Get(
                graph != null ? GraphEventNames.Collect(graph) : null,
                serializedObject.FindProperty(EventNamePropertyName).stringValue,
                (GraphEventButtonAction)serializedObject.FindProperty(ActionPropertyName).enumValueIndex,
                serializedObject.FindProperty(SendOnClickPropertyName).boolValue,
                clickEvent != null,
                GraphEventButton.CallsSend(clickEvent, button));

            _hints.Clear();
            foreach (var hint in hints)
            {
                _hints.Add(new HelpBox(hint, HelpBoxMessageType.Info));
            }
        }
    }
}
