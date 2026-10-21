using System;
using System.Collections.Generic;
using System.Linq;
using Reiga.VisualNodeEditor.Editor.Views;
using UnityEditor;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Editor.Inspector
{
    /// <summary>
    /// ノードのインスペクタに出す Transitions の一覧。そのノードから出ているエッジを、遷移を調べる順（アセット内の順）に並べ、ドラッグで並べ替える。
    /// 行き先が 2 つ以上あるときだけ見える（グラフで繋ぎ足せば、作り直さなくても出る）。
    /// 選ばれることの無い行（<see cref="TransitionOrder"/>。検証の Warning と同じ判定）は薄く出し、理由をツールチップに出す。
    /// 並べ替えはすぐアセットへ書き（Undo 可）、<see cref="Changed"/> で知らせる。見た目は NodeGraphEditor.uss（.vne-transition-list）で決める。
    /// </summary>
    public sealed class TransitionListView : VisualElement
    {
        /// <summary>選ばれることの無い行の USS クラス。</summary>
        internal const string HiddenRowClass = "vne-transition-list__row--hidden";

        /// <summary>見出しの下の説明。</summary>
        internal const string Hint = "Checked from the top: the first one that matches is taken. Drag to reorder.";

        private const string FewClass = "vne-transition-list--few";

        private readonly NodeGraphAsset _asset;
        private readonly NodeData _node;
        private readonly List<EdgeData> _items = new();
        private readonly Dictionary<EdgeData, HiddenTransition> _hidden = new();
        private readonly ListView _list;

        /// <summary><paramref name="asset"/> の <paramref name="node"/> から出ているエッジの一覧を作る（<see cref="Supports"/> のノードに使う）。</summary>
        public TransitionListView(NodeGraphAsset asset, NodeData node)
        {
            _asset = asset;
            _node = node;
            AddToClassList("vne-transition-list");

            var header = new Label("Transitions");
            header.AddToClassList("vne-transition-list__header");
            Add(header);

            var hint = new Label(Hint);
            hint.AddToClassList("vne-transition-list__hint");
            Add(hint);

            _list = new ListView(_items, -1f, MakeItem, BindItem)
            {
                reorderable = true,
                reorderMode = ListViewReorderMode.Animated,
                selectionType = SelectionType.Single,
                showBorder = true,
                virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight,
            };
            _list.AddToClassList("vne-transition-list__list");
            _list.itemIndexChanged += OnItemIndexChanged;
            Add(_list);

            Reload();
        }

        /// <summary>並べ替えたとき。</summary>
        public event Action Changed;

        /// <summary>一覧に並べている（見えていれば表示している）エッジ。ノードから出ているエッジすべて、アセット内の順。</summary>
        internal IReadOnlyList<EdgeData> Items => _items;

        /// <summary>一覧が見えているか（行き先が 2 つ以上あるか）。</summary>
        internal bool IsShown => !ClassListContains(FewClass);

        /// <summary>一覧を出すノードか。コンテナ（出口ごとのエッジ）と Note は出さない。</summary>
        public static bool Supports(NodeData node) => node != null && !(node is ContainerNode) && !(node is NoteNode);

        /// <summary>その行が選ばれることの無い理由（無ければ null）。</summary>
        internal string GetHiddenReason(EdgeData edge) =>
            edge != null && _hidden.TryGetValue(edge, out var hidden) ? TransitionOrder.Describe(_node, hidden) : null;

        /// <summary>アセットから一覧を作り直す（グラフで繋いだ・外した、Undo したときなど）。</summary>
        public void Reload()
        {
            _items.Clear();
            _items.AddRange(OutgoingOf(_node.Id));
            _hidden.Clear();
            foreach (var hidden in TransitionOrder.FindHidden(_node, OutgoingOf, _asset.FindNode))
            {
                _hidden[hidden.Edge] = hidden;
            }

            EnableInClassList(FewClass, _items.Count < 2);
            _list.RefreshItems();
        }

        /// <summary>
        /// 一覧の <paramref name="edge"/> を、このノードから出ているエッジの <paramref name="index"/> 番目へ移す（Undo 可）。
        /// 一覧が古ければ（作り直す前に、グラフ・MCP・Undo でこのノードのエッジが足された・消された・差し替わった）、
        /// 一覧の位置がアセットの位置と合わないので、動かさずに作り直して false。
        /// </summary>
        internal bool Move(EdgeData edge, int index)
        {
            // ListView は並べ替え済みなので、順は比べずに、同じエッジがそろっているかだけを見る
            var current = OutgoingOf(_node.Id).ToList();
            if (edge == null || !_items.Contains(edge) || current.Count != _items.Count || current.Except(_items).Any())
            {
                Reload();
                return false;
            }

            Undo.RecordObject(_asset, "Reorder Transitions");
            _asset.MoveEdge(edge, index);
            EditorUtility.SetDirty(_asset);
            Reload();
            Changed?.Invoke();
            return true;
        }

        /// <summary>行の種類: 名前で起こす Event は Raise、自動の Event は Auto、それ以外（Advance・Entry・Event の先）は Next。</summary>
        internal static string GetKind(NodeData source, NodeData target) =>
            source is EventNode || !(target is EventNode eventNode) ? "Next" : eventNode.IsAutomatic ? "Auto" : "Raise";

        /// <summary>行の行き先: Event ならタイトル（イベント名）と条件（例: <c>Finish if Affection &gt;= 60</c>）、それ以外はノードの名前。</summary>
        internal static string GetLabel(NodeGraphAsset asset, NodeData target)
        {
            if (target == null)
            {
                return "(missing node)";
            }

            if (!(target is EventNode eventNode))
            {
                return NodeDisplay.GetNodeLabel(target);
            }

            // 名前の無い自動の Event は、種類（Auto）と同じ「Auto」を重ねて出さない
            var name = eventNode.HasCustomTitle || !string.IsNullOrWhiteSpace(eventNode.EventName) ? NodeDisplay.GetNodeLabel(eventNode) : string.Empty;
            var conditions = NodeDisplay.GetConditionsText(eventNode, asset);
            if (conditions.Length == 0)
            {
                return name.Length > 0 ? name : "(always)";
            }

            return name.Length > 0 ? $"{name} if {conditions}" : "if " + conditions;
        }

        /// <summary>Event の行の、その先（Event から出ている最初のエッジの先）。Event でなければ空文字。</summary>
        internal static string GetDestination(NodeGraphAsset asset, NodeData source, NodeData target)
        {
            if (source is EventNode || !(target is EventNode eventNode))
            {
                return string.Empty;
            }

            var next = asset.Edges.Where(e => e != null && e.FromNodeId == eventNode.Id)
                .Select(e => asset.FindNode(e.ToNodeId))
                .FirstOrDefault(n => n != null);
            return next != null ? "→ " + NodeDisplay.GetNodeLabel(next) : "→ (only notifies; stays here)";
        }

        private IEnumerable<EdgeData> OutgoingOf(string nodeId) => _asset.Edges.Where(e => e != null && e.FromNodeId == nodeId);

        private void OnItemIndexChanged(int oldIndex, int newIndex)
        {
            // ListView は itemsSource を並べ替え済み。動かしたエッジは newIndex にある
            if (newIndex >= 0 && newIndex < _items.Count)
            {
                Move(_items[newIndex], newIndex);
            }
        }

        private static VisualElement MakeItem()
        {
            var row = new VisualElement();
            row.AddToClassList("vne-transition-list__row");

            var number = new Label();
            number.AddToClassList("vne-transition-list__number");

            var kind = new Label();
            kind.AddToClassList("vne-transition-list__kind");

            var texts = new VisualElement();
            texts.AddToClassList("vne-transition-list__texts");
            var label = new Label();
            label.AddToClassList("vne-transition-list__label");
            var destination = new Label();
            destination.AddToClassList("vne-transition-list__destination");
            texts.Add(label);
            texts.Add(destination);

            row.Add(number);
            row.Add(kind);
            row.Add(texts);
            return row;
        }

        private void BindItem(VisualElement element, int index)
        {
            var edge = _items[index];
            var target = _asset.FindNode(edge.ToNodeId);
            var labels = element.Query<Label>().ToList();
            labels[0].text = (index + 1).ToString();
            labels[1].text = GetKind(_node, target);
            labels[2].text = GetLabel(_asset, target);

            var destination = GetDestination(_asset, _node, target);
            labels[3].text = destination;
            labels[3].EnableInClassList("vne-transition-list__destination--empty", destination.Length == 0);

            var reason = GetHiddenReason(edge);
            element.EnableInClassList(HiddenRowClass, reason != null);
            element.tooltip = reason ?? string.Empty;
        }
    }
}
