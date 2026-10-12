using System.Collections.Generic;
using UnityEngine;

namespace Reiga.VisualNodeEditor
{
    /// <summary>
    /// グラフが指定したノードにいる間だけ、指定した GameObject（<see cref="Targets"/>）を表示するコンポーネント。
    /// コンテナを指せば、その中のどこかにいる間。ポーズ・ゲームオーバーのパネルなどに使う。
    /// <para>
    /// Runner は <see cref="GraphRunner.FindRunning"/> で探す（参照は要らない）。確かめるのは、有効になったとき・Runner がノードに入ったとき・
    /// Runner が始まった・止まったとき。Runner が無い・止まっているときは隠す。
    /// 自分の GameObject とその親は隠せない（自分も無効になり、二度と表示できなくなる）ので飛ばす。
    /// </para>
    /// </summary>
    [AddComponentMenu("Visual Node Editor/Graph Node Activator")]
    public sealed class GraphNodeActivator : MonoBehaviour
    {
        [Tooltip("このグラフを実行中の Graph Runner を見る。")]
        [SerializeField] private NodeGraphAsset _graph;

        [Tooltip("グラフがこのノードにいる間（コンテナなら、その中のどこかにいる間）だけ表示する。")]
        [SerializeField] private string _nodeId = string.Empty;

        [Tooltip("表示・非表示を切り替える GameObject（自分とその親は選べない）。")]
        [SerializeField] private List<GameObject> _targets = new List<GameObject>();

        private GraphRunner _runner;
        private bool _listening;

        /// <summary>見るグラフ。</summary>
        public NodeGraphAsset Graph
        {
            get => _graph;
            set
            {
                _graph = value;
                RefreshIfListening();
            }
        }

        /// <summary>このノード（の ID）にいる間だけ表示する。</summary>
        public string NodeId
        {
            get => _nodeId;
            set
            {
                _nodeId = value ?? string.Empty;
                RefreshIfListening();
            }
        }

        /// <summary>表示・非表示を切り替える GameObject。変えたら <see cref="Refresh"/> を呼ぶ。</summary>
        public List<GameObject> Targets => _targets;

        /// <summary>最後に確かめたとき、グラフが指定したノードにいたか。</summary>
        public bool IsInNode { get; private set; }

        /// <summary>今のグラフの位置で、<see cref="Targets"/> を表示する・隠す。</summary>
        public void Refresh()
        {
            var runner = _graph != null ? GraphRunner.FindRunning(_graph) : null;
            // 有効な間だけ Runner のノードの出入りを聞く（無効な間に呼ばれても、聞き始めない）
            if (_listening)
            {
                SetRunner(runner);
            }

            IsInNode = IsIn(runner, _nodeId);
            foreach (var target in _targets)
            {
                if (CanTarget(gameObject, target) && target.activeSelf != IsInNode)
                {
                    target.SetActive(IsInNode);
                }
            }
        }

        /// <summary>
        /// <paramref name="runner"/> が <paramref name="nodeId"/> のノードにいるか。コンテナなら、その中（何段下でも）にいるか。
        /// Runner が無い・止まっているときは false。
        /// </summary>
        public static bool IsIn(GraphRunner runner, string nodeId)
        {
            var current = runner != null && runner.IsRunning ? runner.Current : null;
            if (current == null || string.IsNullOrEmpty(nodeId))
            {
                return false;
            }

            if (current.Id == nodeId)
            {
                return true;
            }

            foreach (var container in runner.ContainerPath)
            {
                if (container.Id == nodeId)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// <paramref name="target"/> を表示・非表示にできるか。null・<paramref name="owner"/> 自身・その親はできない
        /// （隠すとコンポーネントも無効になり、二度と表示できない）。
        /// </summary>
        public static bool CanTarget(GameObject owner, GameObject target) =>
            target != null && owner != null && target != owner && !owner.transform.IsChildOf(target.transform);

        private void OnEnable() => Enable();

        private void OnDisable() => Disable();

        /// <summary>有効になったとき: Runner の開始・停止とノードの出入りを聞き、表示を直す（テストからも呼ぶ）。</summary>
        internal void Enable()
        {
            _listening = true;
            GraphRunner.Started += OnRunnerChanged;
            GraphRunner.Stopped += OnRunnerChanged;
            Refresh();
        }

        /// <summary>無効になったとき: <see cref="Enable"/> で聞き始めたものを外す（<see cref="Targets"/> には触らない）。</summary>
        internal void Disable()
        {
            _listening = false;
            GraphRunner.Started -= OnRunnerChanged;
            GraphRunner.Stopped -= OnRunnerChanged;
            SetRunner(null);
        }

        private void OnRunnerChanged(GraphRunner runner)
        {
            if (_graph != null && runner.Graph == _graph)
            {
                Refresh();
            }
        }

        private void OnNodeEntered(NodeData node) => Refresh();

        private void RefreshIfListening()
        {
            if (_listening)
            {
                Refresh();
            }
        }

        // ノードの出入りを聞く Runner を替える（止まった Runner からは外れる）
        private void SetRunner(GraphRunner runner)
        {
            if (runner == _runner)
            {
                return;
            }

            if (_runner != null)
            {
                _runner.NodeEntered -= OnNodeEntered;
            }

            _runner = runner;
            if (_runner != null)
            {
                _runner.NodeEntered += OnNodeEntered;
            }
        }
    }
}
