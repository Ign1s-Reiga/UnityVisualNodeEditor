using System;
using System.Collections.Generic;
using System.Linq;
using Reiga.VisualNodeEditor.Editor.Behaviours;
using Reiga.VisualNodeEditor.Editor.Build;
using Reiga.VisualNodeEditor.Editor.Debugging;
using Reiga.VisualNodeEditor.Editor.Inspector;
using Reiga.VisualNodeEditor.Editor.Issues;
using Reiga.VisualNodeEditor.Editor.Mcp;
using Reiga.VisualNodeEditor.Editor.Play;
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
        [SerializeField] private bool _nodeTreeVisible = true;

        private NodeGraphView _graphView;
        private NodeTreePanel _nodeTree;
        private TwoPaneSplitView _body;
        private NodeSearchWindow _searchWindow;
        private NodeInspectorView _inspector;
        private RuntimePanel _runtimePanel;
        private ListView _issueList;
        private ToolbarToggle _issueToggle;
        private ToolbarSearchField _searchField;
        private Label _searchCountLabel;
        private List<string> _searchMatches = new();
        private int _searchIndex = -1;
        private Label _assetNameLabel;
        private ToolbarButton _playButton;
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
            // ノードツリー・グラフ・インスペクタの最小幅（USS）+ 境界線が収まる大きさ。これより狭くするとはみ出す
            minSize = new Vector2(600f, 240f);

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
            _graphView.ConnectedNodeRequested += OnConnectedNodeRequested;
            _graphView.NotificationRequested += message => ShowNotification(new GUIContent(message));

            // 左のノードツリー: クリックでそのノードへ（別の階層なら開いて）、ダブルクリックでコンテナの中へ
            _nodeTree = new NodeTreePanel();
            rootVisualElement.Q("node-tree-pane")?.Add(_nodeTree);
            _nodeTree.NodeSelected += nodeId => _graphView.FocusNode(nodeId);
            _nodeTree.NodeOpened += nodeId => _graphView.EnterLevel(nodeId);
            _body = rootVisualElement.Q<TwoPaneSplitView>("body");
            SetNodeTreeVisible(_nodeTreeVisible);

            _inspector = new NodeInspectorView();
            var inspectorPane = rootVisualElement.Q("inspector") ?? rootVisualElement;
            inspectorPane.Add(_inspector);

            // Play 中だけ、インスペクタの上に「Now running」（今いるノードと、そこから起こせる操作）を出す
            _runtimePanel = new RuntimePanel();
            inspectorPane.Insert(0, _runtimePanel);
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
                AddViewToggle(viewMenu, "Node Tree", () => _nodeTreeVisible, value =>
                {
                    _nodeTreeVisible = value;
                    SetNodeTreeVisible(value);
                });
            }

            _playButton = rootVisualElement.Q<ToolbarButton>("play-button");
            if (_playButton != null)
            {
                _playButton.clicked += PlayGraph;
                UpdatePlayButton();
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

            // Create Script… で作ったスクリプトが、コンパイル後にノードへ追加されたとき
            NodeBehaviourScriptCreator.BehaviourAdded += OnBehaviourScriptAdded;

            // MCP のツールがグラフを書き換えたとき
            GraphEdits.Edited += OnGraphEditedByMcp;

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
            NodeBehaviourScriptCreator.BehaviourAdded -= OnBehaviourScriptAdded;
            GraphEdits.Edited -= OnGraphEditedByMcp;
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
            _nodeTree?.ShowLevel(_levelContainerId);
        }

        /// <summary>左のノードツリーを出す・畳む（View メニューの Node Tree）。</summary>
        private void SetNodeTreeVisible(bool visible)
        {
            if (_body == null)
            {
                return;
            }

            if (visible)
            {
                _body.UnCollapse();
            }
            else
            {
                _body.CollapseChild(0);
            }
        }

        /// <summary>Play 中に実行中のノードを、グラフとノードツリーの両方で強調する（null で消す）。</summary>
        private void ShowRunningNode(string nodeId)
        {
            _graphView?.SetRunningNode(nodeId);
            _nodeTree?.ShowRunning(nodeId);
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
            runner.ParameterChanged += OnObservedParameterChanged;
            ShowRunningNode(runner.Current?.Id);
            _graphView.ClearTrail();
            _graphView.Blackboard.ShowRuntimeValues(runner);
            _runtimePanel?.Show(runner);
        }

        private void StopObservingRunner()
        {
            if (_observedRunner != null)
            {
                _observedRunner.NodeEntered -= OnObservedNodeEntered;
                _observedRunner.ParameterChanged -= OnObservedParameterChanged;
                _observedRunner = null;
            }

            ShowRunningNode(null);
            _graphView?.Blackboard.ShowRuntimeValues(null);
            _runtimePanel?.Show(null);
        }

        private void OnObservedNodeEntered(NodeData node)
        {
            ShowRunningNode(node.Id);
            _runtimePanel?.Refresh();
        }

        private void OnObservedParameterChanged(string parameterName) => _graphView?.Blackboard.RefreshRuntimeValues();

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

                // Play を終えたら軌跡も消す（編集中に前回の実行の跡が残らないように）
                _graphView?.ClearTrail();
            }

            UpdatePlayButton();
        }

        /// <summary>ツールバーの Play: シーンと Graph Runner を用意して Play に入る（Play 中なら止める）。</summary>
        private void PlayGraph()
        {
            PlaySetup.Run(_asset, message => ShowNotification(new GUIContent(message)));

            // Runner を追加したら「Runner が無い」警告を消す
            Revalidate();
        }

        private void UpdatePlayButton()
        {
            if (_playButton != null)
            {
                _playButton.text = EditorApplication.isPlayingOrWillChangePlaymode ? "Stop" : "Play";
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

            // ノードの追加・削除・改名・階層の変化をノードツリーにも反映する（変わっていなければ作り直さない）
            _nodeTree?.Show(_asset);
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
            var row = new VisualElement();
            row.AddToClassList("vne-issue-list__item");
            var label = new Label();
            label.AddToClassList("vne-issue-list__message");
            var fix = new Button();
            fix.AddToClassList("vne-issue-list__fix");
            row.Add(label);
            row.Add(fix);

            // 選択状態に頼らずクリックごとに反応させる（同じ項目を続けてクリックしてもフォーカスし直せる）
            label.RegisterCallback<ClickEvent>(_ =>
            {
                if (row.userData is GraphIssue issue && issue.NodeId != null)
                {
                    _graphView.FocusNode(issue.NodeId);
                }
            });
            fix.clicked += () =>
            {
                if (row.userData is GraphIssue issue)
                {
                    FixIssue(issue, fix);
                }
            };
            return row;
        }

        private void BindIssueItem(VisualElement element, int index)
        {
            var issue = _issues[index];
            element.userData = issue;
            element.EnableInClassList(IssueErrorClassName, issue.Severity == GraphIssueSeverity.Error);
            element.EnableInClassList(IssueWarningClassName, issue.Severity == GraphIssueSeverity.Warning);

            var label = element.Q<Label>(className: "vne-issue-list__message");
            label.text = issue.Message;
            label.tooltip = issue.Message;

            // 直し方の決まっている問題には、直すボタンを出す（行き止まりで止まらないように）
            var fixLabel = IssueFixes.GetLabel(issue);
            var fix = element.Q<Button>(className: "vne-issue-list__fix");
            fix.text = fixLabel ?? string.Empty;
            fix.EnableInClassList("vne-issue-list__fix--hidden", fixLabel == null);
        }

        private void FixIssue(GraphIssue issue, VisualElement anchor)
        {
            if (_asset == null || _graphView == null)
            {
                return;
            }

            switch (issue.Kind)
            {
                case GraphIssueKind.MissingEntry:
                    // ルートの Entry はルート階層にしか置けない
                    if (_graphView.CurrentContainerId.Length > 0)
                    {
                        _graphView.EnterLevel(string.Empty);
                    }

                    _graphView.AddEntry();
                    break;
                case GraphIssueKind.SceneNotSet:
                case GraphIssueKind.SceneMissing:
                    var nodeId = issue.NodeId;
                    var title = _asset.FindNode(nodeId) is NodeData node && node.HasCustomTitle ? node.Title : null;
                    ScenePicker.ShowMenu(anchor.worldBound, scene => AssignScene(nodeId, scene), ScenePicker.ToFileName(title));
                    break;
                case GraphIssueKind.SceneNotInBuildSettings:
                    AddScenesToBuildSettings();
                    break;
                case GraphIssueKind.NoGraphRunner:
                    PlaySetup.PrepareWithoutPlaying(_asset, message => ShowNotification(new GUIContent(message)));
                    Revalidate();
                    break;
            }
        }

        private void AssignScene(string nodeId, SceneAsset scene)
        {
            var path = scene != null ? AssetDatabase.GetAssetPath(scene) : null;
            if (string.IsNullOrEmpty(path) || !_graphView.AssignScene(nodeId, new SceneReference(AssetDatabase.AssetPathToGUID(path), path)))
            {
                return;
            }

            // インスペクタのシーンの欄はバインドではないので、表示中なら作り直す
            if (_inspector != null && _inspector.NodeId == nodeId)
            {
                _inspector.Show(_asset, nodeId);
            }
        }

        private void OnSelectedNodeChanged(NodeView view)
        {
            var nodeId = view?.NodeId;
            if (_inspector != null && nodeId != _inspector.NodeId)
            {
                _inspector.Show(_asset, nodeId);
            }

            // ツリーでも同じノードを選ぶ（ツリーからの移動は起こさない）
            _nodeTree?.Select(nodeId);
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

        /// <summary>新しいスクリプトの振る舞いがノードへ追加されたら、表示を作り直してそのノードを選び直す（インスペクタに出す）。</summary>
        private void OnBehaviourScriptAdded(NodeGraphAsset asset, string nodeId)
        {
            if (asset != _asset || _graphView == null)
            {
                return;
            }

            _graphView.Populate(_asset);
            _graphView.SelectNode(nodeId);
        }

        // MCP のツール（AI エージェント）が開いているグラフを書き換えたら、表示中の階層のまま作り直し、選んでいたノードを選び直す
        private void OnGraphEditedByMcp(NodeGraphAsset asset)
        {
            if (asset != _asset || _graphView == null)
            {
                return;
            }

            var selected = _inspector?.NodeId;
            _graphView.Populate(_asset);
            _graphView.SelectNode(selected);
            _inspector?.Show(_asset, _asset.FindNode(selected) != null ? selected : null);
        }

        private void OnNodeCreationRequest(NodeCreationContext context)
        {
            if (_asset == null)
            {
                ShowNotification(new GUIContent("Open a Node Graph asset first."));
                return;
            }

            _searchWindow.SetPendingConnection(null);
            SearchWindow.Open(new SearchWindowContext(context.screenMousePosition), _searchWindow);
        }

        /// <summary>ポートからエッジを空き地へ落としたら、そこに作って繋ぐノードを検索で選ばせる。</summary>
        private void OnConnectedNodeRequested(PendingConnection pending, Vector2 worldPosition)
        {
            if (_asset == null)
            {
                return;
            }

            _searchWindow.SetPendingConnection(pending);
            var screenPosition = Event.current != null
                ? GUIUtility.GUIToScreenPoint(Event.current.mousePosition)
                : position.position + worldPosition;
            SearchWindow.Open(new SearchWindowContext(screenPosition), _searchWindow);
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
