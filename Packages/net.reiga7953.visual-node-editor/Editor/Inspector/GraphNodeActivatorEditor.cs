using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Editor.Inspector
{
    /// <summary>
    /// <see cref="GraphNodeActivator"/> のインスペクタ。ノードは、グラフの待機ノードとコンテナから選ぶドロップダウン（場所付き。例: <c>Round › Playing</c>）。
    /// 設定に問題があれば、直し方を添えて出す。
    /// </summary>
    [CustomEditor(typeof(GraphNodeActivator))]
    public sealed class GraphNodeActivatorEditor : GameComponentEditor
    {
        // GraphNodeActivator の private フィールド名（EditMode テストで存在を検証している）
        internal const string GraphPropertyName = "_graph";
        internal const string NodeIdPropertyName = "_nodeId";
        internal const string TargetsPropertyName = "_targets";

        private const string MissingLabel = "(missing)";
        private const string ChooseLabel = "(choose)";

        private VisualElement _nodeField;

        public override VisualElement CreateInspectorGUI()
        {
            var root = CreateRoot();
            var graph = serializedObject.FindProperty(GraphPropertyName);
            root.Add(new PropertyField(graph, "Graph"));
            _nodeField = new VisualElement();
            root.Add(_nodeField);
            root.Add(new PropertyField(serializedObject.FindProperty(TargetsPropertyName), "Show While In Node"));
            AddHints(root);

            // グラフが変わったときだけノードの欄を作り直す
            RebuildNodeField();
            root.TrackPropertyValue(graph, _ => RebuildNodeField());
            return root;
        }

        private void RebuildNodeField()
        {
            serializedObject.Update();
            _nodeField.Clear();
            var graph = serializedObject.FindProperty(GraphPropertyName).objectReferenceValue as NodeGraphAsset;
            if (graph == null)
            {
                return;
            }

            var choices = ActivatorNodeChoices.Get(graph);
            var currentId = serializedObject.FindProperty(NodeIdPropertyName).stringValue;
            var current = choices.FindIndex(choice => choice.Key == currentId);
            var labels = choices.Select(choice => choice.Value).ToList();
            var offset = current < 0 ? 1 : 0;
            if (current < 0)
            {
                labels.Insert(0, string.IsNullOrEmpty(currentId) ? ChooseLabel : MissingLabel);
            }

            // 先頭の "(choose)" などと同じ表示のノードがあっても、別の項目として選べるように
            var dropdown = new DropdownField("Node", DropdownLabels.MakeUnique(labels), current < 0 ? 0 : current)
            {
                tooltip = "Show the targets while the graph is on this node (for a container: anywhere inside it)",
            };
            dropdown.AddToClassList(BaseField<string>.alignedFieldUssClassName);
            dropdown.RegisterValueChangedCallback(_ =>
            {
                var chosen = dropdown.index - offset;
                if (chosen < 0 || chosen >= choices.Count)
                {
                    return;
                }

                serializedObject.Update();
                serializedObject.FindProperty(NodeIdPropertyName).stringValue = choices[chosen].Key;
                serializedObject.ApplyModifiedProperties();
                RebuildNodeField();
            });
            _nodeField.Add(dropdown);
        }

        protected override List<string> GetHints()
        {
            var activator = (GraphNodeActivator)target;
            var graph = serializedObject.FindProperty(GraphPropertyName).objectReferenceValue as NodeGraphAsset;
            var nodeId = serializedObject.FindProperty(NodeIdPropertyName).stringValue;
            var targets = serializedObject.FindProperty(TargetsPropertyName);
            var targetsSelfOrParent = false;
            for (var i = 0; i < targets.arraySize; i++)
            {
                var targetObject = targets.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
                targetsSelfOrParent |= targetObject != null && !GraphNodeActivator.CanTarget(activator.gameObject, targetObject);
            }

            return GameComponentHints.ForActivator(graph != null, nodeId, graph != null && graph.FindNode(nodeId) != null,
                targets.arraySize, targetsSelfOrParent);
        }
    }
}
