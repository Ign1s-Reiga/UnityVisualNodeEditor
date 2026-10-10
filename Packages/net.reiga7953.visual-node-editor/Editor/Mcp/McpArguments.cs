using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Reiga.VisualNodeEditor.Editor.Mcp
{
    /// <summary>ツールの引数（<c>tools/call</c> の <c>arguments</c>）を読む。型が違う・必須が無いときは <see cref="McpToolException"/>。</summary>
    public sealed class McpArguments
    {
        private readonly IDictionary<string, object> _values;

        /// <summary><c>tools/call</c> の <c>arguments</c>（無ければ空として扱う）。</summary>
        public McpArguments(IDictionary<string, object> values)
        {
            _values = values ?? new Dictionary<string, object>();
        }

        /// <summary>引数が指定されているか（null も指定なしとみなす）。</summary>
        public bool Has(string name) => _values.TryGetValue(name, out var value) && value != null;

        /// <summary>必須の文字列（空も不可）。</summary>
        public string GetString(string name)
        {
            var value = GetOptionalString(name);
            if (string.IsNullOrEmpty(value))
            {
                throw new McpToolException($"The argument '{name}' is required.");
            }

            return value;
        }

        /// <summary>任意の文字列。無ければ null。</summary>
        public string GetOptionalString(string name)
        {
            if (!_values.TryGetValue(name, out var value) || value == null)
            {
                return null;
            }

            return value as string ?? throw new McpToolException($"The argument '{name}' must be a string.");
        }

        /// <summary>任意の数（有限のものだけ。NaN・無限大は不可）。無ければ null。</summary>
        public float? GetOptionalFloat(string name)
        {
            if (!_values.TryGetValue(name, out var value) || value == null)
            {
                return null;
            }

            float result = value switch
            {
                long integer => integer,
                double number => (float)number,
                string text when float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
                _ => throw new McpToolException($"The argument '{name}' must be a number."),
            };

            // NaN や無限大を位置に入れると、ノードがキャンバスから消えてしまう
            if (float.IsNaN(result) || float.IsInfinity(result))
            {
                throw new McpToolException($"The argument '{name}' must be a finite number.");
            }

            return result;
        }

        /// <summary>任意の真偽値。無ければ <paramref name="defaultValue"/>。</summary>
        public bool GetBool(string name, bool defaultValue = false)
        {
            if (!_values.TryGetValue(name, out var value) || value == null)
            {
                return defaultValue;
            }

            return value as bool? ?? throw new McpToolException($"The argument '{name}' must be true or false.");
        }

        /// <summary>必須の文字列の配列（空は不可）。</summary>
        public List<string> GetStringList(string name)
        {
            if (!_values.TryGetValue(name, out var value) || !(value is IEnumerable<object> items))
            {
                throw new McpToolException($"The argument '{name}' must be an array of strings.");
            }

            var list = items.Select(item => item as string ?? throw new McpToolException($"The argument '{name}' must be an array of strings.")).ToList();
            if (list.Count == 0)
            {
                throw new McpToolException($"The argument '{name}' must not be empty.");
            }

            return list;
        }

        /// <summary>引数の名前（未知の引数の確認用）。</summary>
        public IEnumerable<string> Names => _values.Keys;

        /// <summary>指定されたが、<paramref name="known"/> に無い引数の名前（打ち間違いを知らせるため）。</summary>
        public List<string> GetUnknown(IEnumerable<string> known)
        {
            var set = new HashSet<string>(known, StringComparer.Ordinal);
            return _values.Keys.Where(key => !set.Contains(key)).ToList();
        }
    }
}
