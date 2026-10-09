using System.Collections.Generic;
using System.Linq;
using Reiga.VisualNodeEditor.Editor.Views;

namespace Reiga.VisualNodeEditor.Editor.Debugging
{
    /// <summary>Play 中に、現在のノードからエディタで起こせる操作を決める（シーンにボタンを置かなくても流れを確かめられるように）。</summary>
    public static class RuntimeActions
    {
        /// <summary>
        /// <paramref name="current"/> から起こせる操作。出ている Event ノードのイベント名（重複なし・空を除く・エッジの順）ごとの Raise と、
        /// Event 以外へのエッジがあれば Advance（行き先のタイトル付き）。
        /// </summary>
        public static List<RuntimeAction> For(GraphQuery query, NodeData current)
        {
            var actions = new List<RuntimeAction>();
            if (query == null || current == null)
            {
                return actions;
            }

            var eventNames = query.GetNext(current.Id)
                .OfType<EventNode>()
                .Select(e => e.EventName)
                .Where(name => !string.IsNullOrEmpty(name))
                .Distinct();
            actions.AddRange(eventNames.Select(RuntimeAction.Raise));

            var next = query.GetFirstNonEventNext(current.Id);
            if (next != null)
            {
                actions.Add(RuntimeAction.Advance(NodeDisplay.GetNodeLabel(next)));
            }

            return actions;
        }
    }
}
