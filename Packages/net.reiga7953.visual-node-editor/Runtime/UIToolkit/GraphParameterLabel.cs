using UnityEngine;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor
{
    /// <summary>
    /// UI Toolkit のラベル。パラメータ（Blackboard）の値を書式（<c>Score: {0}</c>）で出す（uGUI の Graph Parameter Text と同じ）。
    /// UI Builder の Library に出る。動くのはゲームの画面のときだけで、UI Builder の中では置いた文字のまま。
    /// 書き換えるのは、画面に付いたとき・そのパラメータが変わったとき・Runner が始まった・止まったとき。Runner が無ければ書き換えない。
    /// </summary>
    [UxmlElement]
    public partial class GraphParameterLabel : Label
    {
        /// <summary>この要素の USS クラス。</summary>
        public const string GraphParameterLabelUssClassName = "vne-graph-parameter-label";

        [UxmlAttribute("graph")]
        private NodeGraphAsset _graph;

        [UxmlAttribute("parameter"), GraphParameterName]
        private string _parameter = string.Empty;

        [UxmlAttribute("format")]
        private string _format = "{0}";

        private readonly GraphRunnerFollower _follower;
        private bool _connected;
        private bool _warnedFormat;

        /// <summary>文字の無いラベル（UXML と UI Builder が使う）。<see cref="Parameter"/> と <see cref="Format"/> を決めて使う。</summary>
        public GraphParameterLabel() : this(string.Empty)
        {
        }

        /// <summary>Runner が無いうちは <paramref name="text"/> を出しておくラベル。</summary>
        /// <param name="text">Runner が無いときに出しておく文字。</param>
        public GraphParameterLabel(string text) : base(text)
        {
            AddToClassList(GraphParameterLabelUssClassName);
            _follower = new GraphRunnerFollower(Refresh, null, OnParameterChanged);
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        /// <summary>値を読むグラフ。null なら実行中のどれか 1 つ。</summary>
        public NodeGraphAsset Graph
        {
            get => _graph;
            set
            {
                _graph = value;
                // 画面に出ていれば、追う Runner を探し直して文字を直す（出ていなければ何もしない）
                _follower.Graph = value;
            }
        }

        /// <summary>出すパラメータの名前。</summary>
        public string Parameter
        {
            get => _parameter;
            set
            {
                _parameter = value ?? string.Empty;
                Refresh();
            }
        }

        /// <summary>書式（<c>{0}</c> が値）。</summary>
        public string Format
        {
            get => _format;
            set
            {
                _format = value ?? string.Empty;
                _warnedFormat = false;
                Refresh();
            }
        }

        /// <summary>今の値で文字を書き換える（ゲームの画面に出ている間だけ）。Runner・パラメータが無ければ何もしない。</summary>
        public void Refresh()
        {
            var runner = _connected ? _follower.Runner : null;
            if (runner == null || !runner.TryGetParameter(_parameter, out var value))
            {
                return;
            }

            if (!GraphParameterText.TryFormat(_format, value, out var formatted) && !_warnedFormat)
            {
                _warnedFormat = true;
                Debug.LogWarning($"[VisualNodeEditor] Graph Parameter Label '{name}': the format \"{_format}\" is not valid, so only the value is shown. Use {{0}} for the value.");
            }

            text = formatted;
        }

        /// <summary>ゲームの画面に付いたとき: Runner を追い始め、文字を書き換える（テストからも呼ぶ）。</summary>
        internal void Connect()
        {
            _connected = true;
            _follower.Graph = _graph;
            _follower.Start();
        }

        /// <summary>外れたとき: 追うのをやめる（文字はそのまま）。</summary>
        internal void Disconnect()
        {
            _follower.Stop();
            _connected = false;
        }

        private void OnParameterChanged(string parameter)
        {
            if (parameter == _parameter)
            {
                Refresh();
            }
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
