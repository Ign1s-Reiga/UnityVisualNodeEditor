using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Reiga.VisualNodeEditor.Editor.Mcp
{
    /// <summary>MCP のツールが指すグラフ（アセットのパス）を探す・読む。</summary>
    public static class GraphAssets
    {
        /// <summary>プロジェクトのグラフのパス（パスの順）。</summary>
        public static List<string> FindPaths() =>
            AssetDatabase.FindAssets("t:" + nameof(NodeGraphAsset))
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !string.IsNullOrEmpty(path))
                .Distinct()
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();

        /// <summary><paramref name="path"/> のグラフ。無ければ、直し方を添えた <see cref="McpToolException"/>。</summary>
        public static NodeGraphAsset Load(string path)
        {
            var graph = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<NodeGraphAsset>(path);
            if (graph == null)
            {
                throw new McpToolException($"No Node Graph at '{path}'. Use list_graphs to see the graphs in this project " +
                                           "(paths look like 'Assets/Flows/Main.asset').");
            }

            return graph;
        }
    }
}
