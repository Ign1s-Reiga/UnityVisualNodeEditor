using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Reiga.VisualNodeEditor.Editor.Inspector
{
    /// <summary>アセットのインスペクタに出す概要（要素の数・パラメータの表示）。描画に依存しない純粋な関数。</summary>
    public static class GraphSummary
    {
        /// <summary>要素の種類ごとの数（表示順）。読み込めなかったノード（null）は数えない。</summary>
        public static List<(string Label, int Count)> GetCounts(NodeGraphAsset graph) => new List<(string, int)>
        {
            ("Nodes", graph.Nodes.Count(n => n != null)),
            ("Edges", graph.Edges.Count(e => e != null)),
            ("Groups", graph.Groups.Count(g => g != null)),
            ("Sticky Notes", graph.StickyNotes.Count(s => s != null)),
            ("Parameters", graph.Parameters.Count(p => p != null)),
        };

        /// <summary>パラメータ 1 件の表示。例: <c>Lives : Int = 3</c>、<c>Player : String = "Ann"</c>。</summary>
        public static string DescribeParameter(GraphParameter parameter)
        {
            var name = string.IsNullOrWhiteSpace(parameter.Name) ? "(no name)" : parameter.Name;
            var value = parameter.Type switch
            {
                GraphParameterType.Bool => parameter.BoolValue ? "true" : "false",
                GraphParameterType.Int => parameter.IntValue.ToString(CultureInfo.InvariantCulture),
                GraphParameterType.Float => parameter.FloatValue.ToString("0.###", CultureInfo.InvariantCulture),
                _ => "\"" + parameter.StringValue + "\"",
            };
            return $"{name} : {parameter.Type} = {value}";
        }
    }
}
