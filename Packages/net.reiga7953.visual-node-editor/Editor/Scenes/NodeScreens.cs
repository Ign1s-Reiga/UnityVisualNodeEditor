using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Editor.Scenes
{
    /// <summary>
    /// ノードの画面（<see cref="IHasScreen.Screen"/> の UI Toolkit の UXML）を作る・開く（ノードのインスペクタの Create Screen… / Edit Screen）。
    /// 画面の配置は UI Builder で行い、実行中は Graph Runner の <see cref="GraphScreenHost"/> が出す。
    /// </summary>
    public static class NodeScreens
    {
        /// <summary>
        /// <paramref name="node"/> の新しい画面を、グラフの隣に <c>&lt;グラフ名&gt; &lt;ノードのタイトル&gt;.uxml</c>（見出しだけ。同じ名前があれば番号を付ける）
        /// で作ってパスを返す。ノードには割り当てない（インスペクタが Undo できるように割り当てる）。
        /// グラフがアセットとして保存されていなければ null と理由（<paramref name="problem"/>）。
        /// </summary>
        public static string CreateScreen(NodeGraphAsset graph, NodeData node, out string problem)
        {
            problem = null;
            var graphPath = graph != null ? AssetDatabase.GetAssetPath(graph) : null;
            if (string.IsNullOrEmpty(graphPath))
            {
                problem = "the graph is not saved as an asset";
                return null;
            }

            if (node == null)
            {
                problem = "the node is no longer in the graph";
                return null;
            }

            var folder = Path.GetDirectoryName(graphPath)?.Replace('\\', '/') ?? "Assets";
            var uxmlPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{GetFileName(graph, node)}.uxml");
            File.WriteAllText(uxmlPath, BuildDefaultScreen(node.Title));
            AssetDatabase.ImportAsset(uxmlPath);
            return uxmlPath;
        }

        /// <summary>新しい画面のファイル名（拡張子なし）: <c>&lt;グラフ名&gt; &lt;ノードのタイトル&gt;</c>。ファイル名に使えない文字は <c>_</c> にする。</summary>
        public static string GetFileName(NodeGraphAsset graph, NodeData node)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var name = $"{(graph != null ? graph.name : "Graph")} {node?.Title}".Trim();
            return new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        }

        /// <summary>画面を開く（Unity の既定では UI Builder で開く）。</summary>
        public static void Open(VisualTreeAsset screen)
        {
            if (screen != null)
            {
                AssetDatabase.OpenAsset(screen);
            }
        }

        /// <summary>
        /// 新しい画面の UXML（中央に見出しだけ）。グラフに繋がるコントロールは UI Builder の Library から置く。
        /// 画面いっぱいの枠はクリックを受けない（下に重なった画面のボタンを押せるように。下をふさぎたければ、画面いっぱいの要素を中に置く）。
        /// </summary>
        public static string BuildDefaultScreen(string heading) =>
            "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\" xmlns:vne=\"Reiga.VisualNodeEditor\" editor-extension-mode=\"False\">\n" +
            "    <ui:VisualElement name=\"screen\" picking-mode=\"Ignore\" style=\"flex-grow: 1; align-items: center; justify-content: center;\">\n" +
            $"        <ui:Label name=\"heading\" text=\"{System.Security.SecurityElement.Escape(heading)}\" style=\"font-size: 72px; color: rgb(255, 255, 255);\" />\n" +
            "    </ui:VisualElement>\n" +
            "</ui:UXML>\n";
    }
}
