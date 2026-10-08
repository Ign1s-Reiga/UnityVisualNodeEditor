using System;
using System.Collections.Generic;
using System.Linq;
using Reiga.VisualNodeEditor.Editor.Build;
using Reiga.VisualNodeEditor.Editor.Inspector;
using Reiga.VisualNodeEditor.Editor.Issues;
using Reiga.VisualNodeEditor.Editor.Scenes;
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
        private const string LevelBarHiddenClassName = "vne-level-bar--hidden";
        private const string RootLevelLabel = "Root";
        private const long AssetLabelRefreshIntervalMs = 500;

        // ドメインリロード後も同じアセットを開き直せるようシリアライズする
        [SerializeField] private NodeGraphAsset _asset;

        // 表示中の階層（コンテナの ID、空ならルート）もドメインリロード後に保つ
        [SerializeField] private string _levelContainerId = string.Empty;

        // View メニューの表示状態もドメインリロード後に保つ
        [SerializeField] private bool _miniMapVisible;
        [SerializeField] private bool _blackboardVisible = true;
        [SerializeField] private bool _snapToGrid;

        private NodeGraphView _graphView;
        private NodeSearchWindow _searchWindow;
        private NodeInspectorView _inspector;
        private ListView _issueList;
        private ToolbarToggle _issueToggle;
        private ToolbarSearchField _searchField;
        private Label _searchCountLabel;
        private List<string> _searchMatches = new();
        private int _searchIndex = -1;
        private Label _assetNameLabel;
        private VisualElement _levelBar;
        private ToolbarBreadcrumbs _levelBreadcrumbs;
        private readonly List<GraphIssue> _issues = new();
        private readonly HashSet<string> _knownErrorKeys = new();
        private bool _resetIssueBaseline = true;

        // Play 中、開いているグラフを実行している Runner（強調表示用）
        private GraphRunner _observedRunner;

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

        // 開こうとしているアセットそのもので判定する（選択中のアセットで判定すると、無関係なダブルクリックまで奪ってしまう）。
        // 6000.5 から OnOpenAsset は EntityId を渡す形になり、int 版の API はコンパイルエラーになる。
        // EntityId 自体は 6000.3 からあるが、6000.3 / 6000.4 の OnOpenAsset はまだ int を渡す。
#if UNITY_6000_5_OR_NEWER
        [OnOpenAsset]
        private static bool OnOpenAsset(EntityId entityId, int line) =>
            TryOpen(EditorUtility.EntityIdToObject(entityId));
#else
        [OnOpenAsset]
        private static bool OnOpenAsset(int instanceId, int line) =>
            TryOpen(EditorUtility.InstanceIDToObject(instanceId));
#endif

        private static bool TryOpen(UnityEngine.Object opened)
        {
            if (opened is NodeGraphAsset asset)
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
            _graphView.LevelChanged += OnLevelChanged;
            _levelBar = rootVisualElement.Q("level-bar");
            _levelBreadcrumbs = rootVisualElement.Q<ToolbarBreadcrumbs>("level-breadcrumbs");

            _searchWindow = CreateInstance<NodeSearchWindow>();
            _searchWindow.hideFlags = HideFlags.HideAndDontSave;
            _searchWindow.Initialize(this, _graphView);
            _graphView.nodeCreationRequest = OnNodeCreationRequest;

            _inspector = new NodeInspectorView();
            (rootVisualElement.Q("inspector") ?? rootVisualElement).Add(_inspector);
            _inspector.NodeChanged += OnInspectorNodeChanged;
            _inspector.StructureChanged += OnInspectorStructureChanged;

            _issueList = rootVisualElement.Q<ListView>("issue-list");
            if (_issueList != null)
            {
                _issueList.makeItem = MakeIssueItem;
                _issueList.bindItem = BindIssueItem;
                _issueList.itemsSource = _issues;
            }

            _searchField = rootVisualElement.Q<ToolbarSearchField>("node-search");
            _searchCountLabel = rootVisualElement.Q<Label>("search-count");
            if (_searchField != null)
            {
                _searchField.RegisterValueChangedCallback(_ => RefreshSearch(resetIndex: true));
                // 入力欄が Enter / Esc を処理する前に受け取る
                _searchField.RegisterCallback<KeyDownEvent>(OnSearchKeyDown, TrickleDown.TrickleDown);
            }

            _issueToggle = rootVisualElement.Q<ToolbarToggle>("issue-toggle");
            _issueToggle?.RegisterValueChangedCallback(_ => UpdateIssueListVisibility());

            var saveButton = rootVisualElement.Q<ToolbarButton>("save-button");
            if (saveButton != null)
            {
                saveButton.clicked += Save;
            }

            var frameAllButton = rootVisualElement.Q<ToolbarButton>("frame-all-button");
            if (frameAllButton != null)
            {
                frameAllButton.clicked += () => _graphView.FrameAll();
            }

            var frameSelectionButton = rootVisualElement.Q<ToolbarButton>("frame-selection-button");
            if (frameSelectionButton != null)
            {
                frameSelectionButton.clicked += _graphView.FrameSelectionOrAll;
            }

            _graphView.MiniMapVisible = _miniMapVisible;
            _graphView.BlackboardVisible = _blackboardVisible;
            _graphView.SnapToGrid = _snapToGrid;
            var viewMenu = rootVisualElement.Q<ToolbarMenu>("view-menu");
            if (viewMenu != null)
            {
                AddViewToggle(viewMenu, "MiniMap", () => _miniMapVisible, value =>
                {
                    _miniMapVisible = value;
                    _graphView.MiniMapVisible = value;
                });
                AddViewToggle(viewMenu, "Blackboard", () => _blackboardVisible, value =>
                {
                    _blackboardVisible = value;
                    _graphView.BlackboardVisible = value;
                });
                AddViewToggle(viewMenu, "Snap to Grid", () => _snapToGrid, value =>
                {
                    _snapToGrid = value;
                    _graphView.SnapToGrid = value;
                });
            }

            var buildSettingsButton = rootVisualElement.Q<ToolbarButton>("build-settings-button");
            if (buildSettingsButton != null)
            {
                buildSettingsButton.clicked += AddScenesToBuildSettings;
            }

            // Build Settings がウィンドウの外で変わっても、警告をすぐ更新する
            EditorBuildSettings.sceneListChanged += Revalidate;

            GraphRunner.Started += OnRunnerStarted;
            GraphRunner.Stopped += OnRunnerStopped;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            _assetNameLabel = rootVisualElement.Q<Label>("asset-name");
            _assetNameLabel?.RegisterCallback<ClickEvent>(_ => PingAsset());

            // 未保存状態は Ctrl+S やアセット側の変更でも変わるため、定期的に確認する
            rootVisualElement.schedule.Execute(UpdateAssetLabel).Every(AssetLabelRefreshIntervalMs);

            Refresh();
        }

        private void OnDisable()
        {
            EditorBuildSettings.sceneListChanged -= Revalidate;
            GraphRunner.Started -= OnRunnerStarted;
            GraphRunner.Stopped -= OnRunnerStopped;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            StopObservingRunner();

            if (_searchWindow != null)
            {
                DestroyImmediate(_searchWindow);
            }
        }

        private void Load(NodeGraphAsset asset)
        {
            // 別のアセットはルートから開く（同じアセットを開き直したときは今の階層のまま）
            if (asset != _asset)
            {
                _levelContainerId = string.Empty;
            }

            _asset = asset;
            Refresh();
        }

        private void Refresh()
        {
            titleContent = new GUIContent(_asset != null ? _asset.name : "Visual Node Editor");

            // エディタを閉じている間のシーン改名などは Postprocessor が拾えない場合があるので、開くときにも合わせる
            if (_asset != null && SceneReferenceSync.Resync(_asset, AssetDatabase.GUIDToAssetPath))
            {
                EditorUtility.SetDirty(_asset);
            }

            UpdateAssetLabel();

            // アセットを開いた時点で既にあるエラーでは一覧を自動で開かない
            _resetIssueBaseline = true;
            _inspector?.Show(null, null);
            _graphView?.Populate(_asset, _levelContainerId);
            ObserveRunnerForAsset();
        }

        private void OnLevelChanged()
        {
            _levelContainerId = _graphView.CurrentContainerId;
            UpdateBreadcrumbs();
        }

        /// <summary>パンくず（Root > Stage > …）を表示中の階層に合わせる。上の階層をクリックするとそこへ戻る。ルートでは隠す。</summary>
        private void UpdateBreadcrumbs()
        {
            if (_levelBreadcrumbs == null || _graphView == null)
            {
                return;
            }

            var path = _graphView.LevelPath;
            _levelBar?.EnableInClassList(LevelBarHiddenClassName, path.Count == 0);
            _levelBreadcrumbs.Clear();
            _levelBreadcrumbs.PushItem(RootLevelLabel, () => _graphView.EnterLevel(string.Empty));
            foreach (var containerId in path)
            {
                var container = _asset != null ? _asset.FindNode(containerId) : null;
                var label = container != null
                    ? NodeDisplay.ResolveTitle(container.Title, NodeDisplay.GetTypeDisplayName(container.GetType()))
                    : containerId;
                _levelBreadcrumbs.PushItem(label, () => _graphView.EnterLevel(containerId));
            }
        }

        /// <summary>開いているグラフを実行中の Runner があれば、その現在のノードを強調し、遷移を追う。</summary>
        private void ObserveRunnerForAsset()
        {
            StopObservingRunner();
            if (_asset == null || _graphView == null)
            {
                return;
            }

            var runner = GraphRunner.Running.FirstOrDefault(r => r.Graph == _asset);
            if (runner == null)
            {
                return;
            }

            _observedRunner = runner;
            runner.NodeEntered += OnObservedNodeEntered;
            _graphView.SetRunningNode(runner.Current?.Id);
        }

        private void StopObservingRunner()
        {
            if (_observedRunner != null)
            {
                _observedRunner.NodeEntered -= OnObservedNodeEntered;
                _observedRunner = null;
            }

            _graphView?.SetRunningNode(null);
        }

        private void OnObservedNodeEntered(NodeData node) => _graphView?.SetRunningNode(node.Id);

        private void OnRunnerStarted(GraphRunner runner)
        {
            if (_observedRunner == null && runner.Graph == _asset)
            {
                ObserveRunnerForAsset();
            }
        }

        private void OnRunnerStopped(GraphRunner runner)
        {
            if (runner == _observedRunner)
            {
                // 同じグラフを動かす別の Runner があれば、そちらに切り替える
                ObserveRunnerForAsset();
            }
        }

        private void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode || change == PlayModeStateChange.EnteredEditMode)
            {
                StopObservingRunner();
            }
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

        /// <summary>View メニューにチェック付きの切り替え項目を足す。</summary>
        private static void AddViewToggle(ToolbarMenu menu, string label, Func<bool> isOn, Action<bool> set)
        {
            menu.menu.AppendAction(label,
                _ => set(!isOn()),
                _ => isOn() ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
        }

        private void AddScenesToBuildSettings()
        {
            ShowNotification(new GUIContent(BuildSettingsSync.Apply(_asset)));
            Revalidate();
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
            _issues.AddRange(GraphIssues.Collect(_asset));

            _graphView?.ShowIssues(_issues);
            _issueList?.Rebuild();
            UpdateIssueToggle();

            // ノードの追加・削除・改名で一致するノードが変わるので、検索結果も更新する
            RefreshSearch(resetIndex: false);
            UpdateAssetLabel();

            // コンテナの改名（インスペクタ・Undo）をパンくずにも反映する
            UpdateBreadcrumbs();
        }

        private void RefreshSearch(bool resetIndex)
        {
            if (_graphView == null)
            {
                return;
            }

            var query = _searchField?.value ?? string.Empty;
            _searchMatches = _graphView.ApplySearch(query);
            if (resetIndex || _searchIndex >= _searchMatches.Count)
            {
                _searchIndex = -1;
            }

            UpdateSearchCount(query);
        }

        private void OnSearchKeyDown(KeyDownEvent evt)
        {
            switch (evt.keyCode)
            {
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    _searchIndex = NodeSearch.NextIndex(_searchIndex, _searchMatches.Count);
                    if (_searchIndex >= 0)
                    {
                        _graphView.FocusNode(_searchMatches[_searchIndex]);
                    }

                    UpdateSearchCount(_searchField.value);
                    evt.StopPropagation();
                    break;
                case KeyCode.Escape:
                    _searchField.value = string.Empty;
                    evt.StopPropagation();
                    break;
            }
        }

        private void UpdateSearchCount(string query)
        {
            if (_searchCountLabel != null)
            {
                _searchCountLabel.text = NodeSearch.GetCountText(query, _searchMatches.Count, _searchIndex);
            }
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

        /// <summary>
        /// コンテナの出口の編集などでポートが変わったら、グラフを作り直して同じノードを選び直す（インスペクタも作り直される）。
        /// 編集中の UI（一覧の並べ替えなど）のコールバックの中で自分を壊さないよう、イベントの後で行う。
        /// </summary>
        private void OnInspectorStructureChanged(string nodeId)
        {
            rootVisualElement.schedule.Execute(() =>
            {
                if (_graphView == null || _asset == null)
                {
                    return;
                }

                // 名前欄からフォーカスを外すクリックで別のノードを選んでいたら、そちらの選択を保つ
                var selected = _inspector?.NodeId ?? nodeId;
                _graphView.Populate(_asset);
                _graphView.SelectNode(selected);
            });
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
