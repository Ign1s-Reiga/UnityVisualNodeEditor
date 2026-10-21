using System;
using System.Collections.Generic;

namespace Reiga.VisualNodeEditor
{
    /// <summary>
    /// 同じノードから出ているエッジの順（<see cref="GraphRunner"/> が遷移を調べる順）で、前のエッジがあるために選ばれることの無いエッジを見つける。
    /// 検証とインスペクタの Transitions で同じ判定を使う。docs/01-architecture.md「条件付き遷移」の「検証」と対応する。
    /// </summary>
    internal static class TransitionOrder
    {
        /// <summary>
        /// <paramref name="source"/> から出ているエッジ <paramref name="outgoing"/>（アセット内の順）のうち、選ばれることの無いもの（エッジの順）。
        /// ほかのノードから出ているエッジ・行き先の無いエッジ・前と同じ行き先へのエッジは数えない（Runner と同じ）。
        /// コンテナ（出口ごとのエッジ）と Note は対象外で、常に空。
        /// </summary>
        public static List<HiddenTransition> FindHidden(NodeData source, IEnumerable<EdgeData> outgoing, Func<string, NodeData> findNode)
        {
            var hidden = new List<HiddenTransition>();
            if (source == null || source is ContainerNode || source is NoteNode || outgoing == null)
            {
                return hidden;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);

            // Event なら最初の行き先、それ以外なら Event 以外の最初の行き先（Advance・Entry の行き先）
            NodeData firstNext = null;
            EventNode unconditionedAutomatic = null;
            var unconditionedRaise = new Dictionary<string, EventNode>(StringComparer.Ordinal);
            foreach (var edge in outgoing)
            {
                if (edge == null || edge.FromNodeId != source.Id)
                {
                    continue;
                }

                var target = findNode(edge.ToNodeId ?? string.Empty);
                if (target == null || !seen.Add(target.Id))
                {
                    continue;
                }

                if (source is EventNode || !(target is EventNode eventNode))
                {
                    if (firstNext == null)
                    {
                        firstNext = target;
                    }
                    else
                    {
                        var reason = source is EventNode ? HiddenTransitionReason.EventFollowsFirstEdge : HiddenTransitionReason.NotFirstNext;
                        hidden.Add(new HiddenTransition(edge, target, firstNext, reason));
                    }

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

                if (unconditionedRaise.TryGetValue(eventNode.EventName, out var unconditioned))
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
