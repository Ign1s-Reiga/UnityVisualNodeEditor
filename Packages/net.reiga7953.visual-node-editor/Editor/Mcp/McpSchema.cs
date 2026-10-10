using System.Collections.Generic;
using System.Linq;

namespace Reiga.VisualNodeEditor.Editor.Mcp
{
    /// <summary>ツールの引数の JSON Schema を組み立てる。</summary>
    public static class McpSchema
    {
        /// <summary>オブジェクト（ツールの引数全体）。<paramref name="required"/> は必須の引数の名前。</summary>
        public static Dictionary<string, object> Object(IDictionary<string, object> properties, params string[] required)
        {
            var schema = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = properties ?? new Dictionary<string, object>(),
                ["additionalProperties"] = false,
            };
            if (required != null && required.Length > 0)
            {
                schema["required"] = required.ToList();
            }

            return schema;
        }

        /// <summary>引数の無いツール。</summary>
        public static Dictionary<string, object> Empty() => Object(null);

        /// <summary>文字列の引数。</summary>
        public static Dictionary<string, object> String(string description) =>
            new() { ["type"] = "string", ["description"] = description };

        /// <summary>数の引数。</summary>
        public static Dictionary<string, object> Number(string description) =>
            new() { ["type"] = "number", ["description"] = description };

        /// <summary>真偽値の引数。</summary>
        public static Dictionary<string, object> Boolean(string description) =>
            new() { ["type"] = "boolean", ["description"] = description };

        /// <summary>文字列の配列の引数。</summary>
        public static Dictionary<string, object> StringArray(string description) =>
            new() { ["type"] = "array", ["items"] = new Dictionary<string, object> { ["type"] = "string" }, ["description"] = description };
    }
}
