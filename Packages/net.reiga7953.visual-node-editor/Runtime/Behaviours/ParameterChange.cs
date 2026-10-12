using System;
using System.Globalization;
using UnityEngine;

namespace Reiga.VisualNodeEditor
{
    /// <summary>
    /// パラメータ（Blackboard）を 1 つ変える指定（<see cref="SetParameterBehaviour"/> が使う）。
    /// パラメータは ID で指す（改名しても壊れない。条件と同じ）。値は型ごとの欄に持つ。
    /// </summary>
    [Serializable]
    public sealed class ParameterChange
    {
        [SerializeField] private string _parameterId = string.Empty;
        [SerializeField] private ParameterChangeOperation _operation = ParameterChangeOperation.Set;
        [SerializeField] private bool _boolValue;
        [SerializeField] private int _intValue;
        [SerializeField] private float _floatValue;
        [SerializeField] private string _stringValue = string.Empty;

        /// <summary>シリアライズ・インスペクタの「+」で使う。</summary>
        public ParameterChange()
        {
        }

        /// <param name="parameterId">変えるパラメータの <see cref="GraphParameter.Id"/>。</param>
        /// <param name="operation">操作。</param>
        public ParameterChange(string parameterId, ParameterChangeOperation operation = ParameterChangeOperation.Set)
        {
            _parameterId = parameterId ?? string.Empty;
            _operation = operation;
        }

        /// <summary>変えるパラメータの ID。</summary>
        public string ParameterId
        {
            get => _parameterId;
            set => _parameterId = value ?? string.Empty;
        }

        /// <summary>操作。</summary>
        public ParameterChangeOperation Operation
        {
            get => _operation;
            set => _operation = value;
        }

        /// <summary>Bool のパラメータに入れる値。</summary>
        public bool BoolValue
        {
            get => _boolValue;
            set => _boolValue = value;
        }

        /// <summary>Int のパラメータに入れる・足す値。</summary>
        public int IntValue
        {
            get => _intValue;
            set => _intValue = value;
        }

        /// <summary>Float のパラメータに入れる・足す値。</summary>
        public float FloatValue
        {
            get => _floatValue;
            set => _floatValue = value;
        }

        /// <summary>String のパラメータに入れる値。</summary>
        public string StringValue
        {
            get => _stringValue;
            set => _stringValue = value ?? string.Empty;
        }

        /// <summary>その型で使える操作か（Add は Int・Float だけ）。</summary>
        public static bool IsAllowed(ParameterChangeOperation operation, GraphParameterType type) =>
            operation == ParameterChangeOperation.Set || type == GraphParameterType.Int || type == GraphParameterType.Float;

        /// <summary>
        /// 設定の問題。パラメータが <paramref name="graph"/> に無い・操作が型に合わないときに理由を返す。問題が無ければ null。
        /// </summary>
        public string GetProblem(NodeGraphAsset graph)
        {
            var parameter = graph != null ? graph.FindParameter(_parameterId) : null;
            if (parameter == null)
            {
                return string.IsNullOrEmpty(_parameterId) ? "no parameter is chosen" : "its parameter was deleted";
            }

            return IsAllowed(_operation, parameter.Type) ? null : $"'{parameter.Name}' is {parameter.Type}, which cannot be added to";
        }

        /// <summary>
        /// <paramref name="runner"/> のパラメータを変える。パラメータが無い・型が合わない・操作が型に合わなければ何もせず、理由を返す。変えたら null。
        /// </summary>
        public string ApplyTo(GraphRunner runner)
        {
            if (runner == null)
            {
                throw new ArgumentNullException(nameof(runner));
            }

            var problem = GetProblem(runner.Graph);
            if (problem != null)
            {
                return problem;
            }

            var parameter = runner.Graph.FindParameter(_parameterId);
            // Runner の値は開始時のアセットの写し。Play 中に足した・型を変えたパラメータは、Runner に同じ名前と型が無い
            if (!runner.HasParameter(parameter.Name, parameter.Type))
            {
                return $"the running graph has no {parameter.Type} parameter '{parameter.Name}' (it was added or changed after the runner started)";
            }

            var add = _operation == ParameterChangeOperation.Add;
            switch (parameter.Type)
            {
                case GraphParameterType.Bool:
                    runner.SetBool(parameter.Name, _boolValue);
                    break;
                case GraphParameterType.Int:
                    runner.SetInt(parameter.Name, add ? unchecked(runner.GetInt(parameter.Name) + _intValue) : _intValue);
                    break;
                case GraphParameterType.Float:
                    runner.SetFloat(parameter.Name, add ? runner.GetFloat(parameter.Name) + _floatValue : _floatValue);
                    break;
                case GraphParameterType.String:
                    runner.SetString(parameter.Name, _stringValue);
                    break;
            }

            return null;
        }

        /// <summary>
        /// 1 行の説明（例: <c>Score = 0</c>、<c>Level += 1</c>、<c>Lives -= 1</c>、<c>Name = "Alice"</c>）。パラメータが無ければ <c>(missing) = …</c>。
        /// </summary>
        public string Describe(NodeGraphAsset graph)
        {
            var parameter = graph != null ? graph.FindParameter(_parameterId) : null;
            var name = parameter != null ? parameter.Name : "(missing)";
            var type = parameter?.Type ?? GraphParameterType.Int;
            var add = _operation == ParameterChangeOperation.Add;
            switch (type)
            {
                case GraphParameterType.Bool:
                    return $"{name} = {(_boolValue ? "true" : "false")}";
                case GraphParameterType.String:
                    return $"{name} = \"{_stringValue}\"";
                case GraphParameterType.Float:
                    return !add ? $"{name} = {Format(_floatValue)}"
                        : _floatValue < 0f ? $"{name} -= {Format(-_floatValue)}" : $"{name} += {Format(_floatValue)}";
                default:
                    return !add ? $"{name} = {_intValue.ToString(CultureInfo.InvariantCulture)}"
                        : _intValue < 0 ? $"{name} -= {(-(long)_intValue).ToString(CultureInfo.InvariantCulture)}"
                        : $"{name} += {_intValue.ToString(CultureInfo.InvariantCulture)}";
            }
        }

        private static string Format(float value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
