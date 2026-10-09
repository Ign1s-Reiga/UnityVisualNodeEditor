using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Editor.Views
{
    /// <summary>
    /// <see cref="NodeData"/> 1 件に対応する GraphView ノード。
    /// ノード種別ごとの UI はサブクラスで拡張し、<see cref="CustomNodeViewAttribute"/> で登録する。
    /// 生成は <see cref="NodeViewFactory.Create"/> を通すこと。
    /// </summary>
    public class NodeView : Node
    {
        /// <summary>標準の入力ポート名。</summary>
        public const string InputPortName = "in";

        /// <summary>標準の出力ポート名。</summary>
        public const string OutputPortName = "out";

        private const string StyleSheetPath = "VisualNodeEditor/NodeView";
        private const string CategoryClassPrefix = "vne-node--";
        private const string HidePortLabelsClassName = "vne-node--hide-port-labels";
        private const string WarningClassName = "vne-node--warning";
        private const string ErrorClassName = "vne-node--error";
        private const string RunningClassName = "vne-node--running";
        private const string VisitedClassName = "vne-node--visited";
        private const string CollapsedClassName = "vne-node--collapsed";
        private const string SearchMatchClassName = "vne-node--search-match";
        private const string SearchDimmedClassName = "vne-node--search-dimmed";

        private static StyleSheet _styleSheet;

        private readonly Label _summaryLabel = new Label();

        // 付いている振る舞い（NodeBehaviour）の一覧。ポートの下（extensionContainer）に出し、折りたたむと GraphView が隠す
        private readonly Label _behavioursLabel = new Label();

        /// <summary>この View が表示しているノードデータ。</summary>
        public NodeData Data { get; private set; }

        /// <summary>
        /// ノードが属するグラフ。コンテナの子の数や親コンテナの出口など、ほかのノードを見て表示を決める View が使う。
        /// グラフ無しで作られた View では null。
        /// </summary>
        protected NodeGraphAsset Graph { get; private set; }

        /// <summary>表示しているノードの ID。</summary>
        public string NodeId => viewDataKey;

        /// <summary>入力・出力ポートの数から、ポートラベル（in / out）を隠すかどうかを決める。</summary>
        public static bool ShouldHidePortLabels(int inputCount, int outputCount) => inputCount <= 1 && outputCount <= 1;

        /// <summary>
        /// ポートの ID（<see cref="EdgeData"/> に保存される値）。表示名（<see cref="Port.portName"/>）とは別に持つ。
        /// </summary>
        public static string GetPortId(Port port) => port?.userData as string ?? port?.portName;

        internal void Initialize(NodeData data, NodeGraphAsset graph = null)
        {
            Graph = graph;

            // GraphView の既定 USS はノード自身に付いているため、確実に上書きできるよう同じ要素に付ける
            _styleSheet ??= Resources.Load<StyleSheet>(StyleSheetPath);
            if (_styleSheet != null)
            {
                styleSheets.Add(_styleSheet);
            }

            viewDataKey = data.Id;
            AddToClassList("vne-node");
            var category = NodeCategory.FromType(data.GetType());
            if (category.Length > 0)
            {
                AddToClassList(CategoryClassPrefix + category);
            }

            _summaryLabel.AddToClassList("vne-node__summary");
            _behavioursLabel.AddToClassList("vne-node__behaviours");
            SetPosition(new Rect(data.Position, Vector2.zero));
            Rebind(data);

            CreatePorts();
            EnableInClassList(HidePortLabelsClassName, !AlwaysShowPortLabels
                && ShouldHidePortLabels(inputContainer.Query<Port>().ToList().Count, outputContainer.Query<Port>().ToList().Count));
            SetCollapsed(data.Collapsed);
            RefreshPorts();
        }

        /// <summary>タイトルの ▼ ボタンで折りたたみ・展開したとき（プログラムからの <see cref="SetCollapsed"/> では呼ばれない）。</summary>
        public event Action<NodeView> CollapsedChanged;

        /// <summary>折りたたまれているか。</summary>
        public bool IsCollapsed => !expanded;

        /// <summary>折りたたみ状態を変える（通知はしない）。折りたたむとサマリー行も隠す。</summary>
        public void SetCollapsed(bool collapsed)
        {
            expanded = !collapsed;
            EnableInClassList(CollapsedClassName, collapsed);
            RefreshExpandedState();
        }

        protected override void ToggleCollapse()
        {
            base.ToggleCollapse();
            EnableInClassList(CollapsedClassName, !expanded);
            CollapsedChanged?.Invoke(this);
        }

        /// <summary>
        /// 同じ ID のノードデータで表示（タイトル・サマリー）を更新する（インスペクタでの編集後など）。
        /// </summary>
        internal void Rebind(NodeData data)
        {
            Data = data;
            title = GetDisplayTitle();
            UpdateSummary(GetSummary());
            UpdateBehaviours();
        }

        /// <summary>ノードに出している振る舞いの一覧。振る舞いが無ければ空文字。</summary>
        public string BehavioursText => _behavioursLabel.parent != null ? _behavioursLabel.text : string.Empty;

        private void UpdateBehaviours()
        {
            var text = NodeDisplay.GetBehavioursText((Data as IBehaviourHost)?.Behaviours);
            _behavioursLabel.text = text;
            _behavioursLabel.tooltip = text.Length > 0 ? "Behaviours: " + text : string.Empty;

            var shown = _behavioursLabel.parent != null;
            if (text.Length == 0 && shown)
            {
                _behavioursLabel.RemoveFromHierarchy();
                RefreshExpandedState();
            }
            else if (text.Length > 0 && !shown)
            {
                extensionContainer.Add(_behavioursLabel);
                RefreshExpandedState();
            }
        }

        /// <summary>
        /// タイトルに出す文字列。既定はユーザーのタイトル（空なら型の表示名）。
        /// 別のデータから決めるノード（コンテナの Exit など）は上書きする。
        /// </summary>
        protected virtual string GetDisplayTitle() =>
            NodeDisplay.ResolveTitle(Data.Title, NodeDisplay.GetTypeDisplayName(Data.GetType()));

        /// <summary>このノードに関する検証結果を枠の色とツールチップに反映する。空なら表示を消す。</summary>
        public void ShowIssues(IReadOnlyList<GraphIssue> issues)
        {
            var hasError = issues.Any(i => i.Severity == GraphIssueSeverity.Error);
            EnableInClassList(ErrorClassName, hasError);
            EnableInClassList(WarningClassName, !hasError && issues.Count > 0);
            tooltip = string.Join("\n", issues.Select(i => i.Message));
        }

        /// <summary>
        /// 検索結果を表示に反映する。検索中（<paramref name="searching"/>）なら一致するノードを強調し、それ以外を薄くする。
        /// </summary>
        public void ShowSearchResult(bool searching, bool isMatch)
        {
            EnableInClassList(SearchMatchClassName, searching && isMatch);
            EnableInClassList(SearchDimmedClassName, searching && !isMatch);
        }

        /// <summary>Play 中に GraphRunner が今いるノードとして強調するか。</summary>
        public bool IsRunning
        {
            get => ClassListContains(RunningClassName);
            set => EnableInClassList(RunningClassName, value);
        }

        /// <summary>Play 中に直近で通ったノードとして薄く強調するか（軌跡）。</summary>
        public bool IsVisited
        {
            get => ClassListContains(VisitedClassName);
            set => EnableInClassList(VisitedClassName, value);
        }

        /// <summary>
        /// 指定した向き・ID のポートを返す。存在しなければ null。
        /// </summary>
        public Port FindPort(string portId, Direction direction)
        {
            var container = direction == Direction.Input ? inputContainer : outputContainer;
            return container.Query<Port>().Where(p => GetPortId(p) == portId).First();
        }

        /// <summary>現在表示しているサマリー。行を出していなければ空文字。</summary>
        public string Summary => _summaryLabel.parent != null ? _summaryLabel.text : string.Empty;

        /// <summary>
        /// タイトル直下に出す 1 行のサマリー。<see cref="Data"/> から型ごとに決める。空文字なら行ごと出さない。
        /// 複数行を返した場合は空でない最初の行だけを表示する。
        /// </summary>
        protected virtual string GetSummary() => string.Empty;

        /// <summary>
        /// ポートが入力・出力 1 つずつ以下でも、ポートラベルを出すか。ラベルが意味を持つノード（コンテナの出口など）で true にする。
        /// </summary>
        protected virtual bool AlwaysShowPortLabels => false;

        /// <summary>ポートラベル（in / out や出口の名前）を隠しているか。</summary>
        public bool PortLabelsHidden => ClassListContains(HidePortLabelsClassName);

        /// <summary>ポートを定義する。既定ではポートを持たない。</summary>
        protected virtual void CreatePorts()
        {
        }

        /// <summary>入力ポートを追加する。<paramref name="portId"/> がエッジに保存される ID で、表示名も兼ねる。</summary>
        protected Port AddInputPort(string portId, Port.Capacity capacity = Port.Capacity.Multi) =>
            AddPort(Direction.Input, portId, portId, capacity);

        /// <summary>出力ポートを追加する。<paramref name="portId"/> がエッジに保存される ID で、表示名も兼ねる。</summary>
        protected Port AddOutputPort(string portId, Port.Capacity capacity = Port.Capacity.Multi) =>
            AddPort(Direction.Output, portId, portId, capacity);

        /// <summary>
        /// ID と表示名を分けて出力ポートを追加する（コンテナの出口: ID = 出口の ID、表示名 = 出口の名前）。
        /// 表示名を変えてもエッジは壊れない。
        /// </summary>
        protected Port AddOutputPort(string portId, string label, Port.Capacity capacity = Port.Capacity.Multi) =>
            AddPort(Direction.Output, portId, label, capacity);

        /// <summary>指定した向きの最初のポート。無ければ null。</summary>
        public Port FirstPort(Direction direction) =>
            (direction == Direction.Input ? inputContainer : outputContainer).Q<Port>();

        private Port AddPort(Direction direction, string portId, string label, Port.Capacity capacity)
        {
            // 空き地へエッジを落としたときにノードを作って繋げるよう、独自のポートを使う
            var port = NodePort.Create(direction, capacity);
            port.portName = label;
            port.userData = portId;
            (direction == Direction.Input ? inputContainer : outputContainer).Add(port);
            return port;
        }

        private void UpdateSummary(string summary)
        {
            var line = NodeDisplay.FirstLine(summary);
            _summaryLabel.text = line;

            if (line.Length == 0)
            {
                _summaryLabel.RemoveFromHierarchy();
            }
            else if (_summaryLabel.parent == null)
            {
                var container = titleContainer.parent;
                container.Insert(container.IndexOf(titleContainer) + 1, _summaryLabel);
            }
        }
    }
}
