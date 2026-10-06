using System;
using System.Linq;
using UnityEditor;

namespace Reiga.VisualNodeEditor.Editor.Scenes
{
    /// <summary>
    /// シーンが移動・改名・（Git などで）再インポートされたら、全グラフの <see cref="SceneReference"/> の Path を更新して保存する
    /// （未保存の変更があるグラフは保存せず、未保存状態にするだけにする）。
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
                if (SyncGraph(graph, AssetDatabase.GUIDToAssetPath, out var shouldSave) && shouldSave)
                {
                    AssetDatabase.SaveAssetIfDirty(graph);
                }
            }
        }

        /// <summary>
        /// グラフの参照を更新し、変更があれば未保存状態にする。変更があれば true。
        /// もともと未保存の変更があったグラフ（エディタで編集中など）は、ユーザーの編集まで勝手に書き込まないよう
        /// <paramref name="shouldSave"/> を false にする（保存はユーザーに任せる）。
        /// </summary>
        internal static bool SyncGraph(NodeGraphAsset graph, Func<string, string> guidToPath, out bool shouldSave)
        {
            shouldSave = false;
            if (graph == null)
            {
                return false;
            }

            var hadUnsavedChanges = EditorUtility.IsDirty(graph);
            if (!SceneReferenceSync.Resync(graph, guidToPath))
            {
                return false;
            }

            EditorUtility.SetDirty(graph);
            shouldSave = !hadUnsavedChanges;
            return true;
        }

        private static bool IsScene(string path) => path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase);
    }
}
