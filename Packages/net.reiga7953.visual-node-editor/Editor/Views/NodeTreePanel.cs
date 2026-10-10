using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Editor.Views
{
    /// <summary>
    /// 左のペインのノードツリー。グラフのノードとコンテナの階層を一覧し、クリックでそのノードへ移る（別の階層なら、その階層を開く）。
    /// ダブルクリック（Enter）でコンテナを開く。グラフで選んだノードはツリーでも選び、Play 中は実行中のノードを強調する。
    /// 見た目は NodeGraphEditor.uss（.vne-node-tree）。階層は <see cref="NodeTree.Build"/>。
    /// </summary>
    public sealed class NodeTreePanel : VisualElement
    {
        private const string ItemClassName = "vne-node-tree__item";
        private const string IconClassName = "vne-node-tree__icon";
        private const string LabelClassName = "vne-node-tree__label";
        private const string ContainerClassName = "vne-node-tree__item--container";
        private const string LevelClassName = "vne-node-tree__item--level";
        private const string RunningClassName = "vne-node-tree__item--running";
        private const string RunningPathClassName = "vne-node-tree__item--running-path";
        private const string CategoryClassPrefix = "vne-category--";

        private readonly TreeView _tree;

        // ノードの ID → ツリーの項目の ID（作り直しても同じ ID にして、開いた状態を保つ）と、項目の親
        private readonly Dictionary<string, int> _ids = new();
        private readonly Dictionary<int, NodeTreeEntry> _entries = new();
        private readonly Dictionary<int, int> _parents = new();

        // 今回の作り直しで初めて出たコンテナの項目（開いた状態で出す）
        private readonly List<int> _newContainerIds = new();
        private int _nextId = 1;

        private NodeGraphAsset _asset;
        private string _signature;
        private string _selectedNodeId;
        private string _levelContainerId = string.Empty;
        private string _runningNodeId;
        private bool _syncing;

        /// <summary>ヘッダーと空のツリーを作る。中身は <see cref="Show"/> で入れる。</summary>
        public NodeTreePanel()
        {
            AddToClassList("vne-node-tree");

            var header = new Label("Nodes");
            header.AddToClassList("vne-node-tree__header");
            Add(header);

            _tree = new TreeView
            {
                makeItem = MakeItem,
                bindItem = BindItem,
                selectionType = SelectionType.Single,
                viewDataKey = "vne-node-tree",
            };
            _tree.AddToClassList("vne-node-tree__list");
            _tree.selectionChanged += _ => OnSelectionChanged();
            _tree.itemsChosen += _ => OnItemsChosen();
            _tree.itemExpandedChanged += _ => OnItemExpandedChanged();
            Add(_tree);
        }

        /// <summary>ツリーでノードを選んだとき（ノードの ID）。受け取った側でそのノードへ移る。</summary>
        public event Action<string> NodeSelected;

        /// <summary>ツリーの項目をダブルクリック（Enter）したとき（ノードの ID）。コンテナならその中を開く。</summary>
        public event Action<string> NodeOpened;

        /// <summary>ツリーで選んでいるノードの ID。無ければ null。</summary>
        public string SelectedNodeId => _selectedNodeId;

        /// <summary>ツリーに出しているノードの数（コンテナの中も含む）。</summary>
        public int NodeCount => _entries.Count;

        /// <summary>
        /// <paramref name="asset"/> のノードでツリーを作り直す（階層・名前が変わっていなければ何もしない）。
        /// 別のアセットに変わったら、コンテナをすべて開いた状態から始める。
        /// </summary>
        public void Show(NodeGraphAsset asset)
        {
            var assetChanged = asset != _asset;
            if (assetChanged)
            {
                _asset = asset;
                _ids.Clear();
                _nextId = 1;
                _signature = null;
                _selectedNodeId = null;
            }

            var roots = NodeTree.Build(asset);
            var signature = GetSignature(roots);
            if (!assetChanged && signature == _signature)
            {
                return;
            }

            _signature = signature;
            _entries.Clear();
            _parents.Clear();
            _newContainerIds.Clear();
            _syncing = true;
            try
            {
                _tree.SetRootItems(ToItems(roots, 0));
                ForgetRemovedNodes();

                // 新しく出たコンテナ（別のアセットを開いたときはすべて）を、作り直す前に開いておく。
                // 作り直しの後に開くと、もう 1 回表示を作り直すことになる。閉じたものは、ユーザーが閉じたままにしている
                ExpandItems(_newContainerIds);

                RebuildRows();

                // 作り直しでは選び直すだけで、閉じたコンテナを開き直さない
                ApplySelection(reveal: false);
            }
            finally
            {
                _syncing = false;
            }
        }

        /// <summary>
        /// グラフで選んだノードをツリーでも選ぶ（<see cref="NodeSelected"/> は呼ばない）。入っているコンテナを開いて見える所まで送る。
        /// null なら選択を外す。
        /// </summary>
        public void Select(string nodeId)
        {
            var next = nodeId != null && _ids.ContainsKey(nodeId) ? nodeId : null;

            // 選択が変わったときだけ、入っているコンテナを開いて見せる（同じノードの選び直しで、ユーザーが閉じたコンテナを開かない）
            var changed = next != _selectedNodeId;
            _selectedNodeId = next;
            _syncing = true;
            try
            {
                ApplySelection(reveal: changed);
            }
            finally
            {
                _syncing = false;
            }
        }

        /// <summary>表示中の階層（コンテナの ID。ルートなら空文字）を強調する。</summary>
        public void ShowLevel(string containerId)
        {
            _levelContainerId = containerId ?? string.Empty;
            RefreshRows();
        }

        /// <summary>Play 中に実行中のノードを強調する（それを含むコンテナも薄く強調する）。null で消す。</summary>
        public void ShowRunning(string nodeId)
        {
            _runningNodeId = nodeId;
            RefreshRows();
        }

        /// <summary>そのノードの項目に付ける状態の USS クラス（コンテナ・表示中の階層・実行中・実行中のノードを含むコンテナ）。</summary>
        internal IEnumerable<string> GetStateClasses(string nodeId)
        {
            if (nodeId == null || !_ids.TryGetValue(nodeId, out var id) || !_entries.TryGetValue(id, out var entry))
            {
                yield break;
            }

            if (entry.IsContainer)
            {
                yield return ContainerClassName;
            }

            if (entry.IsContainer && nodeId == _levelContainerId)
            {
                yield return LevelClassName;
            }

            if (nodeId == _runningNodeId)
            {
                yield return RunningClassName;
            }
            else if (_runningNodeId != null && _ids.TryGetValue(_runningNodeId, out var runningId) && GetAncestors(runningId).Contains(id))
            {
                yield return RunningPathClassName;
            }
        }

        /// <summary>ツリーでノードを選んだときと同じ（テストと、項目の選択から呼ぶ）。</summary>
        internal void Choose(string nodeId, bool open)
        {
            if (nodeId == null || !_ids.TryGetValue(nodeId, out var id) || !_entries.TryGetValue(id, out var entry))
            {
                return;
            }

            if (open && entry.IsContainer)
            {
                NodeOpened?.Invoke(nodeId);
            }
            else
            {
                NodeSelected?.Invoke(nodeId);
            }
        }

        private List<TreeViewItemData<NodeTreeEntry>> ToItems(IEnumerable<NodeTreeEntry> entries, int parentId)
        {
            var items = new List<TreeViewItemData<NodeTreeEntry>>();
            foreach (var entry in entries)
            {
                if (!_ids.TryGetValue(entry.NodeId, out var id))
                {
                    id = _nextId++;
                    _ids[entry.NodeId] = id;
                    if (entry.IsContainer)
                    {
                        _newContainerIds.Add(id);
                    }
                }

                _entries[id] = entry;
                _parents[id] = parentId;
                items.Add(new TreeViewItemData<NodeTreeEntry>(id, entry, ToItems(entry.Children, id)));
            }

            return items;
        }

        // ツリーから消えたノードの ID を忘れる（覚えたままだと、Undo などで戻ったコンテナが「新しい」とみなされず、前の開閉の状態で出てしまう）
        private void ForgetRemovedNodes()
        {
            var present = new HashSet<string>(_entries.Values.Select(entry => entry.NodeId));
            foreach (var nodeId in _ids.Keys.Where(nodeId => !present.Contains(nodeId)).ToList())
            {
                _ids.Remove(nodeId);
            }
        }

        /// <summary>覚えている項目の ID の数（テスト用。消えたノードの分は残さない）。</summary>
        internal int KnownIdCount => _ids.Count;

        private IEnumerable<int> GetAncestors(int id)
        {
            var visited = new HashSet<int>();
            for (var parent = _parents.TryGetValue(id, out var p) ? p : 0; parent != 0 && visited.Add(parent);
                 parent = _parents.TryGetValue(parent, out var next) ? next : 0)
            {
                yield return parent;
            }
        }

        // 選んでいるノードを選ぶ（_syncing の中で呼ぶ）。reveal なら、入っているコンテナを開いて見える所まで送る
        private void ApplySelection(bool reveal)
        {
            if (_selectedNodeId == null || !_ids.TryGetValue(_selectedNodeId, out var id) || !_entries.ContainsKey(id))
            {
                _selectedNodeId = null;
                _tree.ClearSelection();
                return;
            }

            if (reveal)
            {
                // 開いているコンテナは開き直さない（開くものが無ければ表示も作り直さない）
                ExpandWithoutRefreshing(GetAncestors(id).Reverse().Where(ancestor => !_tree.IsExpanded(ancestor)).ToList());
            }
            else if (GetAncestors(id).Any(ancestor => !_tree.IsExpanded(ancestor)))
            {
                // TreeView は選んだ項目の親を開いてしまうので、閉じたコンテナの中なら項目は選ばない（選んでいるノードは覚えておく）
                _tree.ClearSelection();
                return;
            }

            _tree.SetSelectionById(id);

            if (reveal)
            {
                // レイアウトが済んでから送る（パネルに付く前は何もしない）
                _tree.schedule.Execute(() => _tree.ScrollToItemById(id));
            }
        }

        /// <summary>そのノードの項目が開いているか（テスト用）。</summary>
        internal bool IsExpanded(string nodeId) => nodeId != null && _ids.TryGetValue(nodeId, out var id) && _tree.IsExpanded(id);

        /// <summary>そのノードの行が、今ツリーに出ているか（テスト用。閉じたコンテナの中なら出ていない）。</summary>
        internal bool IsRowShown(string nodeId) =>
            nodeId != null && _ids.TryGetValue(nodeId, out var id) && _tree.viewController.GetIndexForId(id) >= 0;

        /// <summary>そのノードの項目を開く・閉じる（テスト用。ユーザーの ▶ の操作と同じ）。</summary>
        internal void SetExpanded(string nodeId, bool expanded)
        {
            if (nodeId == null || !_ids.TryGetValue(nodeId, out var id))
            {
                return;
            }

            if (expanded)
            {
                _tree.ExpandItem(id);
            }
            else
            {
                _tree.CollapseItem(id);
            }
        }

        // 閉じたコンテナの中のノードは、項目を選ばずに覚えておくだけにしている。コンテナが開いて見えるようになったら選び直す
        private void OnItemExpandedChanged()
        {
            if (_syncing || IsSelectionShown)
            {
                return;
            }

            _syncing = true;
            try
            {
                ApplySelection(reveal: false);
            }
            finally
            {
                _syncing = false;
            }
        }

        /// <summary>選んでいるノードが、ツリーの項目としても選ばれているか（テスト用。閉じたコンテナの中なら選ばれていない）。</summary>
        internal bool IsSelectionShown
        {
            get
            {
                var index = _tree.selectedIndex;
                return _selectedNodeId != null && index >= 0 && _tree.GetItemDataForIndex<NodeTreeEntry>(index)?.NodeId == _selectedNodeId;
            }
        }

        private void OnSelectionChanged()
        {
            var index = _tree.selectedIndex;
            var entry = index >= 0 ? _tree.GetItemDataForIndex<NodeTreeEntry>(index) : null;
            if (_syncing || entry == null)
            {
                return;
            }

            _selectedNodeId = entry.NodeId;

            // 選んだノードへ移ると、別の階層ならグラフとこのツリーが作り直される。選択の通知の中で作り直さないよう、後で行う
            schedule.Execute(() => Choose(entry.NodeId, false));
        }

        private void OnItemsChosen()
        {
            var index = _tree.selectedIndex;
            var entry = index >= 0 ? _tree.GetItemDataForIndex<NodeTreeEntry>(index) : null;
            if (entry != null)
            {
                schedule.Execute(() => Choose(entry.NodeId, true));
            }
        }

        // 項目をまとめて開く（表示は作り直さない。作り直すのは呼び出し側で 1 回だけ）
        private void ExpandItems(IEnumerable<int> ids)
        {
            foreach (var id in ids)
            {
                _tree.ExpandItem(id, false, false);
            }
        }

        // 項目をまとめて開き、表示の作り直しは最後に 1 回だけ行う（1 つ開くたびに作り直すと、コンテナが多いと重い）
        private void ExpandWithoutRefreshing(IReadOnlyCollection<int> ids)
        {
            if (ids.Count == 0)
            {
                return;
            }

            ExpandItems(ids);
            RefreshRows();
        }

        /// <summary>
        /// 表示を作り直した回数（テスト用）。項目を作り直す（<see cref="RebuildRows"/>）のも、表示だけ更新する（<see cref="RefreshRows"/>）のも、
        /// すべてここを通るので、どこで作り直しても数える。
        /// </summary>
        internal int RefreshCount { get; private set; }

        // 項目を作り直す（階層が変わったとき）
        private void RebuildRows()
        {
            RefreshCount++;
            _tree.Rebuild();
        }

        // 項目はそのままで表示を更新する（開閉・強調の変化）
        private void RefreshRows()
        {
            RefreshCount++;
            _tree.RefreshItems();
        }

        private static VisualElement MakeItem()
        {
            var row = new VisualElement();
            row.AddToClassList(ItemClassName);
            var icon = new VisualElement();
            icon.AddToClassList(IconClassName);
            var label = new Label();
            label.AddToClassList(LabelClassName);
            row.Add(icon);
            row.Add(label);
            return row;
        }

        private void BindItem(VisualElement element, int index)
        {
            var entry = _tree.GetItemDataForIndex<NodeTreeEntry>(index);
            var label = element.Q<Label>(className: LabelClassName);
            label.text = entry.Label;
            element.tooltip = entry.TypeName;

            var icon = element.Q(className: IconClassName);
            icon.ClearClassList();
            icon.AddToClassList(IconClassName);
            if (entry.Category.Length > 0)
            {
                icon.AddToClassList(CategoryClassPrefix + entry.Category);
            }

            var states = GetStateClasses(entry.NodeId).ToList();
            foreach (var className in new[] { ContainerClassName, LevelClassName, RunningClassName, RunningPathClassName })
            {
                element.EnableInClassList(className, states.Contains(className));
            }
        }

        // 階層・名前・カテゴリが同じなら作り直さない（インスペクタでの入力ごとに作り直さないように）
        private static string GetSignature(IEnumerable<NodeTreeEntry> entries)
        {
            var parts = new List<string>();
            Append(entries, 0, parts);
            return string.Join("\n", parts);

            static void Append(IEnumerable<NodeTreeEntry> level, int depth, List<string> into)
            {
                foreach (var entry in level)
                {
                    into.Add($"{depth}|{entry.NodeId}|{entry.Label}|{entry.Category}");
                    Append(entry.Children, depth + 1, into);
                }
            }
        }
    }
}
