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

        // 貼り付けた要素を元の位置から少しずらす量。同じ内容を続けて貼るたびに、さらにこの分ずらす
        private static readonly Vector2 PasteOffset = new Vector2(30f, 30f);

        private string _lastPastedData;
        private int _pasteCount;

        // 位置・大きさは NodeGraphView.uss（#vne-minimap）で決める
        private readonly MiniMap _miniMap = new MiniMap { anchored = true, name = "vne-minimap" };

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

        /// <summary>Play 中に強調しているノードの ID。無ければ null。</summary>
        public string RunningNodeId { get; private set; }

        /// <summary>
        /// GraphRunner が今いるノードを強調する。null で強調を消す。再構築（Undo など）の後も維持する。
        /// </summary>
        public void SetRunningNode(string nodeId)
        {
            if (RunningNodeId != null && FindNodeView(RunningNodeId) is NodeView previous)
            {
                previous.IsRunning = false;
            }

            RunningNodeId = nodeId;
            if (nodeId != null && FindNodeView(nodeId) is NodeView current)
            {
                current.IsRunning = true;
            }
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

        /// <summary>アセットの内容でビューを再構築する。再構築中の変更はアセットへ書き戻さない。</summary>
        public void Populate(NodeGraphAsset asset)
        {
            _asset = asset;

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

            NotifySelectionChanged();
            GraphChanged?.Invoke();
        }

        /// <summary>
        /// <paramref name="nodeType"/> のノードを <paramref name="position"/>（グラフ座標）に追加する。
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

            Undo.RecordObject(_asset, "Add Node");
            _asset.AddNode(data);
            EditorUtility.SetDirty(_asset);

            var view = NodeViewFactory.Create(data);
            AddElement(view);
            GraphChanged?.Invoke();
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
            var data = new GroupData { Position = position };

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

        /// <summary>検証結果を各ノードの表示に反映する。</summary>
        public void ShowIssues(IReadOnlyList<GraphIssue> issues)
        {
            var issuesByNode = issues.Where(i => i.NodeId != null).ToLookup(i => i.NodeId);
            foreach (var view in nodes.ToList().OfType<NodeView>())
            {
                view.ShowIssues(issuesByNode[view.NodeId].ToList());
            }
        }

        /// <summary>ノードを選択し、画面の中央に表示する。</summary>
        public void FocusNode(string nodeId)
        {
            var view = FindNodeView(nodeId);
            if (view == null)
            {
                return;
            }

            ClearSelection();
            AddToSelection(view);
            FrameSelection();
        }

        /// <summary>向きが逆・別ノード・まだ接続されていないポートだけを接続候補にする。</summary>
        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            return ports.ToList()
                .Where(p => p.direction != startPort.direction
                    && p.node != startPort.node
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
            evt.menu.AppendSeparator();
            evt.menu.AppendAction("Create Group", _ => CreateGroup(position));
            evt.menu.AppendAction("Create Sticky Note", _ => CreateStickyNote(position));
        }

        /// <summary><paramref name="position"/>（グラフ座標）に付箋を追加する。アセット未設定のときは何もせず null を返す。</summary>
        public StickyNoteView CreateStickyNote(Vector2 position)
        {
            if (_asset == null)
            {
                return null;
            }

            var data = new StickyNoteData { Rect = new Rect(position, StickyNoteData.DefaultSize) };

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

        private void BuildElements(NodeGraphAsset asset)
        {
            var views = new Dictionary<string, NodeView>();
            foreach (var node in asset.Nodes)
            {
                // 型が削除・改名されると SerializeReference は null を返すことがある
                if (node == null)
                {
                    continue;
                }

                var view = NodeViewFactory.Create(node);
                AddElement(view);
                views[node.Id] = view;
            }

            foreach (var edgeData in asset.Edges)
            {
                var output = views.TryGetValue(edgeData.FromNodeId, out var from)
                    ? from.FindPort(edgeData.FromPort, Direction.Output)
                    : null;
                var input = views.TryGetValue(edgeData.ToNodeId, out var to)
                    ? to.FindPort(edgeData.ToPort, Direction.Input)
                    : null;
                if (output == null || input == null)
                {
                    Debug.LogWarning($"[VisualNodeEditor] Skipped edge {edgeData.FromNodeId}.{edgeData.FromPort} -> "
                        + $"{edgeData.ToNodeId}.{edgeData.ToPort} in '{asset.name}': port not found.", asset);
                    continue;
                }

                AddElement(output.ConnectTo(input));
            }

            foreach (var groupData in asset.Groups)
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

            foreach (var stickyNote in asset.StickyNotes)
            {
                AddStickyNoteView(stickyNote);
            }

            if (RunningNodeId != null && views.TryGetValue(RunningNodeId, out var running))
            {
                running.IsRunning = true;
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
                    SavePosition(element);

                    // グループを動かすと中のノードも動くので、念のため一緒に保存する
                    if (element is GroupView groupView)
                    {
                        foreach (var member in groupView.containedElements)
                        {
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

        private static bool IsEditingText(VisualElement target) =>
            target is TextField || target?.GetFirstAncestorOfType<TextField>() != null;

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

            var content = GraphClipboard.Deserialize(data, PasteOffset * _pasteCount);
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

            if (_asset.FindEdge(fromId, edge.output.portName, toId, edge.input.portName) == null)
            {
                _asset.AddEdge(new EdgeData(fromId, edge.output.portName, toId, edge.input.portName));
            }
        }

        private void RemoveEdgeData(Edge edge)
        {
            if (TryGetEdgeKey(edge, out var fromId, out var toId))
            {
                _asset.RemoveEdge(_asset.FindEdge(fromId, edge.output.portName, toId, edge.input.portName));
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
