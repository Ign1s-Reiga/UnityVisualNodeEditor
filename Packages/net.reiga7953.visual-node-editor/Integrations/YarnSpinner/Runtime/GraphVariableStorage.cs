#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Yarn;
using Yarn.Unity;

namespace Reiga.VisualNodeEditor.YarnSpinner
{
    /// <summary>
    /// Yarn の変数をグラフと共有する Variable Storage。DialogueRunner の Variable Storage に設定する。
    /// <list type="bullet">
    /// <item>Yarn の <c>$Name</c> は、Blackboard のパラメータ <c>Name</c> と同じもの（読み書きは Runner のパラメータに対して行い、条件付き遷移・Play 中の Blackboard・セーブが同じ値を見る）</item>
    /// <item>型の対応: Bool ↔ bool、Int ↔ 数（書くときは四捨五入）、Float ↔ 数、String ↔ 文字列。型の合わない書き込みはエラーを出して無視する</item>
    /// <item>Blackboard に無い変数（Yarn だけの変数や訪問回数 <c>$Yarn.Internal.*</c>）は Runner の <see cref="GraphRunner.Extras"/> に置く（セーブに含まれ、Start で消える）</item>
    /// <item>Runner が見つからない間は、自分の中の一時的な置き場を使う（警告を 1 回出す）</item>
    /// </list>
    /// </summary>
    [AddComponentMenu("Visual Node Editor/Graph Variable Storage")]
    public sealed class GraphVariableStorage : VariableStorageBehaviour
    {
        [Tooltip("このグラフを実行中の Graph Runner と変数を共有する。空なら実行中のどれか。")]
        [SerializeField] private NodeGraphAsset? _graph;

        private readonly Dictionary<string, object> _fallback = new(StringComparer.Ordinal);
        private GraphRunner? _listening;
        private bool _warnedNoRunner;

        /// <summary>共有するグラフ。null なら実行中のどれか。</summary>
        public NodeGraphAsset? Graph
        {
            get => _graph;
            set => _graph = value;
        }

        /// <summary>変数を置いている Runner（見つからなければ null）。</summary>
        public GraphRunner? Runner
        {
            get
            {
                var runner = GraphRunner.FindRunning(_graph);
                Listen(runner);
                return runner;
            }
        }

        /// <summary>Yarn の変数名（<c>$Name</c>）に対応する Blackboard のパラメータ名（<c>Name</c>）。</summary>
        public static string ToParameterName(string variableName) =>
            variableName.StartsWith("$", StringComparison.Ordinal) ? variableName.Substring(1) : variableName;

        /// <inheritdoc />
        public override bool TryGetValue<T>(string variableName, [NotNullWhen(true)] out T? result) where T : default
        {
            switch (GetVariableKind(variableName))
            {
                case VariableKind.Stored:
                    if (TryGetStored(variableName, out var stored) && TryConvert(stored, out result))
                    {
                        return true;
                    }

                    // まだ値が入っていない宣言済みの変数は、Yarn の初期値
                    if (Program == null)
                    {
                        result = default;
                        return false;
                    }

                    return Program.TryGetInitialValue(variableName, out result);
                case VariableKind.Smart:
                    if (SmartVariableEvaluator == null)
                    {
                        result = default;
                        return false;
                    }

                    return SmartVariableEvaluator.TryGetSmartVariable(variableName, out result);
                default:
                    result = default;
                    return false;
            }
        }

        /// <inheritdoc />
        public override void SetValue(string variableName, string stringValue) => Set(variableName, stringValue ?? string.Empty);

        /// <inheritdoc />
        public override void SetValue(string variableName, float floatValue) => Set(variableName, floatValue);

        /// <inheritdoc />
        public override void SetValue(string variableName, bool boolValue) => Set(variableName, boolValue);

        /// <summary>
        /// Yarn の変数をすべて消す（Extras と一時的な置き場にある <c>$</c> で始まる値）。Blackboard のパラメータはグラフのものなので消さない。
        /// </summary>
        public override void Clear()
        {
            _fallback.Clear();
            var runner = Runner;
            if (runner == null)
            {
                return;
            }

            foreach (var key in runner.Extras.Keys.Where(k => k.StartsWith("$", StringComparison.Ordinal)).ToList())
            {
                runner.Extras.Remove(key);
            }
        }

        /// <inheritdoc />
        public override bool Contains(string variableName) => TryGetStored(variableName, out _);

        /// <inheritdoc />
        public override void SetAllVariables(Dictionary<string, float> floats, Dictionary<string, string> strings, Dictionary<string, bool> bools, bool clear = true)
        {
            if (clear)
            {
                Clear();
            }

            foreach (var pair in floats)
            {
                Set(pair.Key, pair.Value);
            }

            foreach (var pair in strings)
            {
                Set(pair.Key, pair.Value ?? string.Empty);
            }

            foreach (var pair in bools)
            {
                Set(pair.Key, pair.Value);
            }
        }

        /// <summary>Blackboard のパラメータ（<c>$Name</c> として）と、Yarn だけの変数のすべて。</summary>
        public override (Dictionary<string, float> FloatVariables, Dictionary<string, string> StringVariables, Dictionary<string, bool> BoolVariables) GetAllVariables()
        {
            var floats = new Dictionary<string, float>();
            var strings = new Dictionary<string, string>();
            var bools = new Dictionary<string, bool>();
            void Add(string name, object value)
            {
                switch (value)
                {
                    case bool flag:
                        bools[name] = flag;
                        break;
                    case int number:
                        floats[name] = number;
                        break;
                    case float real:
                        floats[name] = real;
                        break;
                    case string text:
                        strings[name] = text;
                        break;
                }
            }

            foreach (var pair in _fallback)
            {
                Add(pair.Key, pair.Value);
            }

            var runner = Runner;
            if (runner != null)
            {
                foreach (var key in runner.Extras.Keys.Where(k => k.StartsWith("$", StringComparison.Ordinal)))
                {
                    if (runner.Extras.TryGetValue(key, out var value))
                    {
                        Add(key, value);
                    }
                }

                foreach (var parameter in runner.Graph.Parameters.Where(p => p != null && !string.IsNullOrEmpty(p.Name)))
                {
                    if (runner.TryGetParameter(parameter.Name, out var value))
                    {
                        Add("$" + parameter.Name, value);
                    }
                }
            }

            return (floats, strings, bools);
        }

        // 値のある変数（Blackboard → Extras → 一時的な置き場の順）
        private bool TryGetStored(string variableName, out object value)
        {
            var runner = Runner;
            if (runner != null)
            {
                if (runner.TryGetParameter(ToParameterName(variableName), out var parameter) && parameter != null)
                {
                    value = parameter;
                    return true;
                }

                if (runner.Extras.TryGetValue(variableName, out var extra) && extra != null)
                {
                    value = extra;
                    return true;
                }
            }

            if (_fallback.TryGetValue(variableName, out var kept))
            {
                value = kept;
                return true;
            }

            value = string.Empty;
            return false;
        }

        private void Set(string variableName, object value)
        {
            var runner = Runner;
            if (runner != null && runner.TryGetParameter(ToParameterName(variableName), out var current))
            {
                // 変わったことは Runner の ParameterChanged から Yarn の変更リスナーへ知らせる（OnParameterChanged）
                SetParameter(runner, variableName, current, value);
                return;
            }

            if (runner != null)
            {
                runner.Extras.Set(variableName, value);
            }
            else
            {
                if (!_warnedNoRunner)
                {
                    _warnedNoRunner = true;
                    Debug.LogWarning($"[VisualNodeEditor] No Graph Runner is running{(_graph != null ? $" '{_graph.name}'" : string.Empty)}, " +
                                     "so Yarn variables are kept only here for now (they are not shared with the graph or saved).", this);
                }

                _fallback[variableName] = value;
            }

            Notify(variableName, value);
        }

        private void SetParameter(GraphRunner runner, string variableName, object current, object value)
        {
            var name = ToParameterName(variableName);
            switch (current)
            {
                case bool _ when value is bool flag:
                    runner.SetBool(name, flag);
                    return;
                case int _ when value is float number:
                    var rounded = Mathf.RoundToInt(number);
                    if (!Mathf.Approximately(rounded, number))
                    {
                        Debug.LogWarning($"[VisualNodeEditor] Yarn set {variableName} to {number.ToString(CultureInfo.InvariantCulture)}, " +
                                         $"but the Blackboard parameter '{name}' is an Int, so it became {rounded}.", this);
                    }

                    runner.SetInt(name, rounded);
                    return;
                case float _ when value is float real:
                    runner.SetFloat(name, real);
                    return;
                case string _ when value is string text:
                    runner.SetString(name, text);
                    return;
            }

            Debug.LogError($"[VisualNodeEditor] Yarn set {variableName} to a {DescribeType(value)}, but the Blackboard parameter '{name}' is a " +
                           $"{DescribeType(current)}. The value was not changed: make the Yarn variable and the parameter the same type.", this);
        }

        private void Listen(GraphRunner? runner)
        {
            if (runner == _listening)
            {
                return;
            }

            if (_listening != null)
            {
                _listening.ParameterChanged -= OnParameterChanged;
            }

            _listening = runner;
            if (runner != null)
            {
                runner.ParameterChanged += OnParameterChanged;
                _warnedNoRunner = false;
            }
        }

        // グラフ側（ゲームのコード・条件付き遷移の値・セーブからの再開など）で変わった値も、Yarn の変更リスナーへ知らせる
        private void OnParameterChanged(string name)
        {
            if (_listening != null && _listening.TryGetParameter(name, out var value) && value != null)
            {
                Notify("$" + name, value is int number ? (float)number : value);
            }
        }

        private void Notify(string variableName, object value)
        {
            switch (value)
            {
                case bool flag:
                    NotifyVariableChanged(variableName, flag);
                    break;
                case float number:
                    NotifyVariableChanged(variableName, number);
                    break;
                case string text:
                    NotifyVariableChanged(variableName, text);
                    break;
            }
        }

        // Yarn の数は float。Int のパラメータは float にしてから渡す
        private static bool TryConvert<T>(object value, [NotNullWhen(true)] out T? result)
        {
            if (value is int number)
            {
                value = (float)number;
            }

            if (value is T typed)
            {
                result = typed;
                return true;
            }

            try
            {
                result = (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
                return result != null;
            }
            catch (Exception exception) when (exception is InvalidCastException || exception is FormatException || exception is OverflowException)
            {
                result = default;
                return false;
            }
        }

        private static string DescribeType(object value) => value switch
        {
            bool _ => "Bool",
            int _ => "Int",
            float _ => "number",
            string _ => "String",
            _ => value?.GetType().Name ?? "null",
        };

        // 有効な間は Runner の値の変化を聞く（会話が変数を読む前に、グラフ側で変わった値も Yarn の変更リスナーへ届くように）
        private void OnEnable()
        {
            GraphRunner.Started += OnRunnerStarted;
            Listen(GraphRunner.FindRunning(_graph));
        }

        private void OnDisable()
        {
            GraphRunner.Started -= OnRunnerStarted;
            Listen(null);
        }

        private void OnRunnerStarted(GraphRunner runner)
        {
            if (_listening == null && (_graph == null || runner.Graph == _graph))
            {
                Listen(runner);
            }
        }

        private void OnDestroy() => Listen(null);
    }
}
