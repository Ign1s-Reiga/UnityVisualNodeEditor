using System;
using System.Collections.Generic;
using System.Linq;

namespace Reiga.VisualNodeEditor
{
    /// <summary>
    /// パラメータ（Blackboard）に無い値の置き場（<see cref="GraphRunner.Extras"/>）。キー → bool / int / float / string。
    /// Yarn Spinner の変数などアドオンが使う。Runner の <see cref="GraphRunner.Start"/> で空になり、セーブ（<see cref="GraphRunner.CaptureState"/>）に含まれる。
    /// </summary>
    public sealed class GraphExtras
    {
        private readonly Dictionary<string, object> _values = new(StringComparer.Ordinal);

        /// <summary>値の数。</summary>
        public int Count => _values.Count;

        /// <summary>キーの一覧。</summary>
        public IEnumerable<string> Keys => _values.Keys;

        /// <summary>そのキーの値があるか。</summary>
        public bool Contains(string key) => key != null && _values.ContainsKey(key);

        /// <summary>そのキーの値（bool / int / float / string）。無ければ false。</summary>
        public bool TryGetValue(string key, out object value)
        {
            value = null;
            return key != null && _values.TryGetValue(key, out value);
        }

        public void SetBool(string key, bool value) => Set(key, value);

        public void SetInt(string key, int value) => Set(key, value);

        public void SetFloat(string key, float value) => Set(key, value);

        public void SetString(string key, string value) => Set(key, value ?? string.Empty);

        /// <summary>値を入れる（同じキーがあれば置き換える。型が違ってもよい）。</summary>
        /// <exception cref="ArgumentException">キーが空、または値が bool / int / float / string でない。</exception>
        public void Set(string key, object value)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("The key must not be empty.", nameof(key));
            }

            if (!(value is bool || value is int || value is float || value is string))
            {
                throw new ArgumentException($"'{key}' must be a bool, int, float or string (got {value?.GetType().Name ?? "null"}).", nameof(value));
            }

            _values[key] = value;
        }

        /// <summary>値を外す。無ければ false。</summary>
        public bool Remove(string key) => key != null && _values.Remove(key);

        /// <summary>すべて外す。</summary>
        public void Clear() => _values.Clear();

        internal List<GraphStateValue> ToStateValues() => _values.Select(p => new GraphStateValue(p.Key, p.Value)).ToList();

        internal void ReplaceWith(IEnumerable<GraphStateValue> values)
        {
            _values.Clear();
            foreach (var value in values ?? Enumerable.Empty<GraphStateValue>())
            {
                if (value != null && !string.IsNullOrEmpty(value.Key))
                {
                    _values[value.Key] = value.Value;
                }
            }
        }
    }
}
