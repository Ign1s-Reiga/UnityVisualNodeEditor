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
        /// Event 以外へのエッジがあれば Advance（行き先のタイトル付き）。自動の Event は自分で進むので出さない。
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
                .Where(e => !e.IsAutomatic)
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

        /// <summary>
        /// Raise の操作が今は進めない理由（その名前の Event の、満たしていない条件）。進めるなら空文字。
        /// 例: <c>Needs Affection &gt;= 5 (or Met)</c>。同じ名前の Event が複数あれば、それぞれの条件を or で並べる。
        /// </summary>
        public static string GetBlockedReason(GraphRunner runner, RuntimeAction action)
        {
            if (runner?.Current == null || action == null || action.Kind != RuntimeActionKind.Raise
                || runner.FindRaiseTarget(action.EventName) != null)
            {
                return string.Empty;
            }

            var alternatives = runner.Query.GetNext(runner.Current.Id)
                .OfType<EventNode>()
                .Where(e => !e.IsAutomatic && e.EventName == action.EventName)
                .Select(e => string.Join(" and ", e.Conditions
                    .Where(c => !runner.IsConditionMet(c))
                    .Select(c => c.Describe(c != null ? runner.Graph.FindParameter(c.ParameterId) : null))))
                .Where(text => text.Length > 0)
                .ToList();
            return alternatives.Count == 0 ? string.Empty : "Needs " + string.Join(" or ", alternatives);
        }
    }
}
