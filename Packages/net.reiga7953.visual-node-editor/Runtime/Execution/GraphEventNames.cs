using System;
using System.Collections.Generic;

namespace Reiga.VisualNodeEditor
{
    /// <summary>
    /// グラフにあるイベント名（Event ノードの名前）。ボタンのイベント名を一覧から選ばせたり、名前の食い違いを見つけたりするのに使う
    /// （同じ文字列を 2 回打たせないため）。
    /// </summary>
    public static class GraphEventNames
    {
        /// <summary>空でないイベント名を、グラフ内のノードの順に、重複を除いて返す（大文字小文字は区別する）。</summary>
        public static List<string> Collect(NodeGraphAsset graph)
        {
            var names = new List<string>();
            if (graph == null)
            {
                return names;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in graph.Nodes)
            {
                if (node is EventNode eventNode && !string.IsNullOrEmpty(eventNode.EventName) && seen.Add(eventNode.EventName))
                {
                    names.Add(eventNode.EventName);
                }
            }

            return names;
        }
    }
}
