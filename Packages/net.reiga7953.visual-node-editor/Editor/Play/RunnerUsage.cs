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

        // シーンのパス → (更新日時, そのシーンの Graph Runner が指すグラフの GUID。バイナリなどで読めなければ null)
        private static readonly Dictionary<string, (DateTime Stamp, HashSet<string> GraphGuids)> _cache = new();

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

            foreach (var scene in EditorBuildSettings.scenes.Where(s => s != null && s.enabled))
            {
                var guids = GetCachedRunnerGraphGuids(scene.path);
                if (guids == null || guids.Contains(graphGuid))
                {
                    return true;
                }
            }

            return false;
        }

        private static HashSet<string> GetCachedRunnerGraphGuids(string scenePath)
        {
            if (string.IsNullOrEmpty(scenePath) || !File.Exists(scenePath))
            {
                // 削除されたシーンは Build Settings の警告で知らせるので、ここでは「Runner は無い」とだけ扱う
                return new HashSet<string>();
            }

            var stamp = File.GetLastWriteTimeUtc(scenePath);
            if (_cache.TryGetValue(scenePath, out var cached) && cached.Stamp == stamp)
            {
                return cached.GraphGuids;
            }

            var guids = GetRunnerGraphGuids(File.ReadAllText(scenePath), RunnerScriptGuid);
            _cache[scenePath] = (stamp, guids);
            return guids;
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
