using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Reiga.VisualNodeEditor.Editor
{
    /// <summary>
    /// グラフのセーブキー（<see cref="NodeGraphAsset.SaveKey"/>）が、別のグラフと重ならないようにする。
    /// 新しいグラフは作るときにキーを持つ（<see cref="NodeGraphFactory.CreateNew"/>）。アセットを複製すると同じキーを持つので、
    /// 読み込まれたグラフのキーが別のグラフと同じなら、新しいキーに入れ直して保存する。
    /// キーを持たないグラフ（0.13.0 までに作ったもの）は書き換えない（クローン・再インポート・CI でアセットが変わらないように）。
    /// 未保存の変更があるグラフ（エディタで編集中など）は、ユーザーの編集まで勝手に書き込まないよう保存せず、未保存状態にするだけにする。
    /// </summary>
    internal sealed class GraphSaveKeySync : AssetPostprocessor
    {
        /// <summary>新しいセーブキー。</summary>
        internal static string NewKey() => Guid.NewGuid().ToString("N");

        private static void OnPostprocessAllAssets(
            string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            var imported = importedAssets.Where(IsGraph).ToList();
            if (imported.Count == 0)
            {
                return;
            }

            // 読み込まれたもの以外のグラフのキー。読み込まれたグラフは順に見て、先に見たもののキーとも比べる
            var importedSet = new HashSet<string>(imported, StringComparer.Ordinal);
            var taken = new HashSet<string>(AssetDatabase.FindAssets("t:" + nameof(NodeGraphAsset))
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !importedSet.Contains(path))
                .Select(path => AssetDatabase.LoadAssetAtPath<NodeGraphAsset>(path)?.SaveKey)
                .Where(key => !string.IsNullOrEmpty(key)), StringComparer.Ordinal);

            foreach (var path in imported)
            {
                var graph = AssetDatabase.LoadAssetAtPath<NodeGraphAsset>(path);
                if (MakeUnique(graph, taken, out var shouldSave) && shouldSave)
                {
                    AssetDatabase.SaveAssetIfDirty(graph);
                }
            }
        }

        /// <summary>
        /// <paramref name="graph"/> のキーが <paramref name="taken"/> にあれば、新しいキーに入れ直して true。どちらにしてもグラフのキーを <paramref name="taken"/> に足す。
        /// キーを持たないグラフは何もしない。もともと未保存の変更があったグラフなら <paramref name="shouldSave"/> を false にする（保存はユーザーに任せる）。
        /// </summary>
        internal static bool MakeUnique(NodeGraphAsset graph, ISet<string> taken, out bool shouldSave)
        {
            shouldSave = false;
            if (graph == null || graph.SaveKey.Length == 0)
            {
                return false;
            }

            if (taken.Add(graph.SaveKey))
            {
                return false;
            }

            shouldSave = !EditorUtility.IsDirty(graph);
            graph.SaveKey = NewKey();
            taken.Add(graph.SaveKey);
            EditorUtility.SetDirty(graph);
            return true;
        }

        private static bool IsGraph(string path) =>
            path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) && AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(NodeGraphAsset);
    }
}
