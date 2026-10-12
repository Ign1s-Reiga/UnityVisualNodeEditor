using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor
{
    /// <summary>
    /// UI Toolkit のパネル。グラフが指定したノード（<c>node</c>。<see cref="GraphNodePath"/> の場所。コンテナなら、その中のどこか）にいる間だけ表示する
    /// （uGUI の Graph Node Activator と同じ）。ポーズ・ゲームオーバーの画面などを中に置く。UI Builder の Library に出る。
    /// <para>
    /// 動くのはゲームの画面のときだけで、UI Builder の中では隠さない（編集できるように）。いないとき・Runner が無いときは <c>display: none</c>。
    /// </para>
    /// </summary>
    [UxmlElement]
    public partial class GraphNodePanel : VisualElement
    {
        /// <summary>この要素の USS クラス。</summary>
        public const string GraphNodePanelUssClassName = "vne-graph-node-panel";

        [UxmlAttribute("graph")]
        private NodeGraphAsset _graph;

        [UxmlAttribute("node"), GraphNodePath]
        private string _node = string.Empty;

        private readonly GraphRunnerFollower _follower;
        private bool _connected;

        public GraphNodePanel()
        {
            AddToClassList(GraphNodePanelUssClassName);
            _follower = new GraphRunnerFollower(Refresh, _ => Refresh());
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        /// <summary>見るグラフ。null なら実行中のどれか 1 つ。</summary>
        public NodeGraphAsset Graph
        {
            get => _graph;
            set
            {
                _graph = value;
                _follower.Graph = value;
                Refresh();
            }
        }

        /// <summary>このノードの場所（例: <c>Paused</c>、<c>Round/Paused</c>）にいる間だけ表示する。</summary>
        public string Node
        {
            get => _node;
            set
            {
                _node = value ?? string.Empty;
                Refresh();
            }
        }

        /// <summary>最後に確かめたとき、グラフが指定したノードにいたか。</summary>
        public bool IsInNode { get; private set; }

        /// <summary>今のグラフの位置で、表示する・隠す（ゲームの画面に出ている間だけ）。</summary>
        public void Refresh()
        {
            if (!_connected)
            {
                return;
            }

            IsInNode = GraphNodePath.IsAt(_follower.Runner, _node);
            style.display = IsInNode ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>ゲームの画面に付いたとき: Runner を追い始め、表示を直す（テストからも呼ぶ）。</summary>
        internal void Connect()
        {
            _connected = true;
            _follower.Graph = _graph;
            _follower.Start();
            Refresh();
        }

        /// <summary>外れたとき: 追うのをやめる。</summary>
        internal void Disconnect()
        {
            _follower.Stop();
            _connected = false;
        }

        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
            if (evt.destinationPanel != null && evt.destinationPanel.contextType == ContextType.Player)
            {
                Connect();
            }
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            if (_connected)
            {
                Disconnect();
            }
        }
    }
}
