using System;
using System.Linq;
using UnityEditor;

namespace Reiga.VisualNodeEditor.Editor.Scenes
{
    /// <summary>
    /// シーンが移動・改名・（Git などで）再インポートされたら、全グラフの <see cref="SceneReference"/> の Path を更新して保存する。
    /// インスペクタでノードを開かなくても、ランタイムが古いシーン名を読まないようにするため。
    /// </summary>
    internal sealed class SceneReferencePostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (!importedAssets.Concat(movedAssets).Any(IsScene))
            {
                return;
            }

            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(NodeGraphAsset)))
            {
                var graph = AssetDatabase.LoadAssetAtPath<NodeGraphAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (SceneReferenceSync.Resync(graph, AssetDatabase.GUIDToAssetPath))
                {
                    EditorUtility.SetDirty(graph);
                    AssetDatabase.SaveAssetIfDirty(graph);
                }
            }
        }

        private static bool IsScene(string path) => path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase);
    }
}
