using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Editor.Inspector
{
    /// <summary>
    /// <see cref="GraphDestroyTrigger"/> のインスペクタ。パラメータの変更はグラフの Blackboard から、イベント名はグラフの Event ノードの名前から選ぶ。
    /// 設定に問題があれば、直し方を添えて出す。
    /// </summary>
    [CustomEditor(typeof(GraphDestroyTrigger))]
    public sealed class GraphDestroyTriggerEditor : GameComponentEditor
    {
        // GraphDestroyTrigger の private フィールド名（EditMode テストで存在を検証している）
        internal const string GraphPropertyName = "_graph";
        internal const string ChangesPropertyName = "_changes";
        internal const string EventNamePropertyName = "_eventName";

        public override VisualElement CreateInspectorGUI()
        {
            var root = CreateRoot();
            root.Add(new PropertyField(serializedObject.FindProperty(GraphPropertyName), "Graph"));
            root.Add(new PropertyField(serializedObject.FindProperty(ChangesPropertyName), "Changes"));
            root.Add(new PropertyField(serializedObject.FindProperty(EventNamePropertyName), "Event"));
            AddHints(root);
            return root;
        }

        protected override List<string> GetHints() => GetReactionHints(serializedObject, GraphPropertyName, ChangesPropertyName, EventNamePropertyName);

        /// <summary>
        /// パラメータを変えてイベントを Raise するコンポーネントの注意（Graph Trigger 2D のインスペクタも使う）。
        /// 各フィールドは名前で読む（コンポーネントの型を知らなくてよいように）。
        /// </summary>
        public static List<string> GetReactionHints(SerializedObject serializedObject, string graphProperty, string changesProperty, string eventNameProperty)
        {
            var graph = serializedObject.FindProperty(graphProperty).objectReferenceValue as NodeGraphAsset;
            var changes = serializedObject.FindProperty(changesProperty);
            var problems = new List<string>();
            for (var i = 0; graph != null && i < changes.arraySize; i++)
            {
                var item = changes.GetArrayElementAtIndex(i);
                var problem = new ParameterChange(
                        item.FindPropertyRelative(ParameterChangeDrawer.ParameterIdPropertyName).stringValue,
                        (ParameterChangeOperation)item.FindPropertyRelative(ParameterChangeDrawer.OperationPropertyName).enumValueIndex)
                    .GetProblem(graph);
                if (problem != null)
                {
                    problems.Add(problem);
                }
            }

            return GameComponentHints.ForReaction(
                graph != null ? GraphEventNames.CollectRaisable(graph) : null,
                serializedObject.FindProperty(eventNameProperty).stringValue,
                changes.arraySize,
                problems);
        }
    }
}
