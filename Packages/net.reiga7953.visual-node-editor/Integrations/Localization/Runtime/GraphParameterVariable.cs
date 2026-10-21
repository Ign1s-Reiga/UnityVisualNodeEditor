using System;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.SmartFormat.Core.Extensions;
using UnityEngine.Localization.SmartFormat.PersistentVariables;

namespace Reiga.VisualNodeEditor.Localization
{
    /// <summary>
    /// グラフのパラメータ（Blackboard）1 つを、Unity Localization の Smart Strings で読む変数。
    /// Localized String の Local Variables に <c>score</c> などの名前で足し、文字列に <c>{score}</c> と書く。
    /// グラフのパラメータをまとめて読むなら <see cref="GraphParameterGroup"/>（<c>{graph.Score}</c>）。
    /// <para>
    /// 値は、そのグラフを実行中の Runner のもの（無ければグラフの初期値）。文字列が使っている間（<see cref="ValueChanged"/> に登録がある間）だけ Runner を聞き、
    /// 値が変わる・Runner が始まる / 止まると <see cref="ValueChanged"/> を出す（Localized String が文字列を作り直す）。
    /// </para>
    /// </summary>
    [Serializable]
    [DisplayName("Graph Parameter")]
    public sealed class GraphParameterVariable : IVariableValueChanged
    {
        [Tooltip("The graph whose running Graph Runner gives the value (the start value while none runs). Empty: any running graph.")]
        [SerializeField] private NodeGraphAsset _graph;

        [Tooltip("The name of the parameter to show.")]
        [SerializeField, GraphParameterName] private string _parameter = string.Empty;

        [NonSerialized] private Action<IVariable> _valueChanged;
        [NonSerialized] private GraphRunner _runner;
        [NonSerialized] private bool _listening;

        /// <summary>空の変数（Local Variables の追加のメニューから作るとき）。</summary>
        public GraphParameterVariable()
        {
        }

        /// <summary><paramref name="graph"/> のパラメータ <paramref name="parameter"/> を読む変数。</summary>
        /// <param name="graph">値を読むグラフ。null なら実行中のどれか 1 つ。</param>
        /// <param name="parameter">パラメータの名前。</param>
        public GraphParameterVariable(NodeGraphAsset graph, string parameter)
        {
            _graph = graph;
            _parameter = parameter ?? string.Empty;
        }

        /// <summary>値が変わったとき（パラメータが変わった・Runner が始まった / 止まった・<see cref="Graph"/> か <see cref="Parameter"/> を替えた）。</summary>
        public event Action<IVariable> ValueChanged
        {
            add
            {
                _valueChanged += value;
                if (_valueChanged != null && !_listening)
                {
                    StartListening();
                }
            }
            remove
            {
                _valueChanged -= value;
                if (_valueChanged == null && _listening)
                {
                    StopListening();
                }
            }
        }

        /// <summary>値を読むグラフ。null なら実行中のどれか 1 つ。替えると、使っている文字列を作り直させる。</summary>
        public NodeGraphAsset Graph
        {
            get => _graph;
            set
            {
                if (_graph == value)
                {
                    return;
                }

                _graph = value;
                Refresh();
            }
        }

        /// <summary>パラメータの名前。替えると、使っている文字列を作り直させる。</summary>
        public string Parameter
        {
            get => _parameter ?? string.Empty;
            set
            {
                value ??= string.Empty;
                if (Parameter == value)
                {
                    return;
                }

                _parameter = value;
                Refresh();
            }
        }

        /// <summary>
        /// 今の値（bool / int / float / string）: そのグラフを実行中の Runner の値。Runner が無ければグラフの初期値。
        /// どちらにも無ければ null（Smart Strings では空の文字）。
        /// </summary>
        public object Value
        {
            get
            {
                var runner = GraphRunner.FindRunning(_graph);
                if (runner != null)
                {
                    return runner.TryGetParameter(Parameter, out var value) ? value : null;
                }

                return _graph != null ? _graph.FindParameterByName(Parameter)?.DefaultValue : null;
            }
        }

        /// <summary>Runner を聞いているか（<see cref="ValueChanged"/> に登録がある間）。</summary>
        internal bool IsListening => _listening;

        /// <inheritdoc/>
        public object GetSourceValue(ISelectorInfo selector) => Value;

        /// <summary>
        /// グラフを替える。知らせない（<see cref="GraphParameterGroup"/> が、文字列を作っている最中に、インスペクタで替わったグラフに合わせるとき）。
        /// </summary>
        internal void UseGraph(NodeGraphAsset graph)
        {
            _graph = graph;
            if (_listening)
            {
                Follow(GraphRunner.FindRunning(_graph));
            }
        }

        // 聞く Runner を選び直し、文字列を作り直させる
        private void Refresh()
        {
            if (!_listening)
            {
                return;
            }

            Follow(GraphRunner.FindRunning(_graph));
            _valueChanged?.Invoke(this);
        }

        private void StartListening()
        {
            _listening = true;
            GraphRunner.Started += OnRunnerStarted;
            GraphRunner.Stopped += OnRunnerStopped;
            Follow(GraphRunner.FindRunning(_graph));
        }

        private void StopListening()
        {
            _listening = false;
            GraphRunner.Started -= OnRunnerStarted;
            GraphRunner.Stopped -= OnRunnerStopped;
            Follow(null);
        }

        private void OnRunnerStarted(GraphRunner runner)
        {
            if (_graph == null || runner.Graph == _graph)
            {
                Refresh();
            }
        }

        // 値を読んでいる Runner が止まったら、ほかの実行中の Runner（無ければグラフの初期値）に替わる
        private void OnRunnerStopped(GraphRunner runner)
        {
            if (runner == _runner)
            {
                Refresh();
            }
        }

        private void OnParameterChanged(string parameter)
        {
            if (parameter == Parameter)
            {
                _valueChanged?.Invoke(this);
            }
        }

        // パラメータの変化を聞く Runner を替える（止まった Runner からは外れる）
        private void Follow(GraphRunner runner)
        {
            if (runner == _runner)
            {
                return;
            }

            if (_runner != null)
            {
                _runner.ParameterChanged -= OnParameterChanged;
            }

            _runner = runner;
            if (_runner != null)
            {
                _runner.ParameterChanged += OnParameterChanged;
            }
        }
    }
}
