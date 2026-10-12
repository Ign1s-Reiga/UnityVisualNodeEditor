using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Editor.Inspector
{
    /// <summary>
    /// <see cref="GraphParameterNameAttribute"/> の付いた文字列フィールドを、同じ並びの <c>graph</c> のグラフのパラメータ名から選べる欄にする
    /// （UI Builder の Graph Parameter Label の <c>parameter</c> など）。グラフが無ければ入力欄。
    /// </summary>
    [CustomPropertyDrawer(typeof(GraphParameterNameAttribute))]
    public sealed class GraphParameterNameDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                return new Label($"[GraphParameterName] only works on string fields ({property.displayName}).");
            }

            var label = string.IsNullOrEmpty(preferredLabel) ? property.displayName : preferredLabel;
            return new ChoiceTextField(property, label, () => GetChoices(ChoiceTextField.FindSiblingGraph(property)),
                "Set the graph to pick one of its parameters");
        }

        /// <summary><paramref name="graph"/> のパラメータ名（空を除き、グラフの中の順で重なり無し）。グラフが無ければ空。</summary>
        public static List<string> GetChoices(NodeGraphAsset graph) =>
            graph == null
                ? new List<string>()
                : graph.Parameters.Where(p => p != null && !string.IsNullOrEmpty(p.Name)).Select(p => p.Name).Distinct().ToList();
    }
}
