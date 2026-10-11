using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Reiga.VisualNodeEditor
{
    /// <summary>
    /// Event ノードの条件 1 つ（パラメータ・比較・値）。パラメータは ID で指す（改名しても壊れない）。
    /// ロジックを組むためのものではなく、宣言的な比較 1 つだけを表す（式や計算は書けない）。
    /// </summary>
    [Serializable]
    public sealed class TransitionCondition
    {
        private static readonly ConditionOperator[] BoolOperators = { ConditionOperator.IsTrue, ConditionOperator.IsFalse };

        private static readonly ConditionOperator[] NumberOperators =
        {
            ConditionOperator.Equal, ConditionOperator.NotEqual, ConditionOperator.Less,
            ConditionOperator.LessOrEqual, ConditionOperator.Greater, ConditionOperator.GreaterOrEqual,
        };

        private static readonly ConditionOperator[] StringOperators = { ConditionOperator.Equal, ConditionOperator.NotEqual };

        [SerializeField] private string _parameterId;
        [SerializeField] private ConditionOperator _operator;
        [SerializeField] private int _intValue;
        [SerializeField] private float _floatValue;
        [SerializeField] private string _stringValue = string.Empty;

        public TransitionCondition()
        {
        }

        public TransitionCondition(string parameterId, ConditionOperator op)
        {
            _parameterId = parameterId;
            _operator = op;
        }

        /// <summary>比べるパラメータの ID（<see cref="GraphParameter.Id"/>）。</summary>
        public string ParameterId
        {
            get => _parameterId;
            set => _parameterId = value;
        }

        /// <summary>比較。</summary>
        public ConditionOperator Operator
        {
            get => _operator;
            set => _operator = value;
        }

        /// <summary>Int のパラメータと比べる値。</summary>
        public int IntValue
        {
            get => _intValue;
            set => _intValue = value;
        }

        /// <summary>Float のパラメータと比べる値。</summary>
        public float FloatValue
        {
            get => _floatValue;
            set => _floatValue = value;
        }

        /// <summary>String のパラメータと比べる値。</summary>
        public string StringValue
        {
            get => _stringValue ?? string.Empty;
            set => _stringValue = value ?? string.Empty;
        }

        /// <summary>その型のパラメータに使える比較（インスペクタで選べる順）。</summary>
        public static IReadOnlyList<ConditionOperator> GetOperators(GraphParameterType type) => OperatorsFor(type);

        /// <summary>その型のパラメータに <paramref name="op"/> が使えるか。</summary>
        public static bool Supports(GraphParameterType type, ConditionOperator op) => Array.IndexOf(OperatorsFor(type), op) >= 0;

        private static ConditionOperator[] OperatorsFor(GraphParameterType type) => type switch
        {
            GraphParameterType.Bool => BoolOperators,
            GraphParameterType.String => StringOperators,
            _ => NumberOperators,
        };

        /// <summary>
        /// 型が <paramref name="type"/> のパラメータの今の値 <paramref name="value"/> が、この条件を満たすか。
        /// 比較が型に合わない・値の型が違うときは満たさない。
        /// </summary>
        public bool IsMet(GraphParameterType type, object value)
        {
            if (!Supports(type, _operator))
            {
                return false;
            }

            switch (type)
            {
                case GraphParameterType.Bool:
                    return value is bool flag && (_operator == ConditionOperator.IsTrue ? flag : !flag);
                case GraphParameterType.Int:
                    return value is int number && Compare(number.CompareTo(_intValue));
                case GraphParameterType.Float:
                    return value is float real && Compare(real.CompareTo(_floatValue));
                default:
                    var equal = string.Equals(value as string ?? string.Empty, StringValue, StringComparison.Ordinal);
                    return _operator == ConditionOperator.Equal ? equal : !equal;
            }
        }

        /// <summary>
        /// 人が読める形（ノードのサマリー行などに出す）。例: <c>Affection &gt;= 5</c>、<c>Met</c>、<c>not Met</c>、<c>Name == "Alice"</c>。
        /// パラメータが無い（削除された）ときは <c>(missing)</c>。
        /// </summary>
        public string Describe(GraphParameter parameter)
        {
            if (parameter == null)
            {
                return "(missing)";
            }

            var name = string.IsNullOrEmpty(parameter.Name) ? "(unnamed)" : parameter.Name;
            switch (_operator)
            {
                case ConditionOperator.IsTrue:
                    return name;
                case ConditionOperator.IsFalse:
                    return "not " + name;
            }

            var value = parameter.Type switch
            {
                GraphParameterType.Int => _intValue.ToString(CultureInfo.InvariantCulture),
                GraphParameterType.Float => _floatValue.ToString("0.###", CultureInfo.InvariantCulture),
                GraphParameterType.String => $"\"{StringValue}\"",
                _ => "?",
            };
            return $"{name} {GetSymbol(_operator)} {value}";
        }

        /// <summary>比較の記号（インスペクタのドロップダウンと <see cref="Describe"/> が使う）。</summary>
        public static string GetSymbol(ConditionOperator op) => op switch
        {
            ConditionOperator.IsTrue => "is true",
            ConditionOperator.IsFalse => "is false",
            ConditionOperator.Equal => "==",
            ConditionOperator.NotEqual => "!=",
            ConditionOperator.Less => "<",
            ConditionOperator.LessOrEqual => "<=",
            ConditionOperator.Greater => ">",
            _ => ">=",
        };

        private bool Compare(int comparison) => _operator switch
        {
            ConditionOperator.Equal => comparison == 0,
            ConditionOperator.NotEqual => comparison != 0,
            ConditionOperator.Less => comparison < 0,
            ConditionOperator.LessOrEqual => comparison <= 0,
            ConditionOperator.Greater => comparison > 0,
            ConditionOperator.GreaterOrEqual => comparison >= 0,
            _ => false,
        };
    }
}
