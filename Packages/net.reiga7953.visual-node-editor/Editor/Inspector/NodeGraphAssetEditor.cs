using System.Linq;
using Reiga.VisualNodeEditor.Editor.Issues;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Editor.Inspector
{
    /// <summary>
    /// グラフアセットの Inspector。生のリスト（ノード・パラメータなど）を出さず、概要と「開く」ボタンだけを表示する。
    /// 既定の Inspector ではリストの「+」で内部 ID ごと要素を複製でき、Blackboard や同期が別の要素を変更してしまうため。
    /// 編集はグラフウィンドウで行う。レイアウトは NodeGraphAssetInspector.uxml / .uss。
    /// </summary>
    [CustomEditor(typeof(NodeGraphAsset))]
    public sealed class NodeGraphAssetEditor : UnityEditor.Editor
    {
        private const string UxmlPath = "VisualNodeEditor/NodeGraphAssetInspector";
        private const string UssPath = "VisualNodeEditor/NodeGraphAssetInspector";
        private const string IssueErrorClassName = "vne-asset-inspector__issues--error";
        private const string IssueWarningClassName = "vne-asset-inspector__issues--warning";

        private Label _issuesLabel;
        private VisualElement _counts;
        private VisualElement _parameters;

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            Resources.Load<VisualTreeAsset>(UxmlPath)?.CloneTree(root);
            var styleSheet = Resources.Load<StyleSheet>(UssPath);
            if (styleSheet != null)
            {
                root.styleSheets.Add(styleSheet);
            }

            var openButton = root.Q<Button>("open-button");
            if (openButton != null)
            {
                openButton.clicked += () => NodeGraphEditorWindow.Open((NodeGraphAsset)target);
            }

            _issuesLabel = root.Q<Label>("issues");
            _counts = root.Q("counts");
            _parameters = root.Q("parameters");
            Refresh();

            // グラフウィンドウで編集されたら、表示中の Inspector も更新する
            root.TrackSerializedObjectValue(serializedObject, _ => Refresh());
            return root;
        }

        private void Refresh()
        {
            var graph = (NodeGraphAsset)target;
            if (graph == null)
            {
                return;
            }

            if (_issuesLabel != null)
            {
                var issues = GraphIssues.Collect(graph);
                var errors = issues.Count(i => i.Severity == GraphIssueSeverity.Error);
                var warnings = issues.Count - errors;
                var state = IssueStatus.GetState(errors, warnings);
                _issuesLabel.text = IssueStatus.GetText(errors, warnings);
                _issuesLabel.EnableInClassList(IssueErrorClassName, state == IssueDisplayState.Error);
                _issuesLabel.EnableInClassList(IssueWarningClassName, state == IssueDisplayState.Warning);
            }

            if (_counts != null)
            {
                _counts.Clear();
                foreach (var (label, count) in GraphSummary.GetCounts(graph))
                {
                    _counts.Add(CreateRow($"{label}: {count}", "vne-asset-inspector__count"));
                }
            }

            if (_parameters != null)
            {
                _parameters.Clear();
                var parameters = graph.Parameters.Where(p => p != null).ToList();
                if (parameters.Count == 0)
                {
                    _parameters.Add(CreateRow("No parameters", "vne-asset-inspector__empty"));
                }

                foreach (var parameter in parameters)
                {
                    _parameters.Add(CreateRow(GraphSummary.DescribeParameter(parameter), "vne-asset-inspector__parameter"));
                }
            }
        }

        private static Label CreateRow(string text, string className)
        {
            var label = new Label(text);
            label.AddToClassList(className);
            return label;
        }
    }
}
