using System.Collections.Generic;
using UnityEngine;

namespace Reiga.VisualNodeEditor
{
    /// <summary>
    /// 遊びの出来事をグラフへ伝える共通の処理（Graph Destroy Trigger・Graph Trigger 2D が使う）: パラメータを変え（上から順に）、その後でイベントを Raise する。
    /// 先に変えるので、<c>Lives -= 1</c> の後に <c>Died</c> を起こせば、<c>Lives &lt;= 0</c> の条件で Game Over へ分けられる。
    /// </summary>
    public static class GraphReactions
    {
        /// <summary>
        /// <paramref name="graph"/> を実行中の Runner（null なら実行中のどれか）で、<paramref name="changes"/> を行い、<paramref name="eventName"/>（空なら起こさない）を Raise する。
        /// Runner が無ければ何もせず false。変更はパラメータを ID で指すので、グラフが null のときは行わず、警告を出す（<paramref name="context"/> ごとに 1 度）。
        /// できない変更（パラメータが無いなど）は飛ばして警告を出す。Raise して遷移しなくても何も言わない。Runner があれば true。
        /// </summary>
        public static bool Run(NodeGraphAsset graph, string eventName, IReadOnlyList<ParameterChange> changes, Object context)
        {
            var runner = GraphRunner.FindRunning(graph);
            if (runner == null)
            {
                return false;
            }

            if (changes != null && changes.Count > 0)
            {
                if (graph == null)
                {
                    WarnOnce(context, $"[VisualNodeEditor] '{(context != null ? context.name : "?")}' has parameter changes but no graph. Set the graph to change its parameters.");
                }
                else
                {
                    foreach (var change in changes)
                    {
                        var problem = change?.ApplyTo(runner);
                        if (problem != null)
                        {
                            Debug.LogWarning($"[VisualNodeEditor] '{(context != null ? context.name : "?")}' skipped a parameter change because {problem}.", context);
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(eventName))
            {
                runner.Raise(eventName);
            }

            return true;
        }

        private static readonly HashSet<Object> _warned = new HashSet<Object>();

        private static void WarnOnce(Object context, string message)
        {
            if (_warned.Add(context))
            {
                Debug.LogWarning(message, context);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetWarnings() => _warned.Clear();
    }
}
