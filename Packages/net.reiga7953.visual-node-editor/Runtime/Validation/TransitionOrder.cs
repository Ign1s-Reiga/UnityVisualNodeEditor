using System;
using System.Collections.Generic;
using System.Linq;

namespace Reiga.VisualNodeEditor
{
    /// <summary>
    /// 同じノードから出ているエッジの順（<see cref="GraphRunner"/> が遷移を調べる順）で、前のエッジがあるために選ばれることの無いエッジを見つける。
    /// 検証とインスペクタの Transitions で同じ判定を使う。docs/01-architecture.md「条件付き遷移」の「検証」と対応する。
    /// </summary>
    internal static class TransitionOrder
    {
        /// <summary>
        /// <paramref name="source"/> から出ているエッジ（<paramref name="outgoingOf"/> がノード ID から返す、アセット内の順）のうち、選ばれることの無いもの（エッジの順）。
        /// 行き先の無いエッジ・前と同じ行き先へのエッジは数えない（Runner と同じ）。コンテナ（出口ごとのエッジ）と Note は対象外で、常に空。
        /// </summary>
        public static List<HiddenTransition> FindHidden(
            NodeData source, Func<string, IEnumerable<EdgeData>> outgoingOf, Func<string, NodeData> findNode)
        {
            var hidden = new List<HiddenTransition>();
            if (source == null || source is ContainerNode || source is NoteNode || outgoingOf == null)
            {
                return hidden;
            }

            // 行き先ごとに最初のエッジだけ（Runner の GetNext と同じ）
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var targets = new List<(EdgeData Edge, NodeData Target)>();
            foreach (var edge in outgoingOf(source.Id) ?? Array.Empty<EdgeData>())
            {
                var target = edge == null || edge.FromNodeId != source.Id ? null : findNode(edge.ToNodeId ?? string.Empty);
                if (target != null && seen.Add(target.Id))
                {
                    targets.Add((edge, target));
                }
            }

            // Event なら最初の行き先、それ以外なら Event 以外の最初の行き先（Advance・Entry の行き先）
            var firstNext = targets.Select(t => t.Target).FirstOrDefault(t => source is EventNode || !(t is EventNode));

            // Entry は Event 以外の行き先があれば、待たずにそこへ進む（Event は起こせない）
            var passesThrough = (source is EntryNode || source is ContainerEntryNode) && firstNext != null;

            // 待つノードでも、条件の無い自動の Event が先へ進むなら、入ってすぐに出ていく（Raise・Advance は選ばれない）
            var leavesAt = passesThrough || source is EventNode ? null : FindLeavingEvent(targets, outgoingOf, findNode);

            EventNode unconditionedAutomatic = null;
            var unconditionedRaise = new Dictionary<string, EventNode>(StringComparer.Ordinal);
            foreach (var (edge, target) in targets)
            {
                if (source is EventNode || !(target is EventNode eventNode))
                {
                    if (target != firstNext)
                    {
                        var reason = source is EventNode ? HiddenTransitionReason.EventFollowsFirstEdge : HiddenTransitionReason.NotFirstNext;
                        hidden.Add(new HiddenTransition(edge, target, firstNext, reason));
                    }
                    else if (leavesAt != null)
                    {
                        hidden.Add(new HiddenTransition(edge, target, leavesAt, HiddenTransitionReason.LeavesRightAway));
                    }

                    continue;
                }

                if (passesThrough)
                {
                    hidden.Add(new HiddenTransition(edge, target, firstNext, HiddenTransitionReason.EntryGoesOn));
                    continue;
                }

                if (eventNode.IsAutomatic)
                {
                    if (unconditionedAutomatic != null)
                    {
                        hidden.Add(new HiddenTransition(edge, target, unconditionedAutomatic, HiddenTransitionReason.AutomaticAfterUnconditioned));
                    }
                    else if (eventNode.Conditions.Count == 0)
                    {
                        unconditionedAutomatic = eventNode;
                    }

                    continue;
                }

                // 名前の無い Raise の Event は起こせない（別の Warning）
                if (string.IsNullOrEmpty(eventNode.EventName))
                {
                    continue;
                }

                if (leavesAt != null)
                {
                    hidden.Add(new HiddenTransition(edge, target, leavesAt, HiddenTransitionReason.LeavesRightAway));
                }
                else if (unconditionedRaise.TryGetValue(eventNode.EventName, out var unconditioned))
                {
                    hidden.Add(new HiddenTransition(edge, target, unconditioned, HiddenTransitionReason.RaiseAfterUnconditioned));
                }
                else if (eventNode.Conditions.Count == 0)
                {
                    unconditionedRaise.Add(eventNode.EventName, eventNode);
                }
            }

            return hidden;
        }

        /// <summary>問題を付けるノード: 選ばれない Event はその Event（ふつうは 1 つのノードからしか来ない）、それ以外は並べ替えるノード（行き先はほかから来ることもある）。</summary>
        public static string AnchorOf(NodeData source, HiddenTransition hidden) =>
            hidden.Target is EventNode && !(source is EventNode) ? hidden.Target.Id : source.Id;

        // 入るとすぐに調べる自動の Event のうち、条件が無く、先へ進むもの（その前の自動の Event もすべて先へ進むときだけ。
        // 先の無い自動の Event は通知だけしてノードに留まるので、条件を満たすとノードを出ないことがある）
        private static EventNode FindLeavingEvent(
            IEnumerable<(EdgeData Edge, NodeData Target)> targets, Func<string, IEnumerable<EdgeData>> outgoingOf, Func<string, NodeData> findNode)
        {
            foreach (var (_, target) in targets)
            {
                if (!(target is EventNode eventNode) || !eventNode.IsAutomatic)
                {
                    continue;
                }

                var leadsOn = (outgoingOf(eventNode.Id) ?? Array.Empty<EdgeData>())
                    .Any(e => e != null && e.FromNodeId == eventNode.Id && findNode(e.ToNodeId ?? string.Empty) != null);
                if (!leadsOn)
                {
                    return null;
                }

                if (eventNode.Conditions.Count == 0)
                {
                    return eventNode;
                }
            }

            return null;
        }

        /// <summary>選ばれない理由と直し方（検証のメッセージ・Transitions のツールチップ）。</summary>
        public static string Describe(NodeData source, HiddenTransition hidden)
        {
            var from = LabelOf(source);
            var target = LabelOf(hidden.Target);
            var instead = LabelOf(hidden.ChosenInstead);
            switch (hidden.Reason)
            {
                case HiddenTransitionReason.AutomaticAfterUnconditioned:
                    return $"'{target}' never fires from '{from}': the automatic Event '{instead}' before it has no conditions. " +
                           $"Put the one without conditions last in the Transitions list of '{from}'.";
                case HiddenTransitionReason.RaiseAfterUnconditioned:
                    return $"'{target}' never fires from '{from}': the Event '{instead}' before it has the same name and no conditions, so it is always taken first. " +
                           $"Put the one without conditions last in the Transitions list of '{from}'.";
                case HiddenTransitionReason.NotFirstNext:
                    return $"'{target}' is never reached from '{from}': it only goes on to the first connected node that is not an Event, '{instead}'. " +
                           $"Put an Event in between, or reorder them in the Transitions list of '{from}'.";
                case HiddenTransitionReason.LeavesRightAway:
                    return $"'{target}' is never taken from '{from}': the automatic Event '{instead}' has no conditions, so the flow leaves '{from}' as soon as it gets there. " +
                           $"Give '{instead}' a condition if the flow should wait at '{from}'.";
                case HiddenTransitionReason.EntryGoesOn:
                    return $"'{target}' never fires from '{from}': an Entry goes straight on to its first connected node that is not an Event, '{instead}', so it never waits for an Event. " +
                           $"Connect '{target}' from '{instead}' instead.";
                default:
                    return $"'{from}' only goes on to '{instead}': an Event follows its first edge, so the edge to '{target}' is never used. " +
                           $"Remove it, or reorder them in the Transitions list of '{from}'.";
            }
        }

        // 文章の中でノードを呼ぶ名前（エディタの NodeDisplay.GetNodeLabel に近い規則。型の表示名の代わりに既定のタイトル）
        internal static string LabelOf(NodeData node)
        {
            if (node == null)
            {
                return string.Empty;
            }

            if (node.HasCustomTitle)
            {
                return node.Title.Trim();
            }

            switch (node)
            {
                case EventNode eventNode when !string.IsNullOrWhiteSpace(eventNode.EventName):
                    return eventNode.EventName;
                case SceneNode scene when !string.IsNullOrEmpty(scene.SceneName):
                    return scene.SceneName;
                case { ContentTitle: { } contentTitle } when !string.IsNullOrWhiteSpace(contentTitle):
                    return contentTitle.Trim();
                default:
                    return node.Title;
            }
        }
    }
}
