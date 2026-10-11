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
    /// どのグラフがキーを残すかは、読み込まれていないグラフ → アセットの GUID と同じキーを持つグラフ（0.13.1 で入れたキー）→ パスの順。
    /// キーを持たないグラフ（0.13.0 までに作ったもの）は書き換えない（クローン・再インポート・CI でアセットが変わらないように）。
    /// 未保存の変更があるグラフ（エディタで編集中など）は、ユーザーの編集まで勝手に書き込まないよう保存せず、未保存状態にするだけにする。
    /// スクリプトの再読み込みの後に 1 回、全部のグラフで重なりを確かめる（保存しなかった複製や、重なったままコミットされたグラフが残らないように）。
    /// </summary>
    internal sealed class GraphSaveKeySync : AssetPostprocessor
    {
        // グラフのパス → キー（キーを持つグラフだけ）。最初に要るときに全部のグラフから作り、あとは読み込み・削除・移動で直す
        // （グラフを保存するたびに、全部のグラフを読み込まないように）
        private static Dictionary<string, string> _keys;

        /// <summary>重なったキーを入れ直すか（テストで、重なったキーをディスクに作るときだけ止める）。</summary>
        internal static bool Enabled = true;

        /// <summary>新しいセーブキー。</summary>
        internal static string NewKey() => Guid.NewGuid().ToString("N");

        /// <summary>全部のグラフで、キーの重なりを直す。</summary>
        internal static void CheckAll()
        {
            if (!Enabled)
            {
                return;
            }

            _keys = null;
            Resolve(FindAllGraphs());
        }

        [InitializeOnLoadMethod]
        private static void CheckAfterReload() => EditorApplication.delayCall += CheckAll;

        private static void OnPostprocessAllAssets(
            string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (_keys != null)
            {
                foreach (var path in deletedAssets)
                {
                    _keys.Remove(path);
                }

                for (var i = 0; i < movedAssets.Length; i++)
                {
                    if (_keys.Remove(movedFromAssetPaths[i], out var key))
                    {
                        _keys[movedAssets[i]] = key;
                    }
                }
            }

            var imported = importedAssets.Where(IsGraph).ToList();
            if (imported.Count > 0)
            {
                Resolve(imported);
            }
        }

        // paths のグラフのキーを、ほかのグラフと重ならないようにして、覚えているキーを直す。
        // paths の中で重なっていれば、アセットの GUID と同じキーを持つグラフ（元のグラフ）→ パスの順に、先のものがキーを残す
        private static void Resolve(IReadOnlyCollection<string> paths)
        {
            var keys = _keys ??= LoadKeys();
            var resolving = new HashSet<string>(paths, StringComparer.Ordinal);
            var taken = new HashSet<string>(keys.Where(pair => !resolving.Contains(pair.Key)).Select(pair => pair.Value), StringComparer.Ordinal);
            var graphs = paths
                .Select(path => (path, graph: AssetDatabase.LoadAssetAtPath<NodeGraphAsset>(path)))
                .Where(entry => entry.graph != null)
                .OrderBy(entry => entry.graph.SaveKey == AssetDatabase.AssetPathToGUID(entry.path) ? 0 : 1)
                .ThenBy(entry => entry.path, StringComparer.Ordinal)
                .ToList();
            foreach (var (path, graph) in graphs)
            {
                if (Enabled && MakeUnique(graph, taken, out var shouldSave) && shouldSave)
                {
                    AssetDatabase.SaveAssetIfDirty(graph);
                }

                if (graph.SaveKey.Length > 0)
                {
                    keys[path] = graph.SaveKey;
                }
                else
                {
                    keys.Remove(path);
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

        private static Dictionary<string, string> LoadKeys()
        {
            var keys = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var path in FindAllGraphs())
            {
                var key = AssetDatabase.LoadAssetAtPath<NodeGraphAsset>(path)?.SaveKey;
                if (!string.IsNullOrEmpty(key))
                {
                    keys[path] = key;
                }
            }

            return keys;
        }

        private static List<string> FindAllGraphs() =>
            AssetDatabase.FindAssets("t:" + nameof(NodeGraphAsset))
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct(StringComparer.Ordinal)
                .ToList();

        private static bool IsGraph(string path) =>
            path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) && AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(NodeGraphAsset);
    }
}
