using System;
using UnityEditor;

namespace Reiga.VisualNodeEditor.Editor
{
    /// <summary>
    /// グラフのセーブキー（<see cref="NodeGraphAsset.SaveKey"/>）をアセットの GUID にそろえる。
    /// アセットを作った・複製した（複製は元のキーを持ったままなので）・読み込み直したときに、GUID と違えば入れ直して保存する。
    /// 未保存の変更があるグラフ（エディタで編集中など）は、ユーザーの編集まで勝手に書き込まないよう保存せず、未保存状態にするだけにする。
    /// </summary>
    internal sealed class GraphSaveKeySync : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            foreach (var path in importedAssets)
            {
                if (!path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) || AssetDatabase.GetMainAssetTypeAtPath(path) != typeof(NodeGraphAsset))
                {
                    continue;
                }

                var graph = AssetDatabase.LoadAssetAtPath<NodeGraphAsset>(path);
                if (Sync(graph, AssetDatabase.AssetPathToGUID(path), out var shouldSave) && shouldSave)
                {
                    AssetDatabase.SaveAssetIfDirty(graph);
                }
            }
        }

        /// <summary>
        /// <paramref name="graph"/> のセーブキーを <paramref name="guid"/> にする。変えたら true。
        /// もともと未保存の変更があったグラフなら <paramref name="shouldSave"/> を false にする（保存はユーザーに任せる）。
        /// </summary>
        internal static bool Sync(NodeGraphAsset graph, string guid, out bool shouldSave)
        {
            shouldSave = false;
            if (graph == null || string.IsNullOrEmpty(guid) || graph.SaveKey == guid)
            {
                return false;
            }

            shouldSave = !EditorUtility.IsDirty(graph);
            graph.SaveKey = guid;
            EditorUtility.SetDirty(graph);
            return true;
        }
    }
}
