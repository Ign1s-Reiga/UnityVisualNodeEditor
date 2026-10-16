using System.Collections.Generic;
using UnityEngine;

namespace Reiga.VisualNodeEditor
{
    /// <summary>
    /// この GameObject が壊された・無効になったら、パラメータを変え、イベントを Raise するコンポーネント（敵を倒したら <c>Enemies -= 1</c>、ボスを倒したら <c>BossDefeated</c>）。
    /// 無効になったとき（<c>OnDisable</c>。<c>Destroy</c> されたときもその前に呼ばれる。このコンポーネントを切ったときも）に起こす。
    /// シーンを閉じるとき（別のシーンの読み込み・Play を終える・アプリを終える）の無効化では起こさない。プールして使い回す物は、無効になるたびに起こす。
    /// 変更とイベントの決まりは <see cref="GraphReactions"/>。
    /// </summary>
    [AddComponentMenu("Visual Node Editor/Graph Destroy Trigger")]
    public sealed class GraphDestroyTrigger : MonoBehaviour
    {
        [Tooltip("The graph whose running Graph Runner gets the changes and the event. Empty: any running graph (then parameters cannot be changed).")]
        [SerializeField] private NodeGraphAsset _graph;

        [Tooltip("Parameters to change first, from top to bottom (Enemies -= 1). Needs the graph.")]
        [SerializeField] private List<ParameterChange> _changes = new List<ParameterChange>();

        [Tooltip("Raised after the changes (BossDefeated). Empty: only change the parameters.")]
        [SerializeField, EventName] private string _eventName = string.Empty;

        private static bool _quitting;

        /// <summary>送り先のグラフ。null なら実行中のどれか（パラメータは変えられない）。</summary>
        public NodeGraphAsset Graph
        {
            get => _graph;
            set => _graph = value;
        }

        /// <summary>先に行うパラメータの変更（上から順）。</summary>
        public List<ParameterChange> Changes => _changes;

        /// <summary>変更の後で Raise するイベント名（空なら起こさない）。</summary>
        public string EventName
        {
            get => _eventName;
            set => _eventName = value ?? string.Empty;
        }

        /// <summary>今すぐ起こす（無効になったときと同じ）。Runner が無ければ何もせず false。</summary>
        public bool Fire() => GraphReactions.Run(_graph, _eventName, _changes, this);

        /// <summary>無効になったときに起こすか: シーンを閉じている（<paramref name="sceneLoaded"/> が false）・アプリを終えているときは起こさない。</summary>
        internal static bool ShouldFire(bool sceneLoaded, bool quitting) => sceneLoaded && !quitting;

        private void OnDisable()
        {
            if (ShouldFire(gameObject.scene.isLoaded, _quitting))
            {
                Fire();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ListenForQuit()
        {
            _quitting = false;
            Application.quitting -= OnQuitting;
            Application.quitting += OnQuitting;
        }

        private static void OnQuitting() => _quitting = true;
    }
}
