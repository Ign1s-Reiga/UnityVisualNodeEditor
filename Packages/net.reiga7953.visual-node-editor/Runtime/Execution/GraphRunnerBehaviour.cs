using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Reiga.VisualNodeEditor
{
    /// <summary>
    /// シーンに置いてグラフを実行するコンポーネント。ルートの GameObject に付けること（<c>DontDestroyOnLoad</c> のため）。
    /// <see cref="Raise"/> / <see cref="Advance"/> は public なので、UI の Button の OnClick などから直接呼べる。
    /// </summary>
    [AddComponentMenu("Visual Node Editor/Graph Runner")]
    [DisallowMultipleComponent]
    public sealed class GraphRunnerBehaviour : MonoBehaviour
    {
        // 同じグラフを動かす永続インスタンス。最初のシーンに戻ったときに Runner が増えないようにする
        private static readonly Dictionary<NodeGraphAsset, GraphRunnerBehaviour> _persistent = new();

        [SerializeField] private NodeGraphAsset _graph;

        [Tooltip("Start() でグラフの実行を始める。")]
        [SerializeField] private bool _startAutomatically = true;

        [Tooltip("シーンを切り替えてもこのオブジェクトを残す。")]
        [SerializeField] private bool _dontDestroyOnLoad = true;

        [Tooltip("Scene ノードに入ったら、そのシーンを読み込む。")]
        [SerializeField] private bool _loadScenes = true;

        [Tooltip("ノードに入るたびに、そのノードのタイトルを引数に呼ぶ。")]
        [SerializeField] private UnityEvent<string> _onNodeEntered = new UnityEvent<string>();

        [Tooltip("イベント名ごとに呼ぶ処理。Event ノードを通過したとき、または出力の無い Event が Raise されたときに呼ばれる。")]
        [SerializeField] private List<GraphEventBinding> _eventBindings = new List<GraphEventBinding>();

        private bool _isDuplicate;

        /// <summary>実行しているグラフ。</summary>
        public NodeGraphAsset Graph => _graph;

        /// <summary>実行器。<see cref="StartGraph"/> 前は null。</summary>
        public GraphRunner Runner { get; private set; }

        /// <summary>ノードに入るたびに呼ばれる（引数はノードのタイトル）。</summary>
        public UnityEvent<string> OnNodeEntered => _onNodeEntered;

        /// <summary>イベント名ごとの処理。</summary>
        public List<GraphEventBinding> EventBindings => _eventBindings;

        /// <summary>グラフの実行を始める。すでに実行中なら何もしない。</summary>
        public void StartGraph()
        {
            if (_graph == null || (Runner != null && Runner.IsRunning))
            {
                return;
            }

            CreateRunner();
            Runner.Start();
        }

        /// <summary>
        /// 今の状態をスロットに保存する（<see cref="GraphSaveSlots.Save"/>）。UnityEvent から呼べる。実行中でなければ警告を出して何もしない。
        /// </summary>
        public void SaveSlot(string slot)
        {
            if (Runner == null || !Runner.IsRunning)
            {
                Debug.LogWarning($"[VisualNodeEditor] '{name}' is not running its graph, so slot '{slot}' was not saved.", this);
                return;
            }

            GraphSaveSlots.Save(Runner, slot);
        }

        /// <summary>スロットから再開する（UnityEvent から呼べる）。止まっていても始められる。</summary>
        public void LoadSlot(string slot) => TryLoadSlot(slot);

        /// <summary>スロットから再開する。止まっていても始められる。スロットが無い・保存したノードがグラフに無ければ false。</summary>
        public bool TryLoadSlot(string slot)
        {
            if (_graph == null)
            {
                return false;
            }

            if (Runner == null)
            {
                CreateRunner();
            }

            return GraphSaveSlots.Load(Runner, slot);
        }

        private void CreateRunner()
        {
            Runner = new GraphRunner(_graph, _loadScenes ? new SceneManagerSceneLoader() : null) { Host = this };

            // 開始時の Entry / 最初のノードの通知にも間に合うよう、Start より前に購読する
            Runner.NodeEntered += node => _onNodeEntered.Invoke(node.Title);
            Runner.EventTriggered += eventName => GraphEventBinding.Dispatch(_eventBindings, eventName);
        }

        /// <summary>グラフの実行を止める。</summary>
        public void StopGraph() => Runner?.Stop();

        /// <summary>現在のノードから、名前が一致するイベントで遷移する。</summary>
        public void Raise(string eventName) => Runner?.Raise(eventName);

        /// <summary>現在のノードから、イベントを介さずに次へ進む。</summary>
        public void Advance() => Runner?.Advance();

        private void Awake()
        {
            if (!_dontDestroyOnLoad || _graph == null)
            {
                return;
            }

            if (_persistent.TryGetValue(_graph, out var existing) && existing != null && existing != this)
            {
                _isDuplicate = true;
                Destroy(gameObject);
                return;
            }

            _persistent[_graph] = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            if (!_isDuplicate && _startAutomatically)
            {
                StartGraph();
            }
        }

        // 現在のノードの振る舞い（NodeBehaviour）の OnUpdate / OnFixedUpdate を呼ぶ
        private void Update() => Runner?.Update(Time.deltaTime);

        private void FixedUpdate() => Runner?.FixedUpdate(Time.fixedDeltaTime);

        private void OnDestroy()
        {
            StopGraph();
            if (_graph != null && _persistent.TryGetValue(_graph, out var registered) && registered == this)
            {
                _persistent.Remove(_graph);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetPersistent() => _persistent.Clear();
    }
}
