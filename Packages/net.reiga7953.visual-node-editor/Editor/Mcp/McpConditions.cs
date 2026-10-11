using System;
using System.Collections.Generic;
using System.Linq;

namespace Reiga.VisualNodeEditor.Editor.Mcp
{
    /// <summary>
    /// MCP のツールでの Event のトリガーと条件。条件はパラメータを名前で、比較を記号（<c>&gt;=</c> など）で表す
    /// （アセットでは ID と enum。エージェントが ID を引かなくてよいように）。
    /// </summary>
    public static class McpConditions
    {
        /// <summary>条件 1 つの欄（<c>conditions</c> の要素）。</summary>
        public static Dictionary<string, object> ItemProperties() => new()
        {
            ["parameter"] = McpSchema.String("Name of the Blackboard parameter to compare."),
            ["op"] = McpSchema.String("Comparison: 'is true' / 'is false' (Bool), '==' / '!=' (Int, Float, String), '<' / '<=' / '>' / '>=' (Int, Float)."),
            ["value"] = McpSchema.Any("Value to compare with: a whole number for Int, a number for Float, a string for String. Leave out for Bool."),
        };

        /// <summary><c>Raise</c> / <c>Automatic</c>（大文字小文字は問わない）を読む。</summary>
        public static EventTriggerMode ParseTrigger(string text)
        {
            if (Enum.TryParse<EventTriggerMode>((text ?? string.Empty).Trim(), true, out var trigger) && Enum.IsDefined(typeof(EventTriggerMode), trigger))
            {
                return trigger;
            }

            throw new McpToolException($"'{text}' is not a trigger. Use 'Raise' or 'Automatic'.");
        }

        /// <summary>
        /// 条件 1 つを読む。パラメータが無い・比較がパラメータの型に合わない・値の型が違うときは、直し方を添えて例外。
        /// </summary>
        public static TransitionCondition Parse(NodeGraphAsset asset, McpArguments item)
        {
            var name = item.GetString("parameter");
            var parameter = asset.FindParameterByName(name);
            if (parameter == null)
            {
                var names = asset.Parameters.Where(p => p != null).Select(p => p.Name).ToList();
                throw new McpToolException($"The graph has no parameter named '{name}'. " +
                                           (names.Count == 0 ? "Add one in the Blackboard first." : $"Parameters: {string.Join(", ", names)}."));
            }

            var op = ParseOperator(item.GetString("op"), parameter);
            var condition = new TransitionCondition(parameter.Id, op);
            if (parameter.Type == GraphParameterType.Bool)
            {
                // Bool は比較（is true / is false）だけで表す
                return condition;
            }

            if (!item.Has("value"))
            {
                throw new McpToolException($"'{parameter.Name}' is a {parameter.Type}, so the condition needs a 'value'.");
            }

            switch (McpValues.ToParameterValue(item.GetRaw("value"), parameter.Type))
            {
                case int whole:
                    condition.IntValue = whole;
                    break;
                case float real:
                    condition.FloatValue = real;
                    break;
                case string text:
                    condition.StringValue = text;
                    break;
                default:
                    throw new McpToolException($"'{parameter.Name}' is a {parameter.Type}, so 'value' must be {McpValues.Describe(parameter.Type)}.");
            }

            return condition;
        }

        /// <summary>条件 1 つを、<see cref="Parse"/> と同じ形の辞書にする（<c>get_graph</c> が使う）。</summary>
        public static Dictionary<string, object> Describe(NodeGraphAsset asset, TransitionCondition condition)
        {
            var parameter = asset.FindParameter(condition.ParameterId);
            var description = new Dictionary<string, object>
            {
                ["parameter"] = parameter?.Name,
                ["op"] = TransitionCondition.GetSymbol(condition.Operator),
                ["text"] = condition.Describe(parameter),
            };
            switch (parameter?.Type)
            {
                case GraphParameterType.Int:
                    description["value"] = condition.IntValue;
                    break;
                case GraphParameterType.Float:
                    description["value"] = condition.FloatValue;
                    break;
                case GraphParameterType.String:
                    description["value"] = condition.StringValue;
                    break;
            }

            return description;
        }

        // 記号（'>=' など）か enum の名前（'GreaterOrEqual'）を読み、パラメータの型で使えるかを確かめる
        private static ConditionOperator ParseOperator(string text, GraphParameter parameter)
        {
            var trimmed = text.Trim();
            var allowed = TransitionCondition.GetOperators(parameter.Type);
            foreach (ConditionOperator op in Enum.GetValues(typeof(ConditionOperator)))
            {
                if (string.Equals(TransitionCondition.GetSymbol(op), trimmed, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(op.ToString(), trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    if (!allowed.Contains(op))
                    {
                        break;
                    }

                    return op;
                }
            }

            throw new McpToolException($"'{text}' cannot compare the {parameter.Type} parameter '{parameter.Name}'. " +
                                       $"Use one of: {string.Join(", ", allowed.Select(o => "'" + TransitionCondition.GetSymbol(o) + "'"))}.");
        }
    }
}
