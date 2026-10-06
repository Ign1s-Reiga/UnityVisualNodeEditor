using System.Collections.Generic;
using System.Linq;
using Reiga.VisualNodeEditor.Editor.Inspector;
using Reiga.VisualNodeEditor.Editor.Issues;
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
        private const string DirtyMark = " *";
        private const string IssueErrorClassName = "vne-issue--error";
        private const string IssueWarningClassName = "vne-issue--warning";
        private const string IssueListHiddenClassName = "vne-issue-list--hidden";
        private const long AssetLabelRefreshIntervalMs = 500;

        // ドメインリロード後も同じアセットを開き直せるようシリアライズする
        [SerializeField] private NodeGraphAsset _asset;

        private NodeGraphView _graphView;
        private NodeSearchWindow _searchWindow;
        private NodeInspectorView _inspector;
        private ListView _issueList;
        private ToolbarToggle _issueToggle;
        private Label _assetNameLabel;
        private readonly List<GraphIssue> _issues = new();
        private readonly HashSet<string> _knownErrorKeys = new();
        private bool _resetIssueBaseline = true;

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
            // グラフとインスペクタの最小幅（USS）+ 境界線が収まる大きさ。これより狭くするとはみ出す
            minSize = new Vector2(480f, 240f);

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
                _issueList.makeItem = MakeIssueItem;
                _issueList.bindItem = BindIssueItem;
                _issueList.itemsSource = _issues;
            }

            _issueToggle = rootVisualElement.Q<ToolbarToggle>("issue-toggle");
            _issueToggle?.RegisterValueChangedCallback(_ => UpdateIssueListVisibility());

            var saveButton = rootVisualElement.Q<ToolbarButton>("save-button");
            if (saveButton != null)
            {
                saveButton.clicked += Save;
            }

            _assetNameLabel = rootVisualElement.Q<Label>("asset-name");
            _assetNameLabel?.RegisterCallback<ClickEvent>(_ => PingAsset());

            // 未保存状態は Ctrl+S やアセット側の変更でも変わるため、定期的に確認する
            rootVisualElement.schedule.Execute(UpdateAssetLabel).Every(AssetLabelRefreshIntervalMs);

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
            UpdateAssetLabel();

            // アセットを開いた時点で既にあるエラーでは一覧を自動で開かない
            _resetIssueBaseline = true;
            _inspector?.Show(null, null);
            _graphView?.Populate(_asset);
        }

        private void UpdateAssetLabel()
        {
            if (_assetNameLabel == null)
            {
                return;
            }

            if (_asset == null)
            {
                _assetNameLabel.text = NoAssetLabel;
                _assetNameLabel.tooltip = string.Empty;
                return;
            }

            _assetNameLabel.text = _asset.name + (EditorUtility.IsDirty(_asset) ? DirtyMark : string.Empty);
            _assetNameLabel.tooltip = AssetDatabase.GetAssetPath(_asset);
        }

        private void PingAsset()
        {
            if (_asset != null)
            {
                EditorGUIUtility.PingObject(_asset);
            }
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
            UpdateIssueToggle();
            UpdateAssetLabel();
        }

        private void UpdateIssueToggle()
        {
            var errorKeys = _issues.Where(i => i.Severity == GraphIssueSeverity.Error).Select(IssueStatus.GetKey).ToList();
            var hasNewErrors = IssueStatus.HasNewErrors(_knownErrorKeys, errorKeys);
            _knownErrorKeys.Clear();
            _knownErrorKeys.UnionWith(errorKeys);

            if (_issueToggle == null)
            {
                return;
            }

            var errors = errorKeys.Count;
            var warnings = _issues.Count - errors;
            var state = IssueStatus.GetState(errors, warnings);
            _issueToggle.text = _asset == null ? string.Empty : IssueStatus.GetText(errors, warnings);
            _issueToggle.EnableInClassList(IssueErrorClassName, state == IssueDisplayState.Error);
            _issueToggle.EnableInClassList(IssueWarningClassName, state == IssueDisplayState.Warning);

            if (hasNewErrors && !_resetIssueBaseline)
            {
                _issueToggle.value = true;
            }

            _resetIssueBaseline = false;
            UpdateIssueListVisibility();
        }

        private void UpdateIssueListVisibility()
        {
            // 問題が無いときはトグルの状態に関わらず一覧を出さない（下部パネルが場所を取らないように）
            var visible = _issueToggle != null && _issueToggle.value && _issues.Count > 0;
            _issueList?.EnableInClassList(IssueListHiddenClassName, !visible);
        }

        private VisualElement MakeIssueItem()
        {
            var label = new Label();
            label.AddToClassList("vne-issue-list__item");

            // 選択状態に頼らずクリックごとに反応させる（同じ項目を続けてクリックしてもフォーカスし直せる）
            label.RegisterCallback<ClickEvent>(_ =>
            {
                if (label.userData is GraphIssue issue && issue.NodeId != null)
                {
                    _graphView.FocusNode(issue.NodeId);
                }
            });
            return label;
        }

        private void BindIssueItem(VisualElement element, int index)
        {
            var issue = _issues[index];
            var label = (Label)element;
            label.userData = issue;
            label.text = issue.Message;
            label.EnableInClassList(IssueErrorClassName, issue.Severity == GraphIssueSeverity.Error);
            label.EnableInClassList(IssueWarningClassName, issue.Severity == GraphIssueSeverity.Warning);
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
                UpdateAssetLabel();
            }
        }
    }
}
