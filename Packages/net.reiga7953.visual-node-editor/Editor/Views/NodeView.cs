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

        private static StyleSheet _styleSheet;

        private readonly Label _summaryLabel = new Label();

        /// <summary>この View が表示しているノードデータ。</summary>
        public NodeData Data { get; private set; }

        /// <summary>表示しているノードの ID。</summary>
        public string NodeId => viewDataKey;

        /// <summary>入力・出力ポートの数から、ポートラベル（in / out）を隠すかどうかを決める。</summary>
        public static bool ShouldHidePortLabels(int inputCount, int outputCount) => inputCount <= 1 && outputCount <= 1;

        internal void Initialize(NodeData data)
        {
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
            SetPosition(new Rect(data.Position, Vector2.zero));
            Rebind(data);

            CreatePorts();
            EnableInClassList(HidePortLabelsClassName,
                ShouldHidePortLabels(inputContainer.Query<Port>().ToList().Count, outputContainer.Query<Port>().ToList().Count));
            RefreshExpandedState();
            RefreshPorts();
        }

        /// <summary>
        /// 同じ ID のノードデータで表示（タイトル・サマリー）を更新する（インスペクタでの編集後など）。
        /// </summary>
        internal void Rebind(NodeData data)
        {
            Data = data;
            title = NodeDisplay.ResolveTitle(data.Title, NodeDisplay.GetTypeDisplayName(data.GetType()));
            UpdateSummary(GetSummary());
        }

        /// <summary>このノードに関する検証結果を枠の色とツールチップに反映する。空なら表示を消す。</summary>
        public void ShowIssues(IReadOnlyList<GraphIssue> issues)
        {
            var hasError = issues.Any(i => i.Severity == GraphIssueSeverity.Error);
            EnableInClassList(ErrorClassName, hasError);
            EnableInClassList(WarningClassName, !hasError && issues.Count > 0);
            tooltip = string.Join("\n", issues.Select(i => i.Message));
        }

        /// <summary>
        /// 指定した向き・名前のポートを返す。存在しなければ null。
        /// </summary>
        public Port FindPort(string portName, Direction direction)
        {
            var container = direction == Direction.Input ? inputContainer : outputContainer;
            return container.Query<Port>().Where(p => p.portName == portName).First();
        }

        /// <summary>現在表示しているサマリー。行を出していなければ空文字。</summary>
        public string Summary => _summaryLabel.parent != null ? _summaryLabel.text : string.Empty;

        /// <summary>
        /// タイトル直下に出す 1 行のサマリー。<see cref="Data"/> から型ごとに決める。空文字なら行ごと出さない。
        /// 複数行を返した場合は空でない最初の行だけを表示する。
        /// </summary>
        protected virtual string GetSummary() => string.Empty;

        /// <summary>ポートを定義する。既定ではポートを持たない。</summary>
        protected virtual void CreatePorts()
        {
        }

        /// <summary>入力ポートを追加する。<paramref name="portName"/> がそのままエッジのポート名になる。</summary>
        protected Port AddInputPort(string portName, Port.Capacity capacity = Port.Capacity.Multi)
        {
            var port = InstantiatePort(Orientation.Horizontal, Direction.Input, capacity, typeof(bool));
            port.portName = portName;
            inputContainer.Add(port);
            return port;
        }

        /// <summary>出力ポートを追加する。<paramref name="portName"/> がそのままエッジのポート名になる。</summary>
        protected Port AddOutputPort(string portName, Port.Capacity capacity = Port.Capacity.Multi)
        {
            var port = InstantiatePort(Orientation.Horizontal, Direction.Output, capacity, typeof(bool));
            port.portName = portName;
            outputContainer.Add(port);
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
