using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Reiga.VisualNodeEditor.Editor.Mcp
{
    /// <summary>
    /// MCP（JSON-RPC）で使う小さな JSON の読み書き（パッケージの依存を増やさないため）。
    /// 読むと、オブジェクトは <c>Dictionary&lt;string, object&gt;</c>、配列は <c>List&lt;object&gt;</c>、
    /// 数は整数なら <c>long</c>・それ以外は <c>double</c>、残りは <c>string</c> / <c>bool</c> / <c>null</c>。
    /// 書くときは辞書・列・文字列・数・bool・列挙値（名前）・null を受け付ける。
    /// </summary>
    public static class McpJson
    {
        /// <summary>読める入れ子（オブジェクト・配列）の深さ。MCP のメッセージはこれよりずっと浅い。</summary>
        public const int MaxDepth = 64;

        /// <summary>JSON の文字列を読む。JSON として正しくない・入れ子が <see cref="MaxDepth"/> より深ければ <see cref="FormatException"/>。</summary>
        public static object Parse(string json)
        {
            if (json == null)
            {
                throw new FormatException("No JSON.");
            }

            var reader = new Reader(json);
            var value = reader.ReadValue();
            reader.SkipWhitespace();
            if (!reader.AtEnd)
            {
                throw reader.Error("Unexpected text after the JSON value");
            }

            return value;
        }

        /// <summary>値を JSON の文字列にする（改行・字下げなし）。</summary>
        public static string Serialize(object value)
        {
            var builder = new StringBuilder();
            Write(builder, value);
            return builder.ToString();
        }

        private static void Write(StringBuilder builder, object value)
        {
            switch (value)
            {
                case null:
                    builder.Append("null");
                    break;
                case string text:
                    WriteString(builder, text);
                    break;
                case bool flag:
                    builder.Append(flag ? "true" : "false");
                    break;
                case Enum enumValue:
                    WriteString(builder, enumValue.ToString());
                    break;
                case float single:
                    WriteNumber(builder, single);
                    break;
                case double number:
                    WriteNumber(builder, number);
                    break;
                case int _:
                case long _:
                case short _:
                case byte _:
                case uint _:
                case ulong _:
                case ushort _:
                case sbyte _:
                case decimal _:
                    builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                    break;
                case IDictionary dictionary:
                    WriteObject(builder, dictionary);
                    break;
                case IEnumerable items:
                    WriteArray(builder, items);
                    break;
                default:
                    WriteString(builder, value.ToString());
                    break;
            }
        }

        private static void WriteNumber(StringBuilder builder, double number)
        {
            // JSON に NaN / Infinity は無い
            if (double.IsNaN(number) || double.IsInfinity(number))
            {
                builder.Append("null");
                return;
            }

            builder.Append(number.ToString("R", CultureInfo.InvariantCulture));
        }

        private static void WriteObject(StringBuilder builder, IDictionary dictionary)
        {
            builder.Append('{');
            var first = true;
            foreach (DictionaryEntry entry in dictionary)
            {
                if (!first)
                {
                    builder.Append(',');
                }

                first = false;
                WriteString(builder, Convert.ToString(entry.Key, CultureInfo.InvariantCulture));
                builder.Append(':');
                Write(builder, entry.Value);
            }

            builder.Append('}');
        }

        private static void WriteArray(StringBuilder builder, IEnumerable items)
        {
            builder.Append('[');
            var first = true;
            foreach (var item in items)
            {
                if (!first)
                {
                    builder.Append(',');
                }

                first = false;
                Write(builder, item);
            }

            builder.Append(']');
        }

        private static void WriteString(StringBuilder builder, string text)
        {
            builder.Append('"');
            foreach (var c in text)
            {
                switch (c)
                {
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    case '\b':
                        builder.Append("\\b");
                        break;
                    case '\f':
                        builder.Append("\\f");
                        break;
                    default:
                        if (c < 0x20)
                        {
                            builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(c);
                        }

                        break;
                }
            }

            builder.Append('"');
        }

        private sealed class Reader
        {
            private readonly string _text;
            private int _index;
            private int _depth;

            public Reader(string text)
            {
                _text = text;
            }

            public bool AtEnd => _index >= _text.Length;

            // 入れ子を読むたびに深さを数え、MaxDepth を超えたら止める（読むのは再帰なので、深すぎるとスタックが溢れて
            // エディタごと落ちる。その例外は捕まえられない）
            private object ReadNested(Func<object> read)
            {
                if (++_depth > MaxDepth)
                {
                    throw Error($"JSON nested deeper than {MaxDepth} levels");
                }

                try
                {
                    return read();
                }
                finally
                {
                    _depth--;
                }
            }

            public FormatException Error(string message) => new FormatException($"{message} at position {_index}.");

            public void SkipWhitespace()
            {
                while (!AtEnd && char.IsWhiteSpace(_text[_index]))
                {
                    _index++;
                }
            }

            public object ReadValue()
            {
                SkipWhitespace();
                if (AtEnd)
                {
                    throw Error("Unexpected end of JSON");
                }

                var c = _text[_index];
                switch (c)
                {
                    case '{':
                        return ReadNested(ReadObject);
                    case '[':
                        return ReadNested(ReadArray);
                    case '"':
                        return ReadString();
                    case 't':
                        ReadLiteral("true");
                        return true;
                    case 'f':
                        ReadLiteral("false");
                        return false;
                    case 'n':
                        ReadLiteral("null");
                        return null;
                    default:
                        if (c == '-' || char.IsDigit(c))
                        {
                            return ReadNumber();
                        }

                        throw Error($"Unexpected character '{c}'");
                }
            }

            private Dictionary<string, object> ReadObject()
            {
                var result = new Dictionary<string, object>(StringComparer.Ordinal);
                _index++;
                SkipWhitespace();
                if (Peek('}'))
                {
                    _index++;
                    return result;
                }

                while (true)
                {
                    SkipWhitespace();
                    if (!Peek('"'))
                    {
                        throw Error("Expected a property name");
                    }

                    var key = ReadString();
                    SkipWhitespace();
                    Expect(':');
                    result[key] = ReadValue();
                    SkipWhitespace();
                    if (Peek(','))
                    {
                        _index++;
                        continue;
                    }

                    Expect('}');
                    return result;
                }
            }

            private List<object> ReadArray()
            {
                var result = new List<object>();
                _index++;
                SkipWhitespace();
                if (Peek(']'))
                {
                    _index++;
                    return result;
                }

                while (true)
                {
                    result.Add(ReadValue());
                    SkipWhitespace();
                    if (Peek(','))
                    {
                        _index++;
                        continue;
                    }

                    Expect(']');
                    return result;
                }
            }

            private string ReadString()
            {
                _index++;
                var builder = new StringBuilder();
                while (true)
                {
                    if (AtEnd)
                    {
                        throw Error("Unterminated string");
                    }

                    var c = _text[_index++];
                    if (c == '"')
                    {
                        return builder.ToString();
                    }

                    if (c != '\\')
                    {
                        builder.Append(c);
                        continue;
                    }

                    if (AtEnd)
                    {
                        throw Error("Unterminated escape");
                    }

                    var escape = _text[_index++];
                    switch (escape)
                    {
                        case '"':
                        case '\\':
                        case '/':
                            builder.Append(escape);
                            break;
                        case 'b':
                            builder.Append('\b');
                            break;
                        case 'f':
                            builder.Append('\f');
                            break;
                        case 'n':
                            builder.Append('\n');
                            break;
                        case 'r':
                            builder.Append('\r');
                            break;
                        case 't':
                            builder.Append('\t');
                            break;
                        case 'u':
                            if (_index + 4 > _text.Length
                                || !int.TryParse(_text.Substring(_index, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code))
                            {
                                throw Error("Invalid \\u escape");
                            }

                            builder.Append((char)code);
                            _index += 4;
                            break;
                        default:
                            throw Error($"Invalid escape '\\{escape}'");
                    }
                }
            }

            private object ReadNumber()
            {
                var start = _index;
                if (Peek('-'))
                {
                    _index++;
                }

                var isInteger = true;
                while (!AtEnd)
                {
                    var c = _text[_index];
                    if (char.IsDigit(c))
                    {
                        _index++;
                    }
                    else if (c == '.' || c == 'e' || c == 'E' || c == '+' || c == '-')
                    {
                        isInteger = false;
                        _index++;
                    }
                    else
                    {
                        break;
                    }
                }

                var token = _text.Substring(start, _index - start);
                if (isInteger && long.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var integer))
                {
                    return integer;
                }

                if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                {
                    return number;
                }

                throw Error($"Invalid number '{token}'");
            }

            private void ReadLiteral(string literal)
            {
                if (string.CompareOrdinal(_text, _index, literal, 0, literal.Length) != 0)
                {
                    throw Error($"Expected '{literal}'");
                }

                _index += literal.Length;
            }

            private bool Peek(char c) => !AtEnd && _text[_index] == c;

            private void Expect(char c)
            {
                if (!Peek(c))
                {
                    throw Error($"Expected '{c}'");
                }

                _index++;
            }
        }
    }
}
