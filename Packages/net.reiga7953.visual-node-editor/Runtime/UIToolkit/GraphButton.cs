using System;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor
{
    /// <summary>
    /// UI Toolkit のボタン。クリックで、グラフを実行中の Runner に Raise / Advance / Save / Load する（uGUI の Graph Event Button と同じ）。
    /// UI Builder の Library に出るので、画面に置いて属性を決めるだけで使える（コード無し）。
    /// <para>
    /// 動くのはゲームの画面（パネルの <c>contextType</c> が Player）のときだけ。UI Builder の中では何もしない。
    /// Load で <c>disable-without-save</c> なら、スロットから再開できないうちは押せないようにする（戻すのは自分が押せないようにしたときだけ）。
    /// </para>
    /// </summary>
    [UxmlElement]
    public partial class GraphButton : Button
    {
        /// <summary>この要素の USS クラス。</summary>
        public const string GraphButtonUssClassName = "vne-graph-button";

        [UxmlAttribute("graph")]
        private NodeGraphAsset _graph;

        [UxmlAttribute("action")]
        private GraphEventButtonAction _action = GraphEventButtonAction.Raise;

        [UxmlAttribute("event-name"), EventName]
        private string _eventName = string.Empty;

        [UxmlAttribute("save-slot")]
        private string _slot = GraphEventButton.DefaultSlot;

        [UxmlAttribute("disable-without-save")]
        private bool _disableWithoutSave = true;

        private readonly GraphRunnerFollower _follower;
        private bool _connected;
        private bool _disabledWithoutSave;

        public GraphButton() : this(string.Empty)
        {
        }

        /// <param name="text">ボタンの文字。</param>
        public GraphButton(string text)
        {
            this.text = text;
            AddToClassList(GraphButtonUssClassName);
            _follower = new GraphRunnerFollower(RefreshEnabled);
            clicked += OnClicked;
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        /// <summary>送り先のグラフ。null なら実行中のどれか 1 つ。</summary>
        public NodeGraphAsset Graph
        {
            get => _graph;
            set
            {
                _graph = value;
                _follower.Graph = value;
                RefreshEnabled();
            }
        }

        /// <summary>送る操作。</summary>
        public GraphEventButtonAction Action
        {
            get => _action;
            set
            {
                _action = value;
                RefreshEnabled();
            }
        }

        /// <summary>Raise するイベント名。</summary>
        public string EventName
        {
            get => _eventName;
            set => _eventName = value ?? string.Empty;
        }

        /// <summary>Save / Load のスロット名。</summary>
        public string Slot
        {
            get => _slot;
            set
            {
                _slot = value ?? string.Empty;
                RefreshEnabled();
            }
        }

        /// <summary>Load のとき、スロットから再開できないうちは押せないようにするか。</summary>
        public bool DisableWithoutSave
        {
            get => _disableWithoutSave;
            set
            {
                _disableWithoutSave = value;
                RefreshEnabled();
            }
        }

        /// <summary>実行中の Runner へ送る。遷移した（または通知の後で遷移する）、保存した、ロードしたなら true。送れなければ開発ビルドで警告を出す。</summary>
        public bool TrySend() => GraphEventSender.Send(_graph, _action, _eventName, _slot, string.IsNullOrEmpty(name) ? text : name, null);

        /// <summary>
        /// Load のボタンを、スロットから再開できるときだけ押せるようにする（<see cref="DisableWithoutSave"/> で Load のときだけ。Graph Event Button と同じ規則）。
        /// ゲームの画面に出ている間だけ働く（Runner の開始・停止とセーブの知らせで呼ばれる）。
        /// </summary>
        public void RefreshEnabled()
        {
            if (!_connected)
            {
                return;
            }

            if (!GraphEventSender.ShouldDisableLoad(_graph, _action, _slot, _disableWithoutSave))
            {
                RestoreEnabled();
                return;
            }

            if (enabledSelf)
            {
                SetEnabled(false);
                _disabledWithoutSave = true;
            }
        }

        /// <summary>ゲームの画面に付いたとき: Runner とセーブの知らせを聞き始め、押せるかを直す（テストからも呼ぶ）。</summary>
        internal void Connect()
        {
            _connected = true;
            _follower.Graph = _graph;
            GraphSaveSlots.SlotChanged += OnSlotChanged;
            _follower.Start();
            RefreshEnabled();
        }

        /// <summary>外れたとき: 聞くのをやめ、押せないようにしていたら戻す。</summary>
        internal void Disconnect()
        {
            GraphSaveSlots.SlotChanged -= OnSlotChanged;
            _follower.Stop();
            RestoreEnabled();
            _connected = false;
        }

        private void RestoreEnabled()
        {
            if (_disabledWithoutSave)
            {
                _disabledWithoutSave = false;
                SetEnabled(true);
            }
        }

        // 大文字・小文字だけが違う名前も同じファイルのことがある（Windows・macOS）。確かめ直すだけなので、別のファイルでも害は無い
        private void OnSlotChanged(NodeGraphAsset graph, string slot)
        {
            if (string.Equals(slot, _slot, StringComparison.OrdinalIgnoreCase))
            {
                RefreshEnabled();
            }
        }

        private void OnClicked()
        {
            if (panel == null || panel.contextType == ContextType.Player)
            {
                TrySend();
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
