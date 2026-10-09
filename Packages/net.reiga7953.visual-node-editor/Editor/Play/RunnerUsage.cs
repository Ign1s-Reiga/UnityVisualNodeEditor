using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;

namespace Reiga.VisualNodeEditor.Editor.Play
{
    /// <summary>
    /// Build Settings のシーンに、グラフを動かす Graph Runner があるかを調べる（「Runner が無いので Play しても何も起きない」を先に知らせるため）。
    /// シーンファイル（テキストの YAML）を読み、Graph Runner の各コンポーネントが指すグラフの GUID を拾う。
    /// 読んだ結果はファイルの更新日時ごとに覚え、編集のたびの再検証でもシーンを読み直さない。
    /// </summary>
    public static class RunnerUsage
    {
        private const string ComponentSeparator = "--- !u!";
        private const string GraphFieldPrefix = "_graph: {fileID: 11400000, guid: ";

        // シーンのパス → (更新日時, そのシーンの Graph Runner が指すグラフの GUID（バイナリなどで読めなければ null）, プレハブのインスタンスの情報)。
        // プレハブの中の Runner はシーンファイルに書き出されないので、インスタンスの元のプレハブと、インスタンスで上書きした参照を別に持つ
        private static readonly Dictionary<string, (DateTime Stamp, HashSet<string> GraphGuids, PrefabReferences Prefabs)> _cache = new();

        private const string PrefabInstanceType = "1001 ";
        private const string SourcePrefabPrefix = "m_SourcePrefab: {fileID: 100100000, guid: ";
        private const string ObjectReferencePrefix = "objectReference: {fileID: 11400000, guid: ";

        // 元のプレハブの GUID → そのプレハブが（入れ子までたどって）依存するアセットのパス
        private static readonly Dictionary<string, HashSet<string>> _prefabDependencies = new();
        private static bool _listeningToProjectChanges;

        private static string _runnerScriptGuid;

        /// <summary>
        /// シーンの YAML から、<paramref name="runnerScriptGuid"/> のスクリプト（Graph Runner）のコンポーネントが指すグラフの GUID を集める。
        /// テキストの YAML でなければ null（読めないので「分からない」）。
        /// </summary>
        public static HashSet<string> GetRunnerGraphGuids(string sceneText, string runnerScriptGuid)
        {
            if (sceneText == null || !sceneText.StartsWith("%YAML", StringComparison.Ordinal))
            {
                return null;
            }

            var guids = new HashSet<string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(runnerScriptGuid))
            {
                return guids;
            }

            var scriptReference = "guid: " + runnerScriptGuid + ",";
            foreach (var component in sceneText.Split(new[] { ComponentSeparator }, StringSplitOptions.None))
            {
                if (!component.Contains(scriptReference))
                {
                    continue;
                }

                var start = component.IndexOf(GraphFieldPrefix, StringComparison.Ordinal);
                if (start < 0)
                {
                    continue;
                }

                start += GraphFieldPrefix.Length;
                var end = component.IndexOf(',', start);
                if (end > start)
                {
                    guids.Add(component.Substring(start, end - start).Trim());
                }
            }

            return guids;
        }

        /// <summary>
        /// 有効な Build Settings のシーンのどれかに、このグラフの Graph Runner があるか。
        /// 読めないシーン（バイナリなど）があれば、分からないので true を返す（誤った警告を出さない）。
        /// グラフがアセットとして保存されていなければ true。
        /// </summary>
        public static bool IsUsedInBuildScenes(NodeGraphAsset graph)
        {
            var graphGuid = graph != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(graph)) : null;
            if (string.IsNullOrEmpty(graphGuid))
            {
                return true;
            }

            var graphPath = AssetDatabase.GUIDToAssetPath(graphGuid);
            foreach (var scene in EditorBuildSettings.scenes.Where(s => s != null && s.enabled))
            {
                var (guids, prefabs) = GetCachedSceneInfo(scene.path);
                if (guids == null || guids.Contains(graphGuid))
                {
                    return true;
                }

                // プレハブの中の Runner はシーンファイルに書き出されない。インスタンスでグラフを上書きしているか、
                // 元のプレハブがグラフを参照していれば、使われているとみなす（誤った警告を出さない側に倒す）。
                // シーンに直接置いた Graph Event Button の参照は数えない（Runner が無いことを見逃さないように）
                if (prefabs.OverriddenGuids.Contains(graphGuid)
                    || prefabs.SourcePrefabGuids.Any(prefab => PrefabUsesGraph(prefab, graphPath)))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// シーンの YAML の、プレハブのインスタンスの情報: 元のプレハブの GUID と、インスタンスで上書きしたアセット参照の GUID。
        /// プレハブの中のコンポーネントはシーンファイルに書き出されないので、Graph Runner を探すにはこれを見る。
        /// </summary>
        public static PrefabReferences GetPrefabReferences(string sceneText)
        {
            var references = new PrefabReferences();
            if (sceneText == null)
            {
                return references;
            }

            foreach (var block in sceneText.Split(new[] { ComponentSeparator }, StringSplitOptions.None))
            {
                if (!block.StartsWith(PrefabInstanceType, StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (var guid in FindGuids(block, SourcePrefabPrefix))
                {
                    references.SourcePrefabGuids.Add(guid);
                }

                foreach (var guid in FindGuids(block, ObjectReferencePrefix))
                {
                    references.OverriddenGuids.Add(guid);
                }
            }

            return references;
        }

        // prefix の直後から ',' までを GUID として拾う
        private static IEnumerable<string> FindGuids(string text, string prefix)
        {
            for (var start = text.IndexOf(prefix, StringComparison.Ordinal); start >= 0; start = text.IndexOf(prefix, start, StringComparison.Ordinal))
            {
                start += prefix.Length;
                var end = text.IndexOf(',', start);
                if (end <= start)
                {
                    yield break;
                }

                yield return text.Substring(start, end - start).Trim();
            }
        }

        // 元のプレハブ（入れ子のプレハブも含む）がグラフを参照しているか
        private static bool PrefabUsesGraph(string prefabGuid, string graphPath) =>
            GetPrefabDependencies(prefabGuid).Contains(graphPath);

        /// <summary>
        /// プレハブ（入れ子を含む）が依存するアセットのパス。見つからないプレハブなら空。
        /// 依存をすべてたどるのは重いので、プレハブごとに覚えておき、プロジェクトのアセットが変わったら捨てる。
        /// </summary>
        internal static HashSet<string> GetPrefabDependencies(string prefabGuid)
        {
            if (!_listeningToProjectChanges)
            {
                EditorApplication.projectChanged += ClearPrefabDependencies;
                _listeningToProjectChanges = true;
            }

            if (_prefabDependencies.TryGetValue(prefabGuid ?? string.Empty, out var cached))
            {
                return cached;
            }

            var prefabPath = string.IsNullOrEmpty(prefabGuid) ? null : AssetDatabase.GUIDToAssetPath(prefabGuid);
            var dependencies = string.IsNullOrEmpty(prefabPath)
                ? new HashSet<string>(StringComparer.Ordinal)
                : new HashSet<string>(AssetDatabase.GetDependencies(prefabPath, true), StringComparer.Ordinal);
            _prefabDependencies[prefabGuid ?? string.Empty] = dependencies;
            return dependencies;
        }

        /// <summary>覚えているプレハブの依存を捨てる（アセットが変わったとき）。</summary>
        internal static void ClearPrefabDependencies() => _prefabDependencies.Clear();

        private static (HashSet<string> GraphGuids, PrefabReferences Prefabs) GetCachedSceneInfo(string scenePath)
        {
            if (string.IsNullOrEmpty(scenePath) || !File.Exists(scenePath))
            {
                // 削除されたシーンは Build Settings の警告で知らせるので、ここでは「Runner は無い」とだけ扱う
                return (new HashSet<string>(), new PrefabReferences());
            }

            var stamp = File.GetLastWriteTimeUtc(scenePath);
            if (_cache.TryGetValue(scenePath, out var cached) && cached.Stamp == stamp)
            {
                return (cached.GraphGuids, cached.Prefabs);
            }

            var text = File.ReadAllText(scenePath);
            var guids = GetRunnerGraphGuids(text, RunnerScriptGuid);
            var prefabs = GetPrefabReferences(text);
            _cache[scenePath] = (stamp, guids, prefabs);
            return (guids, prefabs);
        }

        /// <summary>Graph Runner（<see cref="GraphRunnerBehaviour"/>）のスクリプトの GUID。</summary>
        internal static string RunnerScriptGuid
        {
            get
            {
                if (string.IsNullOrEmpty(_runnerScriptGuid))
                {
                    var script = MonoImporter.GetAllRuntimeMonoScripts()
                        .FirstOrDefault(s => s != null && s.GetClass() == typeof(GraphRunnerBehaviour));
                    _runnerScriptGuid = script != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(script)) : null;
                }

                return _runnerScriptGuid;
            }
        }
    }
}
