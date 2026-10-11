using System;
using UnityEngine;

namespace Reiga.VisualNodeEditor
{
    /// <summary>セーブに入れる値 1 つ（キー・型・値）。パラメータと <see cref="GraphExtras"/> の値に使う。</summary>
    [Serializable]
    public sealed class GraphStateValue
    {
        [SerializeField] private string _key;
        [SerializeField] private GraphParameterType _type;
        [SerializeField] private bool _boolValue;
        [SerializeField] private int _intValue;
        [SerializeField] private float _floatValue;
        [SerializeField] private string _stringValue = string.Empty;

        public GraphStateValue()
        {
        }

        /// <param name="key">パラメータ名、または Extras のキー。</param>
        /// <param name="value">bool / int / float / string のどれか。</param>
        /// <exception cref="ArgumentException">それ以外の型。</exception>
        public GraphStateValue(string key, object value)
        {
            _key = key;
            switch (value)
            {
                case bool flag:
                    _type = GraphParameterType.Bool;
                    _boolValue = flag;
                    break;
                case int number:
                    _type = GraphParameterType.Int;
                    _intValue = number;
                    break;
                case float real:
                    _type = GraphParameterType.Float;
                    _floatValue = real;
                    break;
                case string text:
                    _type = GraphParameterType.String;
                    _stringValue = text;
                    break;
                default:
                    throw new ArgumentException($"'{key}' must be a bool, int, float or string (got {value?.GetType().Name ?? "null"}).", nameof(value));
            }
        }

        /// <summary>パラメータ名、または Extras のキー。</summary>
        public string Key => _key;

        /// <summary>値の型。</summary>
        public GraphParameterType Type => _type;

        /// <summary>値（ボックス化される）。</summary>
        public object Value => _type switch
        {
            GraphParameterType.Bool => _boolValue,
            GraphParameterType.Int => _intValue,
            GraphParameterType.Float => _floatValue,
            _ => _stringValue ?? string.Empty,
        };
    }
}
