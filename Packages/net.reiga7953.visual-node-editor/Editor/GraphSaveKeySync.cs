using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Editor
{
    /// <summary>
    /// グラフのセーブキー（<see cref="NodeGraphAsset.SaveKey"/>）が、別のグラフと重ならないようにする。
    /// 新しいグラフは作るときにキーを持つ（<see cref="NodeGraphFactory.CreateNew"/>）。アセットを複製すると同じキーを持つので、
    /// 読み込まれたグラフのキーが別のグラフと同じなら、そのアセットの GUID に入れ直して保存する
    /// （ランダムな ID にすると、保存しないバッチモードのビルドでビルドのたびにキーが変わり、プレイヤーのセーブが見えなくなる）。
    /// どのグラフがキーを残すかは、読み込まれていないグラフ → アセットの GUID と同じキーを持つグラフ（0.13.1 で入れたキー）→
    /// ファイル名の自然な順（Unity の複製は "Main 1"・"Stage 10" のように番号を付けるので、元のグラフが先）→ パスの順。
    /// キーを持たないグラフ（0.13.0 までに作ったもの）は書き換えない（クローン・再インポート・CI でアセットが変わらないように）。
    /// 未保存の変更があるグラフ（エディタで編集中など）は、ユーザーの編集まで勝手に書き込まないよう保存せず、未保存状態にするだけにする。
    /// エディタを開いたときに 1 回、全部のグラフで重なりを確かめる（保存しなかった複製や、重なったままコミットされたグラフが残らないように）。
    /// バッチモード（CI など）では、その確かめをせず、読み込みで入れ直したキーも保存しない（アセットを書き換えないように）。
    /// </summary>
    internal sealed class GraphSaveKeySync : AssetPostprocessor
    {
        // エディタを開いてから全部のグラフを確かめたか（SessionState はスクリプトの再読み込みでは消えず、エディタを閉じると消える）
        private const string CheckedKey = "Reiga.VisualNodeEditor.GraphSaveKeySync.Checked";

        // グラフのパス → キー（キーを持つグラフだけ）。最初に要るときに全部のグラフから作り、あとは読み込み・削除・移動で直す
        // （グラフを保存するたびに、全部のグラフを読み込まないように）
        private static Dictionary<string, string> _keys;

        // 重なったキーを入れ直さない間（WithoutFixing の中）の深さ
        private static int _suspended;

        /// <summary>新しいセーブキー。</summary>
        internal static string NewKey() => Guid.NewGuid().ToString("N");

        /// <summary>全部のグラフで、キーの重なりを直す。<see cref="WithoutFixing"/> の中なら何もせず false。</summary>
        internal static bool CheckAll()
        {
            if (_suspended > 0)
            {
                return false;
            }

            _keys = null;
            Resolve(FindAllGraphs(), true);
            return true;
        }

        /// <summary><paramref name="action"/> の間だけ、重なったキーを入れ直さない（テストで、重なったキーをディスクに作るときに使う）。</summary>
        internal static void WithoutFixing(Action action)
        {
            _suspended++;
            try
            {
                action();
            }
            finally
            {
                _suspended--;
            }
        }

        // エディタを開いたときに 1 回だけ確かめる（スクリプトの再読み込み・Play に入るたびに全部のグラフを読み込まないように）。
        // バッチモード（CI など）では、アセットを書き換えないよう確かめない
        [InitializeOnLoadMethod]
        private static void CheckOncePerSession()
        {
            if (Application.isBatchMode || SessionState.GetBool(CheckedKey, false))
            {
                return;
            }

            // 印は確かめてから付ける（その前にスクリプトの再読み込みが起きると delayCall が消えるので、次の読み込みでもう一度頼む）。
            // 確かめるのに失敗しても付ける（失敗するたびに、再読み込みのたびに全部のグラフを読み直さないように）。
            // WithoutFixing の中で何もしなかったときだけは付けない（次の再読み込みで確かめる）
            EditorApplication.delayCall += () =>
            {
                var skipped = false;
                try
                {
                    skipped = !CheckAll();
                }
                finally
                {
                    if (!skipped)
                    {
                        SessionState.SetBool(CheckedKey, true);
                    }
                }
            };
        }

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

            // バッチモード（CI など）では、重なったキーを入れ直しても保存しない（アセットを書き換えないように）
            var imported = importedAssets.Where(IsGraph).ToList();
            if (imported.Count > 0)
            {
                Resolve(imported, !Application.isBatchMode);
            }
        }

        // paths のグラフのキーを、ほかのグラフと重ならないようにして、覚えているキーを直す。save なら入れ直したキーを保存する。
        // paths の中で重なっていれば、アセットの GUID と同じキーを持つグラフ（元のグラフ）→ ファイル名の自然な順 → パスの順に、先のものがキーを残す
        // （Unity の複製は "Main 1"・"Stage 9" の次の "Stage 10" のように番号を付けるので、自然な順なら元のグラフが先になる。
        // 文字の順では "Main 1.asset" が "Main.asset" より、"Stage 10" が "Stage 9" より先になる）
        private static void Resolve(IReadOnlyCollection<string> paths, bool save)
        {
            var keys = _keys ??= LoadKeys();
            var resolving = new HashSet<string>(paths, StringComparer.Ordinal);
            var taken = new HashSet<string>(keys.Where(pair => !resolving.Contains(pair.Key)).Select(pair => pair.Value), StringComparer.Ordinal);
            var graphs = paths
                .Select(path => (path, graph: AssetDatabase.LoadAssetAtPath<NodeGraphAsset>(path)))
                .Where(entry => entry.graph != null)
                .OrderBy(entry => entry.graph.SaveKey == AssetDatabase.AssetPathToGUID(entry.path) ? 0 : 1)
                .ThenBy(entry => Path.GetFileNameWithoutExtension(entry.path), Comparer<string>.Create(EditorUtility.NaturalCompare))
                .ThenBy(entry => entry.path, StringComparer.Ordinal)
                .ToList();
            foreach (var (path, graph) in graphs)
            {
                if (_suspended == 0 && MakeUnique(graph, taken, AssetDatabase.AssetPathToGUID(path), out var shouldSave) && shouldSave && save)
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
        /// 新しいキーは <paramref name="preferredKey"/>（アセットの GUID。どの環境でも同じキーになる）。空か使われていればランダムな ID。
        /// キーを持たないグラフは何もしない。もともと未保存の変更があったグラフなら <paramref name="shouldSave"/> を false にする（保存はユーザーに任せる）。
        /// </summary>
        internal static bool MakeUnique(NodeGraphAsset graph, ISet<string> taken, string preferredKey, out bool shouldSave)
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
            graph.SaveKey = !string.IsNullOrEmpty(preferredKey) && !taken.Contains(preferredKey) ? preferredKey : NewKey();
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
