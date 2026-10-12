using UnityEngine;

namespace Reiga.VisualNodeEditor
{
    /// <summary>
    /// キーを押したら、グラフを実行中の Runner に Raise（または Advance）するコンポーネント（Esc → <c>Pause</c> など）。
    /// Runner は <see cref="GraphRunner.FindRunning"/> で探すので、Graph Runner への参照は要らない。キーは <see cref="GraphInput"/> で読む
    /// （旧 Input Manager でも Input System でも動く）。
    /// <para>
    /// 送れなくても（今のノードからその名前の遷移が無い）何も言わない。キーはどの画面でも押せるので、同じキーをノードごとに別の意味で使えるように。
    /// 同じキーの Graph Event Key が 2 つあると、同じフレームにどちらも送る（順は決まらない）。
    /// </para>
    /// </summary>
    [AddComponentMenu("Visual Node Editor/Graph Event Key")]
    public sealed class GraphEventKey : MonoBehaviour
    {
        [Tooltip("このグラフを実行中の Graph Runner へ送る。空なら、実行中のどれか 1 つへ送る。")]
        [SerializeField] private NodeGraphAsset _graph;

        [Tooltip("押したら送るキー。")]
        [SerializeField] private KeyCode _key = KeyCode.Space;

        [Tooltip("Raise: イベント名で遷移する。Advance: イベントを介さずに次のノードへ進む。")]
        [SerializeField] private GraphEventKeyAction _action = GraphEventKeyAction.Raise;

        [Tooltip("Raise するイベント名（グラフの Event ノードの名前）。")]
        [SerializeField] private string _eventName = string.Empty;

        /// <summary>送り先のグラフ。null なら実行中のどれか 1 つ。</summary>
        public NodeGraphAsset Graph
        {
            get => _graph;
            set => _graph = value;
        }

        /// <summary>押したら送るキー。</summary>
        public KeyCode Key
        {
            get => _key;
            set => _key = value;
        }

        /// <summary>送る操作。</summary>
        public GraphEventKeyAction Action
        {
            get => _action;
            set => _action = value;
        }

        /// <summary>Raise するイベント名。</summary>
        public string EventName
        {
            get => _eventName;
            set => _eventName = value ?? string.Empty;
        }

        private void Update() => CheckKey();

        /// <summary>キーがこのフレームに押されていれば送る（毎フレーム。テストからも呼ぶ）。送って遷移したら true。</summary>
        internal bool CheckKey() => GraphInput.GetKeyDown(_key) && TrySend();

        /// <summary>今すぐ送る（キーを押したときと同じ）。遷移したら true。Runner が無い・遷移が無ければ何もせず false。</summary>
        public bool TrySend()
        {
            var runner = GraphRunner.FindRunning(_graph);
            if (runner == null)
            {
                return false;
            }

            return _action == GraphEventKeyAction.Advance ? runner.Advance() : !string.IsNullOrEmpty(_eventName) && runner.Raise(_eventName);
        }
    }
}
