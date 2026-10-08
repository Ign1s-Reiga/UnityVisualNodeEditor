using System;
using System.Collections.Generic;
using System.Linq;
using Reiga.VisualNodeEditor.Editor.Clipboard;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Editor.Views
{
    /// <summary>
    /// GraphView 本体。アセットとビューの同期を担当する。
    /// データは常に ID で引き直し、View が持つインスタンスの同一性には頼らない。
    /// </summary>
    public sealed class NodeGraphView : GraphView
    {
        private const string StyleSheetPath = "VisualNodeEditor/NodeGraphView";

        /// <summary>グリッドの間隔。NodeGraphView.uss の GridBackground（--spacing）と同じ値にすること。</summary>
        public const float GridSpacing = 20f;

        // 貼り付けた要素を元の位置から少しずらす量。同じ内容を続けて貼るたびに、さらにこの分ずらす
        private static readonly Vector2 PasteOffset = new Vector2(30f, 30f);

        // 新しいコンテナの中に自動で作る Exit ノードの位置（Entry は原点）
        private static readonly Vector2 ContainerExitOrigin = new Vector2(400f, 0f);
        private static readonly Vector2 ContainerExitSpacing = new Vector2(0f, 100f);

        private string _lastPastedData;
        private int _pasteCount;
        private string _searchQuery;

        // 表示中の階層（ルートから外側順のコンテナ ID）。空ならルート階層
        private readonly List<string> _levelPath = new();

        // 位置・大きさは NodeGraphView.uss（#vne-minimap）で決める
        private readonly MiniMap _miniMap = new MiniMap { anchored = true, name = "vne-minimap" };

        // 位置・大きさは NodeGraphView.uss（#vne-blackboard）で決める
        private readonly ParameterBlackboard _blackboard;

        private NodeGraphAsset _asset;

        // Populate やプログラムからの変更中は、GraphView のコールバックをアセットへ書き戻さない
        private bool _suppressSync;

        public NodeGraphView()
        {
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            // GraphView は既定 USS を自身に付けており、同じ詳細度ならより近い USS が勝つ。
            // ウィンドウのルートに書いたグリッドのスタイルは負けて効かないため、GraphView 自身に付ける
            var styleSheet = Resources.Load<StyleSheet>(StyleSheetPath);
            if (styleSheet != null)
            {
                styleSheets.Add(styleSheet);
            }

            // グリッドは最背面（contentViewContainer より前）に置き、GraphView 全体に広げる
            var grid = new GridBackground { name = "vne-grid" };
            Insert(0, grid);
            grid.StretchToParentSize();

            // Ctrl+C / X / V / D と右クリックの Copy / Cut / Paste / Duplicate は GraphView が処理し、ここに委ねる
            serializeGraphElements = SerializeElements;
            canPasteSerializedData = GraphClipboard.CanPaste;
            unserializeAndPaste = PasteElements;

            graphViewChanged = OnGraphViewChanged;
            elementsAddedToGroup = OnElementsAddedToGroup;
            elementsRemovedFromGroup = OnElementsRemovedFromGroup;
            groupTitleChanged = OnGroupTitleChanged;
            _miniMap.graphView = this;
            _blackboard = new ParameterBlackboard(this);
            _blackboard.Changed += () => GraphChanged?.Invoke();
            RegisterCallback<KeyDownEvent>(OnKeyDown);
            RegisterCallback<AttachToPanelEvent>(_ => Undo.undoRedoPerformed += OnUndoRedo);
            RegisterCallback<DetachFromPanelEvent>(_ => Undo.undoRedoPerformed -= OnUndoRedo);
        }

        /// <summary>グラフの内容（ノード・エッジ・グループ）が変わった後に呼ばれる。</summary>
        public event Action GraphChanged;

        /// <summary>選択が変わったときに呼ばれる。ノードがちょうど 1 つ選ばれていればその View、それ以外は null。</summary>
        public event Action<NodeView> SelectedNodeChanged;

        /// <summary>現在表示しているアセット。</summary>
        public NodeGraphAsset Asset => _asset;

        /// <summary>
        /// ノードを検索し、一致するノードを強調・それ以外を薄くする。一致したノードの ID をアセット内の順で返す。
        /// 空の検索語で表示を元に戻す。再構築（Undo など）の後も同じ検索語で表示を保つ。
        /// </summary>
        public List<string> ApplySearch(string query)
        {
            _searchQuery = query;
            var searching = !string.IsNullOrWhiteSpace(query);
            var matches = new List<string>();
            foreach (var view in nodes.ToList().OfType<NodeView>())
            {
                var isMatch = searching && NodeSearch.Matches(query, view.title, NodeDisplay.GetTypeDisplayName(view.Data.GetType()));
                view.ShowSearchResult(searching, isMatch);
                if (isMatch)
                {
                    matches.Add(view.NodeId);
                }
            }

            if (_asset != null)
            {
                // ID が重複していても（検証で Error になる状態）落ちないよう、先に出てきた位置を使う
                var order = new Dictionary<string, int>();
                for (var i = 0; i < _asset.Nodes.Count; i++)
                {
                    if (_asset.Nodes[i] != null)
                    {
                        order.TryAdd(_asset.Nodes[i].Id, i);
                    }
                }

                matches.Sort((x, y) => order.GetValueOrDefault(x).CompareTo(order.GetValueOrDefault(y)));
            }

            return matches;
        }

        /// <summary>Play 中に強調しているノードの ID。無ければ null。</summary>
        public string RunningNodeId { get; private set; }

        /// <summary>
        /// GraphRunner が今いるノードを強調する。null で強調を消す。再構築（Undo など）の後も維持する。
        /// ノードが表示中の階層より深い所にあれば、それを含むコンテナを強調する。
        /// </summary>
        public void SetRunningNode(string nodeId)
        {
            RunningNodeId = nodeId;
            ShowRunningNode();
        }

        private void ShowRunningNode()
        {
            var visibleId = FindVisibleNodeId(RunningNodeId);
            foreach (var view in nodes.ToList().OfType<NodeView>())
            {
                view.IsRunning = visibleId != null && view.NodeId == visibleId;
            }
        }

        /// <summary>
        /// ノードが表示中の階層にあればその ID を、より深い階層にあれば、それを含む表示中の階層のコンテナの ID を返す。
        /// 表示中の階層の外（上の階層や、別のコンテナの中）にあれば null。
        /// </summary>
        public string FindVisibleNodeId(string nodeId)
        {
            if (_asset == null || nodeId == null)
            {
                return null;
            }

            var level = CurrentContainerId;
            var visited = new HashSet<string>();
            for (var node = _asset.FindNode(nodeId); node != null && visited.Add(node.Id); node = _asset.FindNode(node.ParentId))
            {
                if (NodeGraphAsset.IsSameLevel(node.ParentId, level))
                {
                    return node.Id;
                }
            }

            return null;
        }

        /// <summary>ミニマップを表示するか。</summary>
        public bool MiniMapVisible
        {
            get => _miniMap.parent == this;
            set
            {
                if (value && _miniMap.parent != this)
                {
                    Add(_miniMap);
                }
                else if (!value)
                {
                    _miniMap.RemoveFromHierarchy();
                }
            }
        }

        /// <summary>ノード・付箋を動かし終えたときにグリッドへ吸着させるか。</summary>
        public bool SnapToGrid { get; set; }

        /// <summary>パラメータの Blackboard を表示するか。</summary>
        public bool BlackboardVisible
        {
            get => _blackboard.parent == this;
            set
            {
                if (value && _blackboard.parent != this)
                {
                    Add(_blackboard);
                }
                else if (!value)
                {
                    _blackboard.RemoveFromHierarchy();
                }
            }
        }

        /// <summary>パラメータの Blackboard（テストや拡張から操作するため）。</summary>
        public ParameterBlackboard Blackboard => _blackboard;

        /// <summary>選択中の要素が収まるように表示する。何も選択していなければ全体を表示する。</summary>
        public void FrameSelectionOrAll()
        {
            if (selection.Count == 0)
            {
                FrameAll();
            }
            else
            {
                FrameSelection();
            }
        }

        /// <summary>表示中の階層（コンテナの ID）。空文字ならルート階層。</summary>
        public string CurrentContainerId => _levelPath.Count == 0 ? string.Empty : _levelPath[_levelPath.Count - 1];

        /// <summary>ルートから表示中の階層までのコンテナの ID（外側から順。ルートなら空）。パンくずの表示に使う。</summary>
        public IReadOnlyList<string> LevelPath => _levelPath;

        /// <summary>表示する階層が変わったとき（コンテナに入った・戻った・表示中のコンテナが消えた）。</summary>
        public event Action LevelChanged;

        /// <summary>
        /// 表示中のアセットの、<paramref name="containerId"/> の階層を表示する（空文字ならルート）。
        /// コンテナが見つからなければルートを表示する。
        /// </summary>
        public void EnterLevel(string containerId) => Populate(_asset, containerId);

        /// <summary>1 つ上の階層を表示する。ルートにいれば何もしない。</summary>
        public void ExitLevel()
        {
            if (_levelPath.Count > 0)
            {
                EnterLevel(_levelPath.Count >= 2 ? _levelPath[_levelPath.Count - 2] : string.Empty);
            }
        }

        /// <summary>
        /// <paramref name="asset"/> の <paramref name="containerId"/> の階層を表示する（空文字ならルート）。
        /// コンテナが見つからなければルートを表示する。
        /// </summary>
        public void Populate(NodeGraphAsset asset, string containerId)
        {
            _levelPath.Clear();
            if (asset != null && asset.FindNode(containerId) is ContainerNode container)
            {
                // 親をたどってルートからの道筋を作る（親の参照が輪になっていても止まる）
                var visited = new HashSet<string>();
                for (NodeData node = container; node is ContainerNode && visited.Add(node.Id); node = asset.FindNode(node.ParentId))
                {
                    _levelPath.Insert(0, node.Id);
                }
            }

            Populate(asset);
            LevelChanged?.Invoke();
        }

        /// <summary>
        /// アセットの内容で、表示中の階層のビューを再構築する。再構築中の変更はアセットへ書き戻さない。
        /// Undo などで表示中のコンテナが消えていたら、存在する一番近い親の階層へ戻る。
        /// </summary>
        public void Populate(NodeGraphAsset asset)
        {
            _asset = asset;

            var missing = _levelPath.FindIndex(id => !(asset != null && asset.FindNode(id) is ContainerNode));
            var levelChanged = missing >= 0;
            if (levelChanged)
            {
                _levelPath.RemoveRange(missing, _levelPath.Count - missing);
            }

            _suppressSync = true;
            try
            {
                DeleteElements(graphElements.ToList());
                if (asset != null)
                {
                    BuildElements(asset);
                }
            }
            finally
            {
                _suppressSync = false;
            }

            _blackboard.Rebuild(asset);

            NotifySelectionChanged();
            GraphChanged?.Invoke();
            if (levelChanged)
            {
                LevelChanged?.Invoke();
            }
        }

        /// <summary>
        /// <paramref name="nodeType"/> のノードを、表示中の階層の <paramref name="position"/>（グラフ座標）に追加する。
        /// コンテナなら中に Entry と、出口ごとの Exit ノードも作る。コンテナの中の Exit ノードは親の最初の出口を指す。
        /// アセット未設定のときは何もせず null を返す。
        /// </summary>
        public NodeView CreateNode(Type nodeType, Vector2 position)
        {
            if (_asset == null)
            {
                return null;
            }

            var data = (NodeData)Activator.CreateInstance(nodeType);
            data.Position = position;
            data.ParentId = CurrentContainerId;
            if (data is ContainerExitNode exitNode && _asset.FindNode(CurrentContainerId) is ContainerNode parent
                && parent.Exits.Count > 0)
            {
                exitNode.ExitId = parent.Exits[0].Id;
            }

            Undo.RecordObject(_asset, "Add Node");
            _asset.AddNode(data);
            if (data is ContainerNode container)
            {
                AddContainerContents(container);
            }

            EditorUtility.SetDirty(_asset);

            var view = CreateNodeView(data);
            AddElement(view);
            GraphChanged?.Invoke();
            return view;
        }

        /// <summary>新しいコンテナの中に、Entry と出口ごとの Exit ノードを作る（中の階層の左に Entry、右に Exit を縦に並べる）。</summary>
        private void AddContainerContents(ContainerNode container)
        {
            _asset.AddNode(new ContainerEntryNode { ParentId = container.Id, Position = Vector2.zero });
            for (var i = 0; i < container.Exits.Count; i++)
            {
                _asset.AddNode(new ContainerExitNode
                {
                    ParentId = container.Id,
                    ExitId = container.Exits[i].Id,
                    Position = ContainerExitOrigin + ContainerExitSpacing * i,
                });
            }
        }

        private NodeView CreateNodeView(NodeData data)
        {
            var view = NodeViewFactory.Create(data, _asset);
            view.CollapsedChanged += OnNodeCollapsedChanged;
            return view;
        }

        /// <summary>
        /// 選択中のノードを囲むグループを作る。何も選択していなければ <paramref name="position"/> に空のグループを作る。
        /// </summary>
        public GroupView CreateGroup(Vector2 position)
        {
            if (_asset == null)
            {
                return null;
            }

            var members = selection.OfType<NodeView>().ToList();
            var data = new GroupData { Position = position, ParentId = CurrentContainerId };

            Undo.RecordObject(_asset, "Create Group");
            foreach (var member in members)
            {
                RemoveFromAllGroups(member.NodeId);
                data.AddNode(member.NodeId);
            }

            _asset.AddGroup(data);
            EditorUtility.SetDirty(_asset);

            var view = new GroupView(data);
            _suppressSync = true;
            try
            {
                AddElement(view);
                foreach (var member in members)
                {
                    if (member.GetContainingScope() is Group current)
                    {
                        current.RemoveElement(member);
                    }
                }

                view.AddElements(members);
            }
            finally
            {
                _suppressSync = false;
            }

            GraphChanged?.Invoke();
            return view;
        }

        /// <summary>表示中の階層のノードだけを選択する（表示位置は変えない）。見つからなければ選択を外すだけ。</summary>
        public void SelectNode(string nodeId)
        {
            ClearSelection();
            if (FindNodeView(nodeId) is NodeView view)
            {
                AddToSelection(view);
            }
        }

        /// <summary>ID に対応するノードの View を返す。無ければ null。</summary>
        public NodeView FindNodeView(string nodeId) => GetNodeByGuid(nodeId) as NodeView;

        /// <summary>アセット上のデータでノードの表示（タイトルなど）を更新する。</summary>
        public void RefreshNode(string nodeId)
        {
            var view = FindNodeView(nodeId);
            var data = _asset != null ? _asset.FindNode(nodeId) : null;
            if (view != null && data != null)
            {
                view.Rebind(data);
            }
        }

        /// <summary>
        /// 検証結果を各ノードの表示に反映する。コンテナには、その中（入れ子を含む）のノードの問題もまとめて出す。
        /// </summary>
        public void ShowIssues(IReadOnlyList<GraphIssue> issues)
        {
            var issuesByNode = issues
                .Select(i => (Issue: i, VisibleId: FindVisibleNodeId(i.NodeId)))
                .Where(x => x.VisibleId != null)
                .ToLookup(x => x.VisibleId, x => x.Issue);
            foreach (var view in nodes.ToList().OfType<NodeView>())
            {
                view.ShowIssues(issuesByNode[view.NodeId].ToList());
            }
        }

        /// <summary>
        /// ノードを選択し、画面の中央に表示する。別の階層のノードなら、その階層に移ってから選択する。
        /// </summary>
        public void FocusNode(string nodeId)
        {
            if (_asset != null && _asset.FindNode(nodeId) is NodeData node
                && !NodeGraphAsset.IsSameLevel(node.ParentId, CurrentContainerId))
            {
                EnterLevel(node.ParentId);
            }

            var view = FindNodeView(nodeId);
            if (view == null)
            {
                return;
            }

            ClearSelection();
            AddToSelection(view);
            FrameSelection();
        }

        /// <summary>
        /// 向きが逆・別ノード・まだ接続されていない・同じ階層にあるポートだけを接続候補にする。
        /// 表示しているのは 1 つの階層だけだが、階層をまたぐエッジは検証で Error になるので、データでも確かめる。
        /// </summary>
        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var startParent = (startPort.node as NodeView)?.Data?.ParentId;
            return ports.ToList()
                .Where(p => p.direction != startPort.direction
                    && p.node != startPort.node
                    && p.node is NodeView view && view.Data != null
                    && NodeGraphAsset.IsSameLevel(view.Data.ParentId, startParent)
                    && !p.connections.Any(e => e.input == startPort || e.output == startPort))
                .ToList();
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            base.BuildContextualMenu(evt);
            if (_asset == null || !(evt.target is GraphView || evt.target is NodeView))
            {
                return;
            }

            var position = contentViewContainer.WorldToLocal(evt.mousePosition);
            if (evt.target is ContainerNodeView containerView)
            {
                evt.menu.AppendSeparator();
                evt.menu.AppendAction("Open Container", _ => EnterLevel(containerView.NodeId));
            }
            else if (evt.target is GraphView && _levelPath.Count > 0)
            {
                evt.menu.AppendSeparator();
                evt.menu.AppendAction("Open Parent Level", _ => ExitLevel());
            }

            evt.menu.AppendSeparator();
            evt.menu.AppendAction("Create Group", _ => CreateGroup(position));
            evt.menu.AppendAction("Create Sticky Note", _ => CreateStickyNote(position));
            evt.menu.AppendSeparator();
            evt.menu.AppendAction("Collapse All", _ => SetCollapsedForSelectionOrAll(true));
            evt.menu.AppendAction("Expand All", _ => SetCollapsedForSelectionOrAll(false));

            var selectedNodes = selection.OfType<NodeView>().Count();
            if (selectedNodes >= 2)
            {
                evt.menu.AppendSeparator();
                AppendAlign(evt, "Left", AlignMode.Left);
                AppendAlign(evt, "Right", AlignMode.Right);
                AppendAlign(evt, "Top", AlignMode.Top);
                AppendAlign(evt, "Bottom", AlignMode.Bottom);
                AppendAlign(evt, "Center Horizontally", AlignMode.CenterHorizontally);
                AppendAlign(evt, "Center Vertically", AlignMode.CenterVertically);
                var distributeStatus = selectedNodes >= 3
                    ? DropdownMenuAction.Status.Normal
                    : DropdownMenuAction.Status.Disabled;
                evt.menu.AppendAction("Distribute/Horizontally", _ => DistributeSelection(DistributeAxis.Horizontal), distributeStatus);
                evt.menu.AppendAction("Distribute/Vertically", _ => DistributeSelection(DistributeAxis.Vertical), distributeStatus);
            }
        }

        /// <summary>選択中のノードを揃える。1 回の Undo で戻せる。</summary>
        public void AlignSelection(AlignMode mode) =>
            MoveSelectedNodes("Align Nodes", rects => NodeAlignment.Align(rects, mode));

        /// <summary>選択中のノード（3 つ以上）を等間隔に並べる。1 回の Undo で戻せる。</summary>
        public void DistributeSelection(DistributeAxis axis) =>
            MoveSelectedNodes("Distribute Nodes", rects => NodeAlignment.Distribute(rects, axis));

        private void AppendAlign(ContextualMenuPopulateEvent evt, string label, AlignMode mode) =>
            evt.menu.AppendAction("Align/" + label, _ => AlignSelection(mode));

        private void MoveSelectedNodes(string undoName, Func<IReadOnlyList<Rect>, Vector2[]> layout)
        {
            if (_asset == null)
            {
                return;
            }

            var views = selection.OfType<NodeView>().ToList();
            if (views.Count < 2)
            {
                return;
            }

            var rects = views.Select(v => v.GetPosition()).ToList();
            var positions = layout(rects);

            Undo.RecordObject(_asset, undoName);
            for (var i = 0; i < views.Count; i++)
            {
                views[i].SetPosition(new Rect(positions[i], rects[i].size));
                if (_asset.FindNode(views[i].NodeId) is NodeData data)
                {
                    data.Position = positions[i];
                }
            }

            EditorUtility.SetDirty(_asset);
        }

        private void SnapIfEnabled(GraphElement element)
        {
            if (!SnapToGrid || !(element is NodeView || element is StickyNoteView))
            {
                return;
            }

            var rect = element.GetPosition();
            element.SetPosition(new Rect(GridSnap.Snap(rect.position, GridSpacing), rect.size));
        }

        /// <summary>選択中のノード（無ければ全ノード）を折りたたむ・展開する。1 回の Undo で戻せる。</summary>
        public void SetCollapsedForSelectionOrAll(bool collapsed)
        {
            if (_asset == null)
            {
                return;
            }

            var targets = selection.OfType<NodeView>().ToList();
            if (targets.Count == 0)
            {
                targets = nodes.ToList().OfType<NodeView>().ToList();
            }

            Undo.RecordObject(_asset, collapsed ? "Collapse Nodes" : "Expand Nodes");
            foreach (var view in targets)
            {
                view.SetCollapsed(collapsed);
                if (_asset.FindNode(view.NodeId) is NodeData data)
                {
                    data.Collapsed = collapsed;
                }
            }

            EditorUtility.SetDirty(_asset);
        }

        private void OnNodeCollapsedChanged(NodeView view)
        {
            if (_suppressSync || _asset == null || !(_asset.FindNode(view.NodeId) is NodeData data))
            {
                return;
            }

            Undo.RecordObject(_asset, view.IsCollapsed ? "Collapse Node" : "Expand Node");
            data.Collapsed = view.IsCollapsed;
            EditorUtility.SetDirty(_asset);
        }

        /// <summary><paramref name="position"/>（グラフ座標）に付箋を追加する。アセット未設定のときは何もせず null を返す。</summary>
        public StickyNoteView CreateStickyNote(Vector2 position)
        {
            if (_asset == null)
            {
                return null;
            }

            var data = new StickyNoteData { Rect = new Rect(position, StickyNoteData.DefaultSize), ParentId = CurrentContainerId };

            Undo.RecordObject(_asset, "Create Sticky Note");
            _asset.AddStickyNote(data);
            EditorUtility.SetDirty(_asset);

            var view = AddStickyNoteView(data);
            GraphChanged?.Invoke();
            return view;
        }

        public override void AddToSelection(ISelectable selectable)
        {
            base.AddToSelection(selectable);
            NotifySelectionChanged();
        }

        public override void RemoveFromSelection(ISelectable selectable)
        {
            base.RemoveFromSelection(selectable);
            NotifySelectionChanged();
        }

        public override void ClearSelection()
        {
            base.ClearSelection();
            NotifySelectionChanged();
        }

        private void NotifySelectionChanged()
        {
            var selectedNodes = selection.OfType<NodeView>().Take(2).ToList();
            SelectedNodeChanged?.Invoke(selectedNodes.Count == 1 ? selectedNodes[0] : null);
        }

        /// <summary>表示中の階層のノード・エッジ・グループ・付箋だけを作る。</summary>
        private void BuildElements(NodeGraphAsset asset)
        {
            var level = CurrentContainerId;
            var views = new Dictionary<string, NodeView>();
            foreach (var node in asset.Nodes)
            {
                // 型が削除・改名されると SerializeReference は null を返すことがある
                if (node == null || !NodeGraphAsset.IsSameLevel(node.ParentId, level))
                {
                    continue;
                }

                var view = CreateNodeView(node);
                AddElement(view);
                views[node.Id] = view;
            }

            foreach (var edgeData in asset.Edges)
            {
                var hasFrom = views.TryGetValue(edgeData.FromNodeId, out var from);
                var hasTo = views.TryGetValue(edgeData.ToNodeId, out var to);

                // 別の階層のエッジ。階層をまたぐもの・ノードが無いものは検証が Error として出す
                if (!hasFrom || !hasTo)
                {
                    continue;
                }

                var output = from.FindPort(edgeData.FromPort, Direction.Output);
                var input = to.FindPort(edgeData.ToPort, Direction.Input);
                if (output == null || input == null)
                {
                    Debug.LogWarning($"[VisualNodeEditor] Skipped edge {edgeData.FromNodeId}.{edgeData.FromPort} -> "
                        + $"{edgeData.ToNodeId}.{edgeData.ToPort} in '{asset.name}': port not found.", asset);
                    continue;
                }

                AddElement(output.ConnectTo(input));
            }

            // 折りたたみ済みのノードは、生成時点（エッジがまだ無い）ですべてのポートが隠れている。
            // エッジを繋いだ後に表示を更新し、接続済みのポートを見えるようにする
            foreach (var view in views.Values.Where(v => v.IsCollapsed))
            {
                view.RefreshExpandedState();
            }

            foreach (var groupData in asset.Groups.Where(g => NodeGraphAsset.IsSameLevel(g.ParentId, level)))
            {
                var groupView = new GroupView(groupData);
                AddElement(groupView);

                var members = new List<GraphElement>();
                foreach (var nodeId in groupData.NodeIds)
                {
                    // ノードは 1 つのグループにしか入れないので、データ上の重複所属は先勝ちにする
                    if (views.TryGetValue(nodeId, out var member) && member.GetContainingScope() == null)
                    {
                        members.Add(member);
                    }
                }

                groupView.AddElements(members);
            }

            foreach (var stickyNote in asset.StickyNotes.Where(s => NodeGraphAsset.IsSameLevel(s.ParentId, level)))
            {
                AddStickyNoteView(stickyNote);
            }

            ShowRunningNode();

            if (!string.IsNullOrWhiteSpace(_searchQuery))
            {
                ApplySearch(_searchQuery);
            }
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (_suppressSync || _asset == null)
            {
                return change;
            }

            var changed = false;

            if (change.elementsToRemove != null && change.elementsToRemove.Count > 0)
            {
                Undo.RecordObject(_asset, "Delete Graph Elements");
                foreach (var element in change.elementsToRemove)
                {
                    switch (element)
                    {
                        case Edge edge:
                            RemoveEdgeData(edge);
                            break;
                        case NodeView nodeView:
                            _asset.RemoveNode(_asset.FindNode(nodeView.NodeId));
                            break;
                        case GroupView groupView:
                            _asset.RemoveGroup(_asset.FindGroup(groupView.GroupId));
                            break;
                        case StickyNoteView stickyNoteView:
                            _asset.RemoveStickyNote(_asset.FindStickyNote(stickyNoteView.StickyNoteId));
                            break;
                    }
                }

                changed = true;
            }

            if (change.edgesToCreate != null && change.edgesToCreate.Count > 0)
            {
                Undo.RecordObject(_asset, "Connect Ports");
                foreach (var edge in change.edgesToCreate)
                {
                    AddEdgeData(edge);
                }

                changed = true;
            }

            if (change.movedElements != null && change.movedElements.Count > 0)
            {
                Undo.RecordObject(_asset, "Move Graph Elements");
                foreach (var element in change.movedElements)
                {
                    SnapIfEnabled(element);
                    SavePosition(element);

                    // グループを動かすと中のノードも動くので、念のため一緒に保存する
                    if (element is GroupView groupView)
                    {
                        foreach (var member in groupView.containedElements)
                        {
                            SnapIfEnabled(member);
                            SavePosition(member);
                        }
                    }
                }

                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(_asset);
                GraphChanged?.Invoke();
            }

            return change;
        }

        private void SavePosition(GraphElement element)
        {
            var position = element.GetPosition().position;
            switch (element)
            {
                case NodeView nodeView when _asset.FindNode(nodeView.NodeId) is NodeData node:
                    node.Position = position;
                    break;
                case GroupView groupView when _asset.FindGroup(groupView.GroupId) is GroupData group:
                    group.Position = position;
                    break;
                case StickyNoteView stickyNoteView when _asset.FindStickyNote(stickyNoteView.StickyNoteId) is StickyNoteData stickyNote:
                    stickyNote.Rect = new Rect(position, stickyNote.Rect.size);
                    break;
            }
        }

        private void OnElementsAddedToGroup(Group group, IEnumerable<GraphElement> elements)
        {
            if (_suppressSync || _asset == null || !(group is GroupView groupView)
                || !(_asset.FindGroup(groupView.GroupId) is GroupData data))
            {
                return;
            }

            Undo.RecordObject(_asset, "Add To Group");
            foreach (var nodeView in elements.OfType<NodeView>())
            {
                RemoveFromAllGroups(nodeView.NodeId);
                data.AddNode(nodeView.NodeId);
            }

            EditorUtility.SetDirty(_asset);
            GraphChanged?.Invoke();
        }

        private void OnElementsRemovedFromGroup(Group group, IEnumerable<GraphElement> elements)
        {
            if (_suppressSync || _asset == null || !(group is GroupView groupView)
                || !(_asset.FindGroup(groupView.GroupId) is GroupData data))
            {
                return;
            }

            Undo.RecordObject(_asset, "Remove From Group");
            foreach (var nodeView in elements.OfType<NodeView>())
            {
                data.RemoveNode(nodeView.NodeId);
            }

            EditorUtility.SetDirty(_asset);
            GraphChanged?.Invoke();
        }

        private void OnGroupTitleChanged(Group group, string title)
        {
            if (_suppressSync || _asset == null || !(group is GroupView groupView)
                || !(_asset.FindGroup(groupView.GroupId) is GroupData data))
            {
                return;
            }

            Undo.RecordObject(_asset, "Rename Group");
            data.Title = title;
            EditorUtility.SetDirty(_asset);
        }

        /// <summary>F = 選択範囲（無ければ全体）、A = 全体。付箋のタイトルなどを入力中は文字入力を優先する。</summary>
        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.modifiers != EventModifiers.None || IsEditingText(evt.target as VisualElement))
            {
                return;
            }

            switch (evt.keyCode)
            {
                case KeyCode.F:
                    FrameSelectionOrAll();
                    break;
                case KeyCode.A:
                    FrameAll();
                    break;
                default:
                    return;
            }

            evt.StopPropagation();
        }

        /// <summary>
        /// キー入力の対象がテキスト入力欄（またはその中）か。TextField だけでなく IntegerField / FloatField など
        /// TextInputBaseField 系すべてを含む（数値欄でも "f" などを入力できるため）。
        /// </summary>
        internal static bool IsEditingText(VisualElement target)
        {
            for (var element = target; element != null; element = element.parent)
            {
                // TextInputBaseField<T> はどの値型でも同じ USS クラスを付ける
                if (element.ClassListContains(TextInputBaseField<string>.ussClassName))
                {
                    return true;
                }
            }

            return false;
        }

        private string SerializeElements(IEnumerable<GraphElement> elements)
        {
            var list = elements.ToList();
            return GraphClipboard.Serialize(
                _asset,
                list.OfType<NodeView>().Select(v => v.NodeId),
                list.OfType<GroupView>().Select(v => v.GroupId),
                list.OfType<StickyNoteView>().Select(v => v.StickyNoteId));
        }

        private void PasteElements(string operationName, string data)
        {
            if (_asset == null)
            {
                return;
            }

            _pasteCount = data == _lastPastedData ? _pasteCount + 1 : 1;
            _lastPastedData = data;

            // 表示中の階層に貼る（コンテナを貼ると、その中身は貼ったコンテナの中に入る）
            var content = GraphClipboard.Deserialize(data, PasteOffset * _pasteCount, CurrentContainerId);
            if (content == null || content.IsEmpty)
            {
                return;
            }

            Undo.RecordObject(_asset, operationName);
            foreach (var node in content.Nodes)
            {
                _asset.AddNode(node);
            }

            foreach (var edge in content.Edges)
            {
                _asset.AddEdge(edge);
            }

            foreach (var group in content.Groups)
            {
                _asset.AddGroup(group);
            }

            foreach (var stickyNote in content.StickyNotes)
            {
                _asset.AddStickyNote(stickyNote);
            }

            EditorUtility.SetDirty(_asset);

            // 再構築してから、貼り付けた要素だけを選択状態にする
            Populate(_asset);
            ClearSelection();
            var pastedKeys = content.Nodes.Select(n => n.Id)
                .Concat(content.Groups.Select(g => g.Id))
                .Concat(content.StickyNotes.Select(s => s.Id));
            foreach (var key in pastedKeys)
            {
                if (GetElementByGuid(key) is GraphElement element)
                {
                    AddToSelection(element);
                }
            }
        }

        private StickyNoteView AddStickyNoteView(StickyNoteData data)
        {
            var view = new StickyNoteView(data);
            view.RegisterCallback<StickyNoteChangeEvent>(evt => OnStickyNoteChanged(view, evt.change));
            view.Resized += SaveStickyNoteRect;
            AddElement(view);
            return view;
        }

        private void OnStickyNoteChanged(StickyNoteView view, StickyNoteChange change)
        {
            if (_suppressSync || _asset == null || !(_asset.FindStickyNote(view.StickyNoteId) is StickyNoteData data))
            {
                return;
            }

            Undo.RecordObject(_asset, "Edit Sticky Note");
            switch (change)
            {
                case StickyNoteChange.Title:
                    data.Title = view.title;
                    break;
                case StickyNoteChange.Contents:
                    data.Contents = view.contents;
                    break;
                case StickyNoteChange.Theme:
                    data.Theme = StickyNoteView.FromGraphView(view.theme);
                    break;
                case StickyNoteChange.FontSize:
                    data.FontSize = StickyNoteView.FromGraphView(view.fontSize);
                    break;
                case StickyNoteChange.Position:
                    data.Rect = view.GetPosition();
                    break;
            }

            EditorUtility.SetDirty(_asset);
        }

        private void SaveStickyNoteRect(StickyNoteView view)
        {
            if (_suppressSync || _asset == null || !(_asset.FindStickyNote(view.StickyNoteId) is StickyNoteData data))
            {
                return;
            }

            Undo.RecordObject(_asset, "Resize Sticky Note");
            data.Rect = view.GetPosition();
            EditorUtility.SetDirty(_asset);
        }

        private void RemoveFromAllGroups(string nodeId)
        {
            foreach (var group in _asset.Groups)
            {
                group.RemoveNode(nodeId);
            }
        }

        private void AddEdgeData(Edge edge)
        {
            if (!TryGetEdgeKey(edge, out var fromId, out var toId))
            {
                return;
            }

            if (_asset.FindEdge(fromId, NodeView.GetPortId(edge.output), toId, NodeView.GetPortId(edge.input)) == null)
            {
                _asset.AddEdge(new EdgeData(fromId, NodeView.GetPortId(edge.output), toId, NodeView.GetPortId(edge.input)));
            }
        }

        private void RemoveEdgeData(Edge edge)
        {
            if (TryGetEdgeKey(edge, out var fromId, out var toId))
            {
                _asset.RemoveEdge(_asset.FindEdge(fromId, NodeView.GetPortId(edge.output), toId, NodeView.GetPortId(edge.input)));
            }
        }

        private static bool TryGetEdgeKey(Edge edge, out string fromNodeId, out string toNodeId)
        {
            fromNodeId = (edge.output?.node as NodeView)?.NodeId;
            toNodeId = (edge.input?.node as NodeView)?.NodeId;
            return fromNodeId != null && toNodeId != null;
        }

        private void OnUndoRedo()
        {
            if (_asset != null)
            {
                Populate(_asset);
            }
        }
    }
}
