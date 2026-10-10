using System.Collections.Generic;
using System.Linq;
using Reiga.VisualNodeEditor.Editor.Views;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Editor.Mcp
{
    /// <summary>
    /// ノードのポート（ID と表示名）。ポートはノードの View が決めるので、View を作って調べる（エディタと同じ結果になるように）。
    /// コンテナの出力ポートの ID は出口の ID、表示名は出口の名前。
    /// </summary>
    public static class NodePorts
    {
        /// <summary>入力ポートと出力ポートの (ID, 表示名)。View は 1 度だけ作る。</summary>
        public static (List<(string Id, string Label)> Inputs, List<(string Id, string Label)> Outputs) Get(NodeGraphAsset asset, NodeData node)
        {
            var view = NodeViewFactory.Create(node, asset);
            return (Read(view.inputContainer), Read(view.outputContainer));
        }

        /// <summary>入力ポートの (ID, 表示名)。</summary>
        public static List<(string Id, string Label)> GetInputs(NodeGraphAsset asset, NodeData node) => Get(asset, node).Inputs;

        /// <summary>出力ポートの (ID, 表示名)。</summary>
        public static List<(string Id, string Label)> GetOutputs(NodeGraphAsset asset, NodeData node) => Get(asset, node).Outputs;

        private static List<(string Id, string Label)> Read(VisualElement container) =>
            container.Query<Port>().ToList()
                .Select(port => (NodeView.GetPortId(port), port.portName))
                .ToList();
    }
}
