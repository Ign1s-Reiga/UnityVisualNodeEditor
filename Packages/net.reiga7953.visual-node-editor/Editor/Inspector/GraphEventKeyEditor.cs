using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Editor.Inspector
{
    /// <summary>
    /// <see cref="GraphEventKey"/> のインスペクタ。送り先のグラフが決まっていれば、イベント名をグラフの Event ノードの名前から選ばせる
    /// （Graph Event Button と同じ欄）。設定に問題があれば、直し方を添えて出す。
    /// </summary>
    [CustomEditor(typeof(GraphEventKey))]
    public sealed class GraphEventKeyEditor : GameComponentEditor
    {
        // GraphEventKey の private フィールド名（EditMode テストで存在を検証している）
        internal const string GraphPropertyName = "_graph";
        internal const string KeyPropertyName = "_key";
        internal const string ActionPropertyName = "_action";
        internal const string EventNamePropertyName = "_eventName";

        private VisualElement _eventField;

        public override VisualElement CreateInspectorGUI()
        {
            var root = CreateRoot();
            var graph = serializedObject.FindProperty(GraphPropertyName);
            var action = serializedObject.FindProperty(ActionPropertyName);
            root.Add(new PropertyField(graph, "Graph"));
            root.Add(new PropertyField(serializedObject.FindProperty(KeyPropertyName), "Key"));
            root.Add(new PropertyField(action, "Action"));
            _eventField = new VisualElement();
            root.Add(_eventField);
            AddHints(root);

            // グラフと操作が変わったときだけ欄を作り直す（入力中に作り直すとフォーカスが外れる）
            RebuildEventField();
            root.TrackPropertyValue(graph, _ => RebuildEventField());
            root.TrackPropertyValue(action, _ => RebuildEventField());
            return root;
        }

        private void RebuildEventField()
        {
            serializedObject.Update();
            _eventField.Clear();
            if ((GraphEventKeyAction)serializedObject.FindProperty(ActionPropertyName).enumValueIndex != GraphEventKeyAction.Raise)
            {
                return;
            }

            var graph = serializedObject.FindProperty(GraphPropertyName).objectReferenceValue as NodeGraphAsset;
            _eventField.Add(EventNameChoices.CreateField(serializedObject, EventNamePropertyName, graph));
        }

        protected override List<string> GetHints()
        {
            var graph = serializedObject.FindProperty(GraphPropertyName).objectReferenceValue as NodeGraphAsset;
            return GameComponentHints.ForEventKey(
                graph != null ? GraphEventNames.CollectRaisable(graph) : null,
                serializedObject.FindProperty(EventNamePropertyName).stringValue,
                (GraphEventKeyAction)serializedObject.FindProperty(ActionPropertyName).enumValueIndex,
                (KeyCode)serializedObject.FindProperty(KeyPropertyName).intValue);
        }
    }
}
