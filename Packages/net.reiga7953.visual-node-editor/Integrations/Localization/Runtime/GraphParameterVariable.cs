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
    /// 値は、そのグラフを実行中の Runner のもの。実行中のものが無ければ、最後に値を読んだ Runner（止まったときの値のまま）、それも無ければグラフの初期値。
    /// 文字列が使っている間（<see cref="ValueChanged"/> に登録がある間）だけ Runner を追い、値が変わる・値を読む Runner が替わると <see cref="ValueChanged"/> を出す
    /// （Localized String が文字列を作り直す）。
    /// </para>
    /// </summary>
    [Serializable]
    [DisplayName("Graph Parameter")]
    public sealed class GraphParameterVariable : IVariableValueChanged
    {
        [Tooltip("The graph whose running Graph Runner gives the value (after it stops, its last value; before any runs, the start value). Empty: any running graph.")]
        [SerializeField] private NodeGraphAsset _graph;

        [Tooltip("The name of the parameter to show.")]
        [SerializeField, GraphParameterName] private string _parameter = string.Empty;

        [NonSerialized] private Action<IVariable> _valueChanged;
        [NonSerialized] private GraphRunnerFollower _follower;
        [NonSerialized] private GraphRunner _source;
        [NonSerialized] private bool _quiet;

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

        /// <summary>値が変わったとき（パラメータが変わった・値を読む Runner が替わった・<see cref="Graph"/> か <see cref="Parameter"/> を替えた）。</summary>
        public event Action<IVariable> ValueChanged
        {
            add
            {
                _valueChanged += value;
                if (_valueChanged != null && !IsListening)
                {
                    StartListening();
                }
            }
            remove
            {
                _valueChanged -= value;
                if (_valueChanged == null && IsListening)
                {
                    Follower.Stop();
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

                UseGraph(value);
                if (IsListening)
                {
                    _valueChanged?.Invoke(this);
                }
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
                if (IsListening)
                {
                    _valueChanged?.Invoke(this);
                }
            }
        }

        /// <summary>
        /// 今の値（bool / int / float / string）: そのグラフを実行中の Runner の値。実行中のものが無ければ、最後に値を読んだ Runner の値
        /// （止まった Runner も、止まったときの値を持っている）。どちらも無ければグラフの初期値。どれにも無ければ null（Smart Strings では空の文字）。
        /// </summary>
        public object Value
        {
            get
            {
                var source = FindSource();
                if (source != null)
                {
                    return source.TryGetParameter(Parameter, out var value) ? value : null;
                }

                return _graph != null ? _graph.FindParameterByName(Parameter)?.DefaultValue : null;
            }
        }

        /// <summary>Runner を追っているか（<see cref="ValueChanged"/> に登録がある間）。</summary>
        internal bool IsListening => _follower != null && _follower.IsFollowing;

        // SerializeReference で作られたときはコンストラクタを通らないことがあるので、使うときに作る
        private GraphRunnerFollower Follower => _follower ??= new GraphRunnerFollower(OnRunnerChanged, null, OnParameterChanged);

        /// <inheritdoc/>
        public object GetSourceValue(ISelectorInfo selector) => Value;

        /// <summary>
        /// グラフを替える。知らせない（<see cref="GraphParameterGroup"/> が、文字列を作っている最中に、インスペクタで替わったグラフに合わせるとき）。
        /// </summary>
        internal void UseGraph(NodeGraphAsset graph)
        {
            _graph = graph;
            _source = null;
            if (!IsListening)
            {
                return;
            }

            _quiet = true;
            try
            {
                Follower.Graph = graph;
            }
            finally
            {
                _quiet = false;
            }
        }

        // 聞き始めるとき（Localized String が登録している最中）は知らせない。値はこの後に読まれる
        private void StartListening()
        {
            _quiet = true;
            try
            {
                Follower.Graph = _graph;
                Follower.Start();
            }
            finally
            {
                _quiet = false;
            }
        }

        // 値を読む Runner: 実行中のものがあればそれ（覚えておく）、無ければ最後に読んだもの
        private GraphRunner FindSource()
        {
            var running = GraphRunner.FindRunning(_graph);
            if (running != null)
            {
                _source = running;
            }

            return _source;
        }

        // Runner が始まった・止まった: 値を読む Runner が替わったときだけ知らせる（止まってほかに無ければ、最後の値のまま）
        private void OnRunnerChanged()
        {
            var before = _source;
            if (FindSource() != before && !_quiet)
            {
                _valueChanged?.Invoke(this);
            }
        }

        private void OnParameterChanged(string parameter)
        {
            if (parameter == Parameter)
            {
                _valueChanged?.Invoke(this);
            }
        }
    }
}
