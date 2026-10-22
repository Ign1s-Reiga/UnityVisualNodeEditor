using System;

namespace Reiga.VisualNodeEditor
{
    /// <summary>
    /// グラフ（null なら実行中のどれか）を実行中の Runner を探して追い、その Runner のノードの出入り・パラメータの変化を伝える
    /// （UI Toolkit の Graph Button・Graph Parameter Label・Graph Node Panel・Graph Screen Host と、Localization 連携の Graph Parameter が使う。
    /// 知らせ方を変えるときは、Localization 連携のテストも確かめる）。
    /// Runner は最初のシーンに残り、画面が先に出ることもあるので、<see cref="GraphRunner.Started"/> / <see cref="GraphRunner.Stopped"/> で探し直す。
    /// </summary>
    internal sealed class GraphRunnerFollower
    {
        private readonly Action _runnerChanged;
        private readonly Action<NodeData> _nodeEntered;
        private readonly Action<string> _parameterChanged;
        private NodeGraphAsset _graph;

        /// <param name="runnerChanged">追う Runner が替わった（始まった・止まった・グラフを変えた）とき。</param>
        /// <param name="nodeEntered">追っている Runner がノードに入ったとき（無ければ null）。</param>
        /// <param name="parameterChanged">追っている Runner のパラメータが変わったとき（無ければ null）。</param>
        public GraphRunnerFollower(Action runnerChanged, Action<NodeData> nodeEntered = null, Action<string> parameterChanged = null)
        {
            _runnerChanged = runnerChanged;
            _nodeEntered = nodeEntered;
            _parameterChanged = parameterChanged;
        }

        /// <summary>追うグラフ。null なら実行中のどれか。</summary>
        public NodeGraphAsset Graph
        {
            get => _graph;
            set
            {
                _graph = value;
                if (IsFollowing)
                {
                    Find();
                }
            }
        }

        /// <summary>今追っている Runner。追っていない・見つからなければ null。</summary>
        public GraphRunner Runner { get; private set; }

        /// <summary><see cref="Start"/> から <see cref="Stop"/> までの間か。</summary>
        public bool IsFollowing { get; private set; }

        /// <summary>Runner の開始・停止を聞き始め、今の Runner を探す。</summary>
        public void Start()
        {
            if (IsFollowing)
            {
                return;
            }

            IsFollowing = true;
            GraphRunner.Started += OnStarted;
            GraphRunner.Stopped += OnStopped;
            Find();
        }

        /// <summary>聞くのをやめ、Runner から外れる。</summary>
        public void Stop()
        {
            IsFollowing = false;
            GraphRunner.Started -= OnStarted;
            GraphRunner.Stopped -= OnStopped;
            SetRunner(null);
        }

        private void OnStarted(GraphRunner runner)
        {
            if (_graph == null || runner.Graph == _graph)
            {
                Find();
            }
        }

        private void OnStopped(GraphRunner runner)
        {
            if (runner == Runner)
            {
                Find();
            }
        }

        // 今の Runner を探し直して知らせる
        private void Find()
        {
            SetRunner(GraphRunner.FindRunning(_graph));
            _runnerChanged?.Invoke();
        }

        private void SetRunner(GraphRunner runner)
        {
            if (runner == Runner)
            {
                return;
            }

            if (Runner != null)
            {
                Runner.NodeEntered -= OnNodeEntered;
                Runner.ParameterChanged -= OnParameterChanged;
            }

            Runner = runner;
            if (Runner != null)
            {
                Runner.NodeEntered += OnNodeEntered;
                Runner.ParameterChanged += OnParameterChanged;
            }
        }

        private void OnNodeEntered(NodeData node) => _nodeEntered?.Invoke(node);

        private void OnParameterChanged(string parameter) => _parameterChanged?.Invoke(parameter);
    }
}
