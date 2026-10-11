using System;
using System.Globalization;

namespace Reiga.VisualNodeEditor.Editor.Mcp
{
    /// <summary>
    /// MCP の JSON の値（bool / long / double / string）を、パラメータの型の値にする。
    /// 条件の値（<see cref="McpConditions"/>）・パラメータの既定値（<see cref="GraphEdits.AddParameter"/>）・ノードの値（<see cref="McpNodeFields"/>）で共通に使い、
    /// 範囲外の数を黙って変えないようにする。
    /// </summary>
    public static class McpValues
    {
        /// <summary>
        /// <paramref name="raw"/> を <paramref name="type"/> の値（bool / int / float / string）にする。合わなければ null
        /// （Int に小数・int の範囲外の数、Float に NaN・無限大・float の範囲外の数、型の違う値）。
        /// Int と Float には、数の文字列（<c>"5"</c>・<c>"0.5"</c>。書き方は InvariantCulture）も数として受け付ける。
        /// </summary>
        public static object ToParameterValue(object raw, GraphParameterType type)
        {
            // 数を文字列で渡すエージェントもいる（0.13.0 までも受け付けていた）
            if ((type == GraphParameterType.Int || type == GraphParameterType.Float) && raw is string text)
            {
                if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                {
                    return null;
                }

                raw = parsed;
            }

            switch (type)
            {
                case GraphParameterType.Bool:
                    return raw is bool flag ? flag : (object)null;
                case GraphParameterType.Int:
                    return raw switch
                    {
                        long whole when whole >= int.MinValue && whole <= int.MaxValue => (int)whole,
                        double real when real == Math.Floor(real) && real >= int.MinValue && real <= int.MaxValue => (int)real,
                        _ => null,
                    };
                case GraphParameterType.Float:
                    return raw switch
                    {
                        long whole => (float)whole,
                        double real when !double.IsNaN(real) && !double.IsInfinity(real) && Math.Abs(real) <= float.MaxValue => (float)real,
                        _ => null,
                    };
                default:
                    return raw as string;
            }
        }

        /// <summary>その型に入れられる値の説明（エラーの文に使う）。</summary>
        public static string Describe(GraphParameterType type) => type switch
        {
            GraphParameterType.Bool => "true or false",
            GraphParameterType.Int => "a whole number",
            GraphParameterType.Float => "a finite number",
            _ => "a string",
        };
    }
}
