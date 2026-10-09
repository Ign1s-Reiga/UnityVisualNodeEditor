using System;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Editor.Views
{
    /// <summary>
    /// ポートから空き地へエッジを落としたとき、検索に出すノード（ドラッグ元と繋げるもの）を決める。
    /// 型がどのポートを持つかは、その型の View を 1 度作って調べる（ポートは View が決めるため）。
    /// </summary>
    public static class ConnectionCandidates
    {
        private static readonly Dictionary<Type, (bool HasInput, bool HasOutput)> _ports = new();

        /// <summary>
        /// <paramref name="draggedDirection"/> のポートから、<paramref name="nodeType"/> の新しいノードへ繋げるか
        /// （出力からなら入力ポートが、入力からなら出力ポートが要る）。
        /// </summary>
        public static bool CanConnect(Type nodeType, Direction draggedDirection)
        {
            var (hasInput, hasOutput) = GetPorts(nodeType);
            return draggedDirection == Direction.Output ? hasInput : hasOutput;
        }

        /// <summary>待機ノード（State・Scene）の出力からなら Event を先頭に出す。それ以外は null。</summary>
        public static Type GetFeaturedType(PendingConnection pending) =>
            pending != null && pending.FromWaitNode && pending.Direction == Direction.Output ? typeof(EventNode) : null;

        /// <summary>出力ポートから次へ「待つ」ノード（ここから Event で遷移する）か。</summary>
        public static bool IsWaitNode(NodeData node) => node is StateNode || node is SceneNode;

        private static (bool HasInput, bool HasOutput) GetPorts(Type nodeType)
        {
            if (nodeType == null || !typeof(NodeData).IsAssignableFrom(nodeType) || nodeType.IsAbstract)
            {
                return (false, false);
            }

            if (!_ports.TryGetValue(nodeType, out var ports))
            {
                var view = NodeViewFactory.Create((NodeData)Activator.CreateInstance(nodeType));
                ports = (view.inputContainer.Q<Port>() != null, view.outputContainer.Q<Port>() != null);
                _ports[nodeType] = ports;
            }

            return ports;
        }
    }
}
