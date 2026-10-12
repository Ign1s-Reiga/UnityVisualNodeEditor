using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Editor.Inspector
{
    /// <summary>
    /// <see cref="GraphNodePathAttribute"/> の付いた文字列フィールドを、同じ並びの <c>graph</c> のグラフのノードの場所（<see cref="GraphNodePath"/>）から選べる欄にする
    /// （UI Builder の Graph Node Panel の <c>node</c> など）。選べるのは Graph Node Activator と同じく待機ノードとコンテナ。グラフが無ければ入力欄。
    /// </summary>
    [CustomPropertyDrawer(typeof(GraphNodePathAttribute))]
    public sealed class GraphNodePathDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                return new Label($"[GraphNodePath] only works on string fields ({property.displayName}).");
            }

            var label = string.IsNullOrEmpty(preferredLabel) ? property.displayName : preferredLabel;
            return new ChoiceTextField(property, label, () => GetChoices(ChoiceTextField.FindSiblingGraph(property)),
                "Set the graph to pick one of its nodes");
        }

        /// <summary><paramref name="graph"/> の待機ノードとコンテナの場所（例: <c>Round/Paused</c>。並べ替えて重なり無し）。グラフが無ければ空。</summary>
        public static List<string> GetChoices(NodeGraphAsset graph)
        {
            if (graph == null)
            {
                return new List<string>();
            }

            var query = new GraphQuery(graph);
            return graph.Nodes.Where(ActivatorNodeChoices.IsChoosable)
                .Select(node => GraphNodePath.Of(query, node))
                .Distinct()
                .OrderBy(path => path, System.StringComparer.Ordinal)
                .ToList();
        }
    }
}
