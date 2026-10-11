using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Reiga.VisualNodeEditor.Editor.Mcp
{
    /// <summary>
    /// 組み込み以外のノード型（Yarn Spinner 連携の Dialogue ノードなど）の値を、MCP のツールで読み書きする（<c>fields</c>）。
    /// 対象は、その型（と、<see cref="NodeData"/> より下の基底）で宣言された public な読み書きできるプロパティのうち、
    /// string / bool / int / float のもの。<see cref="NodeData"/> 自身の値（ID・タイトル・位置・親など）は対象外（専用の欄で扱う）。
    /// </summary>
    public static class McpNodeFields
    {
        /// <summary><paramref name="node"/> の値（プロパティ名 → 値）。対象のプロパティが無ければ空。</summary>
        public static Dictionary<string, object> Describe(NodeData node) =>
            GetProperties(node.GetType()).ToDictionary(p => p.Name, p => p.GetValue(node) ?? (p.PropertyType == typeof(string) ? string.Empty : null));

        /// <summary>
        /// <paramref name="values"/>（プロパティ名 → 値）を確かめ、入れる処理を返す（確かめる途中では何も変えない）。
        /// 無いプロパティ・型の合わない値は、使える名前を添えて例外。
        /// </summary>
        public static Action Prepare(NodeData node, IDictionary<string, object> values)
        {
            var properties = GetProperties(node.GetType()).ToDictionary(p => p.Name, StringComparer.Ordinal);
            var changes = new List<Action>();
            foreach (var pair in values)
            {
                if (!properties.TryGetValue(pair.Key, out var property))
                {
                    var names = properties.Keys.ToList();
                    throw new McpToolException(
                        $"{node.GetType().Name} has no field '{pair.Key}'. " +
                        (names.Count == 0 ? "It has no fields to set here." : $"Fields: {string.Join(", ", names)}."));
                }

                var value = Convert(property, pair.Value);
                changes.Add(() => property.SetValue(node, value));
            }

            return () => changes.ForEach(change => change());
        }

        private static IEnumerable<PropertyInfo> GetProperties(Type type) =>
            type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0
                            && p.DeclaringType != typeof(NodeData) && typeof(NodeData).IsAssignableFrom(p.DeclaringType)
                            && TryGetParameterType(p.PropertyType, out _))
                .OrderBy(p => p.MetadataToken);

        /// <summary><paramref name="property"/> に入れる値にする。扱わない型のプロパティ・型の合わない値は例外。</summary>
        internal static object Convert(PropertyInfo property, object value)
        {
            if (!TryGetParameterType(property.PropertyType, out var parameterType))
            {
                throw new McpToolException(
                    $"The field '{property.Name}' is a {property.PropertyType.Name}, which cannot be set here (only string, bool, int and float fields can).");
            }

            if (value == null && parameterType == GraphParameterType.String)
            {
                return string.Empty;
            }

            return McpValues.ToParameterValue(value, parameterType)
                   ?? throw new McpToolException($"The field '{property.Name}' must be {McpValues.Describe(parameterType)}.");
        }

        // プロパティの型に当たるパラメータの型（値はパラメータと同じ規則で変える）。string / bool / int / float 以外は扱わない
        private static bool TryGetParameterType(Type type, out GraphParameterType parameterType)
        {
            if (type == typeof(string))
            {
                parameterType = GraphParameterType.String;
            }
            else if (type == typeof(bool))
            {
                parameterType = GraphParameterType.Bool;
            }
            else if (type == typeof(int))
            {
                parameterType = GraphParameterType.Int;
            }
            else if (type == typeof(float))
            {
                parameterType = GraphParameterType.Float;
            }
            else
            {
                parameterType = default;
                return false;
            }

            return true;
        }
    }
}
