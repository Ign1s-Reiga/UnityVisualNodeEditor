using UnityEditor;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Editor
{
    /// <summary>
    /// 新しいグラフアセットを作る。最初から Entry を置いておき、作った直後に「Entry が無い」Error で止まらないようにする
    /// （Unity の State Graph の Start ステートと同じ考え方）。
    /// </summary>
    public static class NodeGraphFactory
    {
        /// <summary>新しいグラフのファイル名の初期値。</summary>
        public const string DefaultFileName = "NewNodeGraph.asset";

        /// <summary>Entry だけがある新しいグラフ（まだアセットとして保存していない）。</summary>
        public static NodeGraphAsset CreateNew()
        {
            var graph = ScriptableObject.CreateInstance<NodeGraphAsset>();
            graph.AddNode(new EntryNode { Position = Vector2.zero });
            return graph;
        }

        // Project ビューで名前を入力させてから保存する（Create メニューの他のアセットと同じ操作感）
        [MenuItem("Assets/Create/Visual Node Editor/Node Graph", priority = 80)]
        private static void CreateGraphAsset() => ProjectWindowUtil.CreateAsset(CreateNew(), DefaultFileName);
    }
}
