using System;
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
        private const string InteractableName = "interactable";

        [Tooltip("このグラフを実行中の Graph Runner へ送る。空なら、実行中のどれか 1 つへ送る。")]
        [SerializeField] private NodeGraphAsset _graph;

        [Tooltip("Raise: イベント名で遷移する。Advance: イベントを介さずに次のノードへ進む。")]
        [SerializeField] private GraphEventButtonAction _action = GraphEventButtonAction.Raise;

        [Tooltip("Raise するイベント名（グラフの Event ノードの名前）。")]
        [SerializeField] private string _eventName = string.Empty;

        [Tooltip("Save / Load のスロット名（英数字・空白・'-'・'_'）。")]
        [SerializeField] private string _slot = DefaultSlot;

        [Tooltip("同じ GameObject のボタン（onClick を持つコンポーネント）が押されたら送る。")]
        [SerializeField] private bool _sendOnClick = true;

        [Tooltip("Load のとき、スロットにセーブが無ければ、同じ GameObject のボタン（interactable を持つコンポーネント）を押せないようにする。")]
        [SerializeField] private bool _disableWithoutSave = true;

        private UnityEvent _clickEvent;

        // クリックを繋いだコンポーネント（onClick を持つもの。押せる・押せないもこれを変える）
        private Component _clickSource;

        // セーブが無いので押せないようにしたコンポーネント。戻すのはこれだけ（選び直すと、ほかで押せないようにしたものを点けることがある）
        private Component _disabledTarget;

        // Enable から Disable までの間（セーブの知らせを聞いている）。この間に設定を変えたら、押せるかを直す
        private bool _listening;

        /// <summary>送り先のグラフ。null なら実行中のどれか 1 つ。</summary>
        public NodeGraphAsset Graph
        {
            get => _graph;
            set
            {
                _graph = value;
                RefreshIfListening();
            }
        }

        /// <summary>送る操作。</summary>
        public GraphEventButtonAction Action
        {
            get => _action;
            set
            {
                _action = value;
                RefreshIfListening();
            }
        }

        /// <summary>Raise するイベント名。</summary>
        public string EventName
        {
            get => _eventName;
            set => _eventName = value ?? string.Empty;
        }

        /// <summary>Save / Load の既定のスロット名。</summary>
        public const string DefaultSlot = "slot1";

        /// <summary>Save / Load のスロット名。</summary>
        public string Slot
        {
            get => _slot;
            set
            {
                _slot = value ?? string.Empty;
                RefreshIfListening();
            }
        }

        /// <summary>同じ GameObject のボタンのクリックで送るか。</summary>
        public bool SendOnClick
        {
            get => _sendOnClick;
            set => _sendOnClick = value;
        }

        /// <summary>Load のとき、スロットにセーブが無ければボタンを押せないようにするか（<see cref="RefreshInteractable"/>）。</summary>
        public bool DisableWithoutSave
        {
            get => _disableWithoutSave;
            set
            {
                _disableWithoutSave = value;
                RefreshIfListening();
            }
        }

        /// <summary>
        /// Load のボタンを、スロットから再開できる（<see cref="GraphSaveSlots.CanLoad"/>）ときだけ押せるようにする（<see cref="DisableWithoutSave"/> で Load のときだけ）。
        /// 有効になったとき、スロットに保存した・消したとき（<see cref="GraphSaveSlots.SlotChanged"/>）、グラフが空なら Runner が始まったとき、
        /// 有効な間にグラフ・操作・スロット・<see cref="DisableWithoutSave"/> を変えたときにも呼ばれる。
        /// 変えるのは、クリックを繋いだコンポーネント（無ければ同じ GameObject の、public な bool の <c>interactable</c> を持つ最初のもの。uGUI の Button など）。
        /// 押せないようにするだけで、押せるように戻すのは、自分が押せないようにしたコンポーネントだけ（作者やほかのスクリプトが押せないようにしたボタンは点けない）。
        /// セーブができたときのほか、<see cref="DisableWithoutSave"/> を切った・Load 以外の操作にした・このコンポーネントが無効になったときにも戻す。
        /// グラフが空で実行中の Runner も無いと、どのグラフのセーブか分からないので押せないようにはしない。
        /// </summary>
        public void RefreshInteractable()
        {
            // Load で DisableWithoutSave のボタンだけが確かめる（ほかのボタンは、セーブの知らせのたびにコンポーネントを探したり、ファイルを読んだりしない）
            if (!GraphEventSender.ShouldDisableLoad(_graph, _action, _slot, _disableWithoutSave))
            {
                RestoreInteractable();
                return;
            }

            var target = _disabledTarget != null ? _disabledTarget : ChooseInteractableTarget(_clickSource, gameObject);
            if (target != null && GetInteractable(target))
            {
                SetInteractable(target, false);
                _disabledTarget = target;
            }
        }

        // 自分が押せないようにしたコンポーネントを押せるように戻す（無い・壊れていれば何もしない）
        private void RestoreInteractable()
        {
            var target = _disabledTarget;
            _disabledTarget = null;
            if (target != null)
            {
                SetInteractable(target, true);
            }
        }

        // 有効な間に、押せるかに関わる設定が変わったら確かめ直す（無効な間は、有効になったときに確かめる）
        private void RefreshIfListening()
        {
            if (_listening)
            {
                RefreshInteractable();
            }
        }

        /// <summary>
        /// 押せる・押せないを変える相手: <paramref name="clickSource"/>（クリックを繋いだ、onClick を持つコンポーネント）が bool の <c>interactable</c> を持てばそれ。
        /// 無ければ <paramref name="gameObject"/> の、それを持つ最初のコンポーネント（例えば、ボタンの前にある CanvasGroup を変えないように）。無ければ null。
        /// パッケージを uGUI に依存させないため、<c>onClick</c> と同じく型ではなく名前で探す。
        /// </summary>
        internal static Component ChooseInteractableTarget(Component clickSource, GameObject gameObject)
        {
            if (FindInteractableProperty(clickSource) != null)
            {
                return clickSource;
            }

            return gameObject == null
                ? null
                : gameObject.GetComponents<Component>().FirstOrDefault(component => !(component is GraphEventButton) && FindInteractableProperty(component) != null);
        }

        private static bool GetInteractable(Component target) => (bool)FindInteractableProperty(target).GetValue(target);

        private static void SetInteractable(Component target, bool value) => FindInteractableProperty(target).SetValue(target, value);

        private static PropertyInfo FindInteractableProperty(Component component)
        {
            if (component == null)
            {
                return null;
            }

            var property = component.GetType().GetProperty(InteractableName, BindingFlags.Public | BindingFlags.Instance);
            return property != null && property.PropertyType == typeof(bool) && property.CanRead && property.CanWrite && property.GetIndexParameters().Length == 0
                ? property
                : null;
        }

        /// <summary>実行中の Runner へ送る。UnityEvent（Button の OnClick など）から直接呼べる。</summary>
        public void Send() => TrySend();

        /// <summary>
        /// 実行中の Runner へ送る。遷移した（または通知の後で遷移する）、保存した、ロードしたなら true。
        /// Runner が見つからない・その名前の遷移が今のノードから無い・スロットが無ければ false を返し、開発ビルドでは警告を出す。
        /// </summary>
        public bool TrySend() => GraphEventSender.Send(_graph, _action, _eventName, _slot, name, this);

        /// <summary><paramref name="graph"/> を実行中の Runner（null なら実行中のどれか）。無ければ null。</summary>
        internal static GraphRunner FindRunner(NodeGraphAsset graph) => GraphRunner.FindRunning(graph);

        /// <summary>
        /// <paramref name="gameObject"/> にある、public な <c>onClick</c> プロパティかフィールド（<see cref="UnityEvent"/>）。無ければ null。
        /// </summary>
        internal static UnityEvent FindClickEvent(GameObject gameObject) => FindClickEventOn(FindClickSource(gameObject));

        /// <summary><paramref name="gameObject"/> にある、public な <c>onClick</c>（<see cref="UnityEvent"/>）を持つ最初のコンポーネント。無ければ null。</summary>
        internal static Component FindClickSource(GameObject gameObject) =>
            gameObject == null
                ? null
                : gameObject.GetComponents<Component>().FirstOrDefault(component =>
                    component != null && !(component is GraphEventButton) && FindClickEventOn(component) != null);

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

        private void OnEnable() => Enable();

        private void OnDisable() => Disable();

        /// <summary>
        /// 有効になったとき: ボタンのクリックに繋ぎ、セーブの知らせと Runner の開始を聞き、押せるかを直す（テストからも呼ぶ）。
        /// </summary>
        internal void Enable()
        {
            _clickSource = FindClickSource(gameObject);
            HookClick(FindClickEventOn(_clickSource));
            GraphSaveSlots.SlotChanged += OnSlotChanged;
            GraphRunner.Started += OnRunnerStarted;
            _listening = true;
            RefreshInteractable();
        }

        /// <summary>
        /// 無効になったとき（GameObject を隠した・壊したときも）: <see cref="Enable"/> で繋いだものを外し、押せないようにしたボタンを戻す
        /// （知らせを聞かなくなるので、押せないままにしない。有効になれば確かめ直す）。
        /// </summary>
        internal void Disable()
        {
            _listening = false;
            GraphSaveSlots.SlotChanged -= OnSlotChanged;
            GraphRunner.Started -= OnRunnerStarted;
            UnhookClick();
            RestoreInteractable();
        }

        // 同じ名前のスロットに保存した・消したら（ほかのボタンや、ゲームのコードから）、押せるかを直す。
        // 大文字・小文字だけが違う名前も同じファイルのことがある（Windows・macOS）。確かめ直すだけなので、別のファイルでも害は無い
        private void OnSlotChanged(NodeGraphAsset graph, string slot)
        {
            if (string.Equals(slot, _slot, StringComparison.OrdinalIgnoreCase))
            {
                RefreshInteractable();
            }
        }

        // グラフが空なら、Runner が始まって初めてどのグラフのセーブか分かる（シーンを読み込むと、Runner が始まるより先にボタンが有効になる）
        private void OnRunnerStarted(GraphRunner runner)
        {
            if (_graph == null)
            {
                RefreshInteractable();
            }
        }

        /// <summary>
        /// <paramref name="clickEvent"/>（ボタンのクリック）に <see cref="Send"/> を繋ぐ。<see cref="SendOnClick"/> でなければ繋がない。
        /// インスペクタの OnClick で既にこのコンポーネントの Send を呼んでいれば繋がない（1 回のクリックで 2 回送らないように）。
        /// </summary>
        internal void HookClick(UnityEvent clickEvent)
        {
            UnhookClick();
            if (_sendOnClick && clickEvent != null && !CallsSend(clickEvent, this))
            {
                _clickEvent = clickEvent;
                _clickEvent.AddListener(Send);
            }
        }

        /// <summary>クリックに自動で繋いでいるか。</summary>
        internal bool IsClickHooked => _clickEvent != null;

        /// <summary>
        /// <paramref name="clickEvent"/> の永続的な呼び出し（インスペクタの OnClick）に、実行時に動く <paramref name="button"/> の送信があるか。
        /// Off にした呼び出しは数えない（数えると、自動でも繋がず、OnClick からも送らず、何も送らなくなる）。
        /// </summary>
        internal static bool CallsSend(UnityEventBase clickEvent, GraphEventButton button)
        {
            if (clickEvent == null || button == null)
            {
                return false;
            }

            for (var i = 0; i < clickEvent.GetPersistentEventCount(); i++)
            {
                if (clickEvent.GetPersistentListenerState(i) == UnityEventCallState.Off)
                {
                    continue;
                }

                var method = clickEvent.GetPersistentMethodName(i);
                if (clickEvent.GetPersistentTarget(i) == button && (method == nameof(Send) || method == nameof(TrySend)))
                {
                    return true;
                }
            }

            return false;
        }

        internal void UnhookClick()
        {
            _clickEvent?.RemoveListener(Send);
            _clickEvent = null;
        }
    }
}
