using System;
using System.Globalization;

namespace Reiga.VisualNodeEditor.Editor.Debugging
{
    /// <summary>Play 中に Blackboard の各パラメータの横に出す、実行中の値の文字列。</summary>
    public static class RuntimeParameterText
    {
        /// <summary>実行中の値（例: "Int = 3"、"String = \"Hard\""）。Runner にそのパラメータが無ければ型名だけ。</summary>
        public static string Format(GraphParameterType type, bool hasValue, object value)
        {
            if (!hasValue)
            {
                return type.ToString();
            }

            var text = value switch
            {
                bool b => b ? "true" : "false",
                float f => f.ToString("0.###", CultureInfo.InvariantCulture),
                string s => "\"" + s + "\"",
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                null => "null",
                _ => value.ToString(),
            };
            return $"{type} = {text}";
        }

        /// <summary>
        /// <paramref name="runner"/> の、そのパラメータの今の値。同じ名前と型のパラメータが Runner に無ければ hasValue が false
        /// （Runner は開始時のアセットの写しなので、Play 中に削除・改名すると同じ名前で型が違うことがある）。
        /// </summary>
        public static object GetValue(GraphRunner runner, GraphParameter parameter, out bool hasValue)
        {
            hasValue = runner != null && parameter != null && runner.HasParameter(parameter.Name, parameter.Type);
            if (!hasValue)
            {
                return null;
            }

            switch (parameter.Type)
            {
                case GraphParameterType.Bool:
                    return runner.GetBool(parameter.Name);
                case GraphParameterType.Int:
                    return runner.GetInt(parameter.Name);
                case GraphParameterType.Float:
                    return runner.GetFloat(parameter.Name);
                default:
                    return runner.GetString(parameter.Name);
            }
        }
    }
}
