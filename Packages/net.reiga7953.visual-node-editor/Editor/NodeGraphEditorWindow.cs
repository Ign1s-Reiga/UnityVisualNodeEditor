using System.Collections.Generic;
using System.Linq;
using Reiga.VisualNodeEditor.Editor.Inspector;
using Reiga.VisualNodeEditor.Editor.Search;
using Reiga.VisualNodeEditor.Editor.Views;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Editor
{
    /// <summary>ノードグラフを編集するメインウィンドウ。</summary>
    public sealed class NodeGraphEditorWindow : EditorWindow
    {
        private const string NoAssetLabel = "(no asset)";
        private const string IssueErrorClassName = "vne-issue--error";
        private const string IssueWarningClassName = "vne-issue--warning";

        // ドメインリロード後も同じアセットを開き直せるようシリアライズする
        [SerializeField] private NodeGraphAsset _asset;

        private NodeGraphView _graphView;
        private NodeSearchWindow _searchWindow;
        private NodeInspectorView _inspector;
        private ListView _issueList;
        private Label _assetNameLabel;
        private Label _issueSummaryLabel;
        private List<GraphIssue> _issues = new();

        [MenuItem("Window/Visual Node Editor")]
        public static void Open() => Open(null);

        /// <summary>ウィンドウを開き、<paramref name="asset"/> が指定されていれば読み込む。</summary>
        public static void Open(NodeGraphAsset asset)
        {
            var window = GetWindow<NodeGraphEditorWindow>("Visual Node Editor");
            if (asset != null)
            {
                window.Load(asset);
            }
        }

        // InstanceIDToObject は 6000.5 でコンパイルエラー、代替の EntityIdToObject は 6000.0 に無いため、
        // どちらにも依存しないよう、ダブルクリックで選択済みになっているアセットを使う
        [OnOpenAsset]
        private static bool OnOpenAsset(int instanceId, int line)
        {
            if (Selection.activeObject is NodeGraphAsset asset)
            {
                Open(asset);
                return true;
            }

            return false;
        }

        private void CreateGUI()
        {
            var uxml = Resources.Load<VisualTreeAsset>("VisualNodeEditor/NodeGraphEditorWindow");
            uxml?.CloneTree(rootVisualElement);

            var uss = Resources.Load<StyleSheet>("VisualNodeEditor/NodeGraphEditor");
            if (uss != null)
            {
                rootVisualElement.styleSheets.Add(uss);
            }

            _graphView = new NodeGraphView { name = "graph-view" };
            _graphView.StretchToParentSize();
            var container = rootVisualElement.Q("graph-container") ?? rootVisualElement;
            container.Add(_graphView);
            _graphView.GraphChanged += Revalidate;
            _graphView.SelectedNodeChanged += OnSelectedNodeChanged;

            _searchWindow = CreateInstance<NodeSearchWindow>();
            _searchWindow.hideFlags = HideFlags.HideAndDontSave;
            _searchWindow.Initialize(this, _graphView);
            _graphView.nodeCreationRequest = OnNodeCreationRequest;

            _inspector = new NodeInspectorView();
            (rootVisualElement.Q("inspector") ?? rootVisualElement).Add(_inspector);
            _inspector.NodeChanged += OnInspectorNodeChanged;

            _issueList = rootVisualElement.Q<ListView>("issue-list");
            if (_issueList != null)
            {
                _issueList.makeItem = () => new Label();
                _issueList.bindItem = BindIssueItem;
                _issueList.itemsSource = _issues;
                _issueList.selectionChanged += OnIssueSelected;
            }

            var saveButton = rootVisualElement.Q<ToolbarButton>("save-button");
            if (saveButton != null)
            {
                saveButton.clicked += Save;
            }

            _assetNameLabel = rootVisualElement.Q<Label>("asset-name");
            _issueSummaryLabel = rootVisualElement.Q<Label>("issue-summary");

            Refresh();
        }

        private void OnDisable()
        {
            if (_searchWindow != null)
            {
                DestroyImmediate(_searchWindow);
            }
        }

        private void Load(NodeGraphAsset asset)
        {
            _asset = asset;
            Refresh();
        }

        private void Refresh()
        {
            titleContent = new GUIContent(_asset != null ? _asset.name : "Visual Node Editor");
            if (_assetNameLabel != null)
            {
                _assetNameLabel.text = _asset != null ? AssetDatabase.GetAssetPath(_asset) : NoAssetLabel;
            }

            _inspector?.Show(null, null);
            _graphView?.Populate(_asset);
        }

        private void Revalidate()
        {
            _issues.Clear();
            if (_asset != null)
            {
                _issues.AddRange(GraphValidator.Validate(_asset));
            }

            _graphView?.ShowIssues(_issues);
            _issueList?.Rebuild();

            if (_issueSummaryLabel != null)
            {
                var errors = _issues.Count(i => i.Severity == GraphIssueSeverity.Error);
                var warnings = _issues.Count - errors;
                _issueSummaryLabel.text = _asset == null ? string.Empty
                    : _issues.Count == 0 ? "No issues"
                    : $"{errors} error(s), {warnings} warning(s)";
                _issueSummaryLabel.EnableInClassList(IssueErrorClassName, errors > 0);
                _issueSummaryLabel.EnableInClassList(IssueWarningClassName, errors == 0 && warnings > 0);
            }
        }

        private void BindIssueItem(VisualElement element, int index)
        {
            var issue = _issues[index];
            var label = (Label)element;
            label.text = issue.Message;
            label.EnableInClassList(IssueErrorClassName, issue.Severity == GraphIssueSeverity.Error);
            label.EnableInClassList(IssueWarningClassName, issue.Severity == GraphIssueSeverity.Warning);
        }

        private void OnIssueSelected(IEnumerable<object> selected)
        {
            if (selected.FirstOrDefault() is GraphIssue issue && issue.NodeId != null)
            {
                _graphView.FocusNode(issue.NodeId);
            }
        }

        private void OnSelectedNodeChanged(NodeView view)
        {
            var nodeId = view?.NodeId;
            if (_inspector != null && nodeId != _inspector.NodeId)
            {
                _inspector.Show(_asset, nodeId);
            }
        }

        private void OnInspectorNodeChanged(string nodeId)
        {
            _graphView.RefreshNode(nodeId);
            Revalidate();
        }

        private void OnNodeCreationRequest(NodeCreationContext context)
        {
            if (_asset == null)
            {
                ShowNotification(new GUIContent("Open a Node Graph asset first."));
                return;
            }

            SearchWindow.Open(new SearchWindowContext(context.screenMousePosition), _searchWindow);
        }

        private void Save()
        {
            if (_asset != null)
            {
                AssetDatabase.SaveAssetIfDirty(_asset);
            }
        }
    }
}
