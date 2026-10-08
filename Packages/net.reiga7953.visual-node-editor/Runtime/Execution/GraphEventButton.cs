using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;

namespace Reiga.VisualNodeEditor
{
    /// <summary>
    /// ボタンからグラフへイベントを送るコンポーネント。UI Button と同じ GameObject に付けると、クリックで、
    /// そのグラフを実行中の Runner に Raise（または Advance）する。
    /// <para>
    /// Runner は <see cref="GraphRunner.Running"/> から探すので、Graph Runner への参照は要らない
    /// （別のシーンに DontDestroyOnLoad で残っている Runner にも届く）。コードは書かなくてよい。
    /// </para>
    /// <para>
    /// 同じ GameObject にある、public な <c>onClick</c>（<see cref="UnityEvent"/>）を持つコンポーネント（uGUI の Button など）に自動で繋ぐ。
    /// それ以外から送るときは、任意の UnityEvent から <see cref="Send"/> を呼ぶ。uGUI に依存しないよう、型ではなく名前で探す。
    /// </para>
    /// </summary>
    [AddComponentMenu("Visual Node Editor/Graph Event Button")]
    public sealed class GraphEventButton : MonoBehaviour
    {
        private const string ClickEventName = "onClick";

        [Tooltip("このグラフを実行中の Graph Runner へ送る。空なら、実行中のどれか 1 つへ送る。")]
        [SerializeField] private NodeGraphAsset _graph;

        [Tooltip("Raise: イベント名で遷移する。Advance: イベントを介さずに次のノードへ進む。")]
        [SerializeField] private GraphEventButtonAction _action = GraphEventButtonAction.Raise;

        [Tooltip("Raise するイベント名（グラフの Event ノードの名前）。")]
        [SerializeField] private string _eventName = string.Empty;

        [Tooltip("同じ GameObject のボタン（onClick を持つコンポーネント）が押されたら送る。")]
        [SerializeField] private bool _sendOnClick = true;

        private UnityEvent _clickEvent;

        /// <summary>送り先のグラフ。null なら実行中のどれか 1 つ。</summary>
        public NodeGraphAsset Graph
        {
            get => _graph;
            set => _graph = value;
        }

        /// <summary>送る操作。</summary>
        public GraphEventButtonAction Action
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

        /// <summary>同じ GameObject のボタンのクリックで送るか。</summary>
        public bool SendOnClick
        {
            get => _sendOnClick;
            set => _sendOnClick = value;
        }

        /// <summary>実行中の Runner へ送る。UnityEvent（Button の OnClick など）から直接呼べる。</summary>
        public void Send() => TrySend();

        /// <summary>
        /// 実行中の Runner へ送る。遷移した（または通知の後で遷移する）なら true。
        /// Runner が見つからない・その名前の遷移が今のノードから無ければ false を返し、開発ビルドでは警告を出す。
        /// </summary>
        public bool TrySend()
        {
            var runner = FindRunner(_graph);
            if (runner == null)
            {
                Warn(_graph != null
                    ? $"No Graph Runner is running '{_graph.name}', so '{name}' did nothing."
                    : $"No Graph Runner is running, so '{name}' did nothing.");
                return false;
            }

            var moved = _action == GraphEventButtonAction.Advance ? runner.Advance() : runner.Raise(_eventName);
            if (!moved)
            {
                Warn(_action == GraphEventButtonAction.Advance
                    ? $"'{name}': there is nothing to advance to from '{runner.Current?.Title}'."
                    : string.IsNullOrEmpty(_eventName)
                        ? $"'{name}' has no event name to raise."
                        : $"'{name}': '{_eventName}' does not lead anywhere from '{runner.Current?.Title}'.");
            }

            return moved;
        }

        /// <summary><paramref name="graph"/> を実行中の Runner（null なら実行中のどれか）。無ければ null。</summary>
        internal static GraphRunner FindRunner(NodeGraphAsset graph) =>
            GraphRunner.Running.FirstOrDefault(runner => runner.IsRunning && (graph == null || runner.Graph == graph));

        /// <summary>
        /// <paramref name="gameObject"/> にある、public な <c>onClick</c> プロパティかフィールド（<see cref="UnityEvent"/>）。無ければ null。
        /// </summary>
        internal static UnityEvent FindClickEvent(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return null;
            }

            foreach (var component in gameObject.GetComponents<Component>())
            {
                if (component == null || component is GraphEventButton)
                {
                    continue;
                }

                var clickEvent = FindClickEventOn(component);
                if (clickEvent != null)
                {
                    return clickEvent;
                }
            }

            return null;
        }

        /// <summary><paramref name="source"/> の public な <c>onClick</c> プロパティかフィールド（<see cref="UnityEvent"/>）。無ければ null。</summary>
        internal static UnityEvent FindClickEventOn(object source)
        {
            if (source == null)
            {
                return null;
            }

            var type = source.GetType();
            var property = type.GetProperty(ClickEventName, BindingFlags.Public | BindingFlags.Instance);
            if (property != null && typeof(UnityEvent).IsAssignableFrom(property.PropertyType)
                && property.GetIndexParameters().Length == 0 && property.GetValue(source) is UnityEvent fromProperty)
            {
                return fromProperty;
            }

            var field = type.GetField(ClickEventName, BindingFlags.Public | BindingFlags.Instance);
            return field != null ? field.GetValue(source) as UnityEvent : null;
        }

        private void OnEnable() => HookClick(FindClickEvent(gameObject));

        private void OnDisable() => UnhookClick();

        /// <summary><paramref name="clickEvent"/>（ボタンのクリック）に <see cref="Send"/> を繋ぐ。<see cref="SendOnClick"/> でなければ繋がない。</summary>
        internal void HookClick(UnityEvent clickEvent)
        {
            UnhookClick();
            if (_sendOnClick && clickEvent != null)
            {
                _clickEvent = clickEvent;
                _clickEvent.AddListener(Send);
            }
        }

        internal void UnhookClick()
        {
            _clickEvent?.RemoveListener(Send);
            _clickEvent = null;
        }

        private void Warn(string message)
        {
            if (Debug.isDebugBuild)
            {
                Debug.LogWarning("[VisualNodeEditor] " + message, this);
            }
        }
    }
}
