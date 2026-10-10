using System;
using UnityEditor;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>MCP のツールのテスト用に、グラフをアセットとして一時フォルダに保存する（ツールはアセットのパスでグラフを指すため）。</summary>
    internal sealed class McpTempGraphs : IDisposable
    {
        public const string Folder = "Assets/__VneMcpTestTemp";

        public McpTempGraphs()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets", "__VneMcpTestTemp");
            }
        }

        /// <summary><paramref name="graph"/> を <c>Folder/name.asset</c> に保存し、そのパスを返す。</summary>
        public string Save(NodeGraphAsset graph, string name)
        {
            var path = $"{Folder}/{name}.asset";
            AssetDatabase.CreateAsset(graph, path);
            AssetDatabase.SaveAssets();
            return path;
        }

        public void Dispose() => AssetDatabase.DeleteAsset(Folder);
    }
}
