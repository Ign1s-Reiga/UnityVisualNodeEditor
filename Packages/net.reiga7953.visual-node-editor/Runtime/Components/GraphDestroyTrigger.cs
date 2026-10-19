using System.Collections.Generic;
using UnityEngine;

namespace Reiga.VisualNodeEditor
{
    /// <summary>
    /// この GameObject が壊された・無効になったら、パラメータを変え、イベントを Raise するコンポーネント（敵を倒したら <c>Enemies -= 1</c>、ボスを倒したら <c>BossDefeated</c>）。
    /// この GameObject 自身が無効になったとき（<c>SetActive(false)</c>）と、壊されたときに起こす（敵の GameObject そのものに付ける）。
    /// 親が無効になっただけ（区画ごと隠した）・親ごと壊された（部屋を片付けた）・隠してから壊した・このコンポーネントを切っただけでは起こさない
    /// （数えたくない物は、このコンポーネントを切っておく）。
    /// 1 度起こしたら、また有効になるまで起こさない。プールして使い回す物は、有効に戻すたびにまた起こせる。
    /// シーンを閉じるとき（別のシーンの読み込み・Play を終える・アプリを終える）は起こさない。変更とイベントの決まりは <see cref="GraphReactions"/>。
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

        // 無くなって、もう起こしたか（また有効になるまで、2 度目は起こさない）
        private bool _gone;

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

        /// <summary>今すぐ起こす（無くなったときと同じ。1 度だけの決まりは見ない）。Runner が無ければ何もせず false。</summary>
        public bool Fire() => GraphReactions.Run(_graph, _eventName, _changes, this);

        /// <summary>無効になったときに起こすか: シーンを閉じている（<paramref name="sceneLoaded"/> が false）・アプリを終えているときは起こさない。</summary>
        internal static bool ShouldFire(bool sceneLoaded, bool quitting) => sceneLoaded && !quitting;

        /// <summary>
        /// <c>OnDisable</c> のときの状態から、倒した（無くなった）と数えるか:
        /// 自分が無効になった・壊された（<paramref name="activeSelf"/> が false。Unity は壊すとき、壊す GameObject を無効にしてから <c>OnDisable</c> を呼ぶ）、
        /// またはこのコンポーネントだけを外した（どちらも有効のまま）なら数える。
        /// 親が無効になった・親ごと壊された（<paramref name="activeInHierarchy"/> だけが false）・このコンポーネントを切った（<paramref name="componentEnabled"/> が false）なら数えない。
        /// </summary>
        internal static bool CountsAsGone(bool componentEnabled, bool activeSelf, bool activeInHierarchy) =>
            componentEnabled && (!activeSelf || activeInHierarchy);

        /// <summary>
        /// 無効になったときの処理（テストからも呼ぶ）。数えるとき（<see cref="CountsAsGone"/>）だけ、また有効になるまでに 1 度、起こす。
        /// シーンを閉じている・アプリを終えているときは起こさない。起こしたら true。
        /// </summary>
        internal bool HandleDisabled(bool componentEnabled, bool activeSelf, bool activeInHierarchy)
        {
            if (!CountsAsGone(componentEnabled, activeSelf, activeInHierarchy) || _gone || !ShouldFire(gameObject.scene.isLoaded, _quitting))
            {
                return false;
            }

            _gone = true;
            Fire();
            return true;
        }

        /// <summary>また有効になった（テストからも呼ぶ）。次に無くなったときに、また起こせる。</summary>
        internal void HandleBack() => _gone = false;

        private void OnEnable() => HandleBack();

        // 壊すときも、その前にここが呼ばれる（壊す GameObject は無効にされてから）。もう無効な物（隠してから壊した・切ってから壊した）には呼ばれないので、数えない
        private void OnDisable() => HandleDisabled(enabled, gameObject.activeSelf, gameObject.activeInHierarchy);

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
