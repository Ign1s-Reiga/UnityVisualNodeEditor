using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Editor.Inspector
{
    /// <summary>
    /// <see cref="GraphParameterText"/> のインスペクタ。グラフが決まっていれば、パラメータを Blackboard から選ぶドロップダウン（名前を打たせない）。
    /// 決まっていなければ名前の入力欄。設定に問題があれば、直し方を添えて出す。
    /// </summary>
    [CustomEditor(typeof(GraphParameterText))]
    public sealed class GraphParameterTextEditor : GameComponentEditor
    {
        // GraphParameterText の private フィールド名（EditMode テストで存在を検証している）
        internal const string GraphPropertyName = "_graph";
        internal const string ParameterPropertyName = "_parameter";
        internal const string FormatPropertyName = "_format";

        private VisualElement _parameterField;

        public override VisualElement CreateInspectorGUI()
        {
            var root = CreateRoot();
            var graph = serializedObject.FindProperty(GraphPropertyName);
            root.Add(new PropertyField(graph, "Graph"));
            _parameterField = new VisualElement();
            root.Add(_parameterField);
            root.Add(new PropertyField(serializedObject.FindProperty(FormatPropertyName), "Format"));
            AddHints(root);

            // グラフが変わったときだけパラメータの欄を作り直す（入力中に作り直すとフォーカスが外れる）
            RebuildParameterField();
            root.TrackPropertyValue(graph, _ => RebuildParameterField());
            return root;
        }

        private void RebuildParameterField()
        {
            serializedObject.Update();
            _parameterField.Clear();
            var graph = serializedObject.FindProperty(GraphPropertyName).objectReferenceValue as NodeGraphAsset;
            var names = GetParameterNames(graph);
            if (names == null || names.Count == 0)
            {
                var text = new PropertyField(serializedObject.FindProperty(ParameterPropertyName), "Parameter");
                text.Bind(serializedObject);
                _parameterField.Add(text);
                return;
            }

            // 今の値がグラフに無ければ、選択肢に残したまま（注意を出す）
            var current = serializedObject.FindProperty(ParameterPropertyName).stringValue;
            if (!string.IsNullOrEmpty(current) && !names.Contains(current))
            {
                names.Add(current);
            }

            var dropdown = new DropdownField("Parameter", names, string.IsNullOrEmpty(current) ? -1 : names.IndexOf(current))
            {
                tooltip = "The Blackboard parameter to show",
            };
            dropdown.AddToClassList(BaseField<string>.alignedFieldUssClassName);
            dropdown.RegisterValueChangedCallback(evt =>
            {
                serializedObject.Update();
                serializedObject.FindProperty(ParameterPropertyName).stringValue = evt.newValue;
                serializedObject.ApplyModifiedProperties();
            });
            _parameterField.Add(dropdown);
        }

        protected override List<string> GetHints()
        {
            var text = (GraphParameterText)target;
            var graph = serializedObject.FindProperty(GraphPropertyName).objectReferenceValue as NodeGraphAsset;
            return GameComponentHints.ForParameterText(GraphParameterText.FindTextTarget(text.gameObject) != null, GetParameterNames(graph),
                serializedObject.FindProperty(ParameterPropertyName).stringValue);
        }

        // グラフのパラメータ名（空を除き、グラフ内の順）。グラフが無ければ null
        private static List<string> GetParameterNames(NodeGraphAsset graph) =>
            graph?.Parameters.Where(p => p != null && !string.IsNullOrEmpty(p.Name)).Select(p => p.Name).Distinct().ToList();
    }
}
