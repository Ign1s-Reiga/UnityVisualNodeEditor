using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor
{
    /// <summary>グラフの今の場所で出す画面（<see cref="IHasScreen"/>）の並び。<see cref="GraphScreenHost"/> が使う。</summary>
    public static class GraphScreens
    {
        /// <summary>
        /// <paramref name="runner"/> の今の場所で出す画面を、下から上の順に返す: 入っているコンテナの画面を外側から、最後に今のノードの画面。
        /// 画面の無い所は飛ばし、同じ画面は 1 度だけ（下のものを残す）。Runner が無い・止まっていれば空。
        /// </summary>
        public static List<VisualTreeAsset> At(GraphRunner runner)
        {
            var screens = new List<VisualTreeAsset>();
            var current = runner != null && runner.IsRunning ? runner.Current : null;
            if (current == null)
            {
                return screens;
            }

            foreach (var container in runner.ContainerPath)
            {
                Add(screens, container);
            }

            Add(screens, current);
            return screens;
        }

        /// <summary><paramref name="graph"/> に、画面を割り当てたノードがあるか。</summary>
        public static bool AnyIn(NodeGraphAsset graph)
        {
            if (graph == null)
            {
                return false;
            }

            foreach (var node in graph.Nodes)
            {
                // Unity の == で比べる（消えたアセットへの参照も「無し」にする）
                if (node is IHasScreen hasScreen && hasScreen.Screen != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static void Add(List<VisualTreeAsset> screens, NodeData node)
        {
            if (node is IHasScreen hasScreen && hasScreen.Screen != null && !screens.Contains(hasScreen.Screen))
            {
                screens.Add(hasScreen.Screen);
            }
        }
    }
}
