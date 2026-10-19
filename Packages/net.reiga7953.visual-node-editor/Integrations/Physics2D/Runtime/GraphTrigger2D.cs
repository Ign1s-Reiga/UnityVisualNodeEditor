using System.Collections.Generic;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Physics2D
{
    /// <summary>
    /// タグの合う物（既定 <c>Player</c>）が同じ GameObject の Collider 2D に触れたら、パラメータを変え、イベントを Raise するコンポーネント
    /// （ゴール → <c>StageClear</c>、トゲ → <c>Lives -= 1</c> と <c>Died</c>、コイン → <c>Coins += 1</c>、チェックポイント → <c>SpawnPoint = "Checkpoint 2"</c>）。
    /// Is Trigger のコライダー（通り抜ける）でも、ふつうのコライダー（ぶつかる）でも動く。同じ物（Rigidbody 2D）が 2 つ以上のコライダーで触れても 1 回だけ起こす。
    /// 変更とイベントの決まりは <see cref="GraphReactions"/>。
    /// </summary>
    [AddComponentMenu("Visual Node Editor/Graph Trigger 2D")]
    public sealed class GraphTrigger2D : MonoBehaviour
    {
        [Tooltip("The graph whose running Graph Runner gets the changes and the event. Empty: any running graph (then parameters cannot be changed).")]
        [SerializeField] private NodeGraphAsset _graph;

        [Tooltip("Only things with this tag set it off (the tag of the touching collider's GameObject or of its Rigidbody 2D). Empty: anything.")]
        [SerializeField] private string _tag = "Player";

        [Tooltip("Enter: when it starts touching. Exit: when it stops touching.")]
        [SerializeField] private GraphTriggerMoment _when = GraphTriggerMoment.Enter;

        [Tooltip("Parameters to change first, from top to bottom (Coins += 1, SpawnPoint = \"Checkpoint 2\"). Needs the graph.")]
        [SerializeField] private List<ParameterChange> _changes = new List<ParameterChange>();

        [Tooltip("Raised after the changes (StageClear, Died). Empty: only change the parameters.")]
        [SerializeField, EventName] private string _eventName = string.Empty;

        [Tooltip("Set it off only once (until the scene is loaded again).")]
        [SerializeField] private bool _once;

        [Tooltip("Turn this GameObject off after setting it off (coins, items picked up once).")]
        [SerializeField] private bool _deactivate;

        private bool _fired;

        // 触れている物ごとの、触れているコライダーの組の数
        private readonly Dictionary<Object, int> _contacts = new Dictionary<Object, int>();

        /// <summary>送り先のグラフ。null なら実行中のどれか（パラメータは変えられない）。</summary>
        public NodeGraphAsset Graph
        {
            get => _graph;
            set => _graph = value;
        }

        /// <summary>反応するタグ（空なら何でも）。</summary>
        public string TargetTag
        {
            get => _tag;
            set => _tag = value ?? string.Empty;
        }

        /// <summary>いつ起こすか。</summary>
        public GraphTriggerMoment When
        {
            get => _when;
            set => _when = value;
        }

        /// <summary>先に行うパラメータの変更（上から順）。</summary>
        public List<ParameterChange> Changes => _changes;

        /// <summary>変更の後で Raise するイベント名（空なら起こさない）。</summary>
        public string EventName
        {
            get => _eventName;
            set => _eventName = value ?? string.Empty;
        }

        /// <summary>1 度だけ起こすか。</summary>
        public bool Once
        {
            get => _once;
            set => _once = value;
        }

        /// <summary>起こした後、この GameObject を無効にするか。</summary>
        public bool Deactivate
        {
            get => _deactivate;
            set => _deactivate = value;
        }

        /// <summary>
        /// 今すぐ起こす（触れたときと同じ。タグは見ない）。Once で起こし済み・Runner が無ければ何もせず false
        /// （Runner が無いときは、Once も Deactivate も使わない）。
        /// </summary>
        public bool Fire()
        {
            if (_once && _fired)
            {
                return false;
            }

            if (!GraphReactions.Run(_graph, _eventName, _changes, this))
            {
                return false;
            }

            _fired = true;
            if (_deactivate)
            {
                gameObject.SetActive(false);
            }

            return true;
        }

        /// <summary><paramref name="other"/> が触れた（または離れた）ときの処理。タグが合えば起こす（テストからも呼ぶ）。</summary>
        internal bool Touch(Collider2D other)
        {
            // 物理の通知は、無効にしたコンポーネントにも届くので、ここで止める
            if (other == null || !enabled || !gameObject.activeInHierarchy)
            {
                return false;
            }

            var body = other.attachedRigidbody != null ? other.attachedRigidbody.gameObject : null;
            return Matches(other.gameObject, body) && Fire();
        }

        /// <summary>タグが合うか: 空なら何でも。触れた GameObject か、その Rigidbody 2D の GameObject のタグ（文字列で比べるので、未定義のタグでもエラーにならない）。</summary>
        internal bool Matches(GameObject touched, GameObject body) =>
            string.IsNullOrEmpty(_tag) || HasTag(touched) || HasTag(body);

        private bool HasTag(GameObject target) => target != null && target.tag == _tag;

        /// <summary>
        /// コライダーの組が触れ始めた（テストからも呼ぶ）。触れている物（Rigidbody 2D、無ければ GameObject）ごとに組を数え、
        /// その物の 1 つ目の組で、When が Enter なら起こす（体と足のコライダーを持つプレイヤーでも 1 回）。起こしたら true。
        /// </summary>
        internal bool Began(Collider2D other)
        {
            if (other == null || !enabled || !gameObject.activeInHierarchy)
            {
                return false;
            }

            var key = KeyOf(other);
            _contacts.TryGetValue(key, out var count);
            _contacts[key] = count + 1;
            return count == 0 && _when == GraphTriggerMoment.Enter && Touch(other);
        }

        /// <summary>
        /// コライダーの組が離れた（テストからも呼ぶ）。その物の最後の組で、When が Exit なら起こす。触れ始めを数えていない物は無視する。起こしたら true。
        /// </summary>
        internal bool Ended(Collider2D other)
        {
            if (other == null)
            {
                return false;
            }

            var key = KeyOf(other);
            if (!_contacts.TryGetValue(key, out var count))
            {
                return false;
            }

            if (count > 1)
            {
                _contacts[key] = count - 1;
                return false;
            }

            _contacts.Remove(key);
            return _when == GraphTriggerMoment.Exit && Touch(other);
        }

        // 触れている物: コライダーの Rigidbody 2D（同じ体の別のコライダーをまとめる）、無ければコライダーの GameObject
        private static Object KeyOf(Collider2D other) => other.attachedRigidbody != null ? other.attachedRigidbody : (Object)other.gameObject;

        // 無効の間に届く Exit は数えないので、数をそろえ直せるよう捨てる
        private void OnDisable() => _contacts.Clear();

        private void OnTriggerEnter2D(Collider2D other) => Began(other);

        private void OnTriggerExit2D(Collider2D other) => Ended(other);

        private void OnCollisionEnter2D(Collision2D collision) => Began(collision.collider);

        private void OnCollisionExit2D(Collision2D collision) => Ended(collision.collider);
    }
}
