using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Editor.Build
{
    /// <summary>
    /// グラフが参照するシーンと Build Settings（<see cref="EditorBuildSettings.scenes"/>）を突き合わせる。
    /// 判定と新しい一覧の組み立ては純粋な関数で、<see cref="Apply"/> だけが実際に書き換える。
    /// </summary>
    public static class BuildSettingsSync
    {
        /// <summary>有効になっているシーンの GUID。</summary>
        public static HashSet<string> GetEnabledSceneGuids(IEnumerable<EditorBuildSettingsScene> buildScenes) =>
            new HashSet<string>(buildScenes.Where(s => s != null && s.enabled).Select(s => s.guid.ToString()));

        /// <summary>
        /// Build Settings に有効な状態で入っていないシーンを参照する Scene ノードの警告。
        /// 参照先のシーンが削除されている（<paramref name="guidToPath"/> が空を返す）ノードは、追加できないのでその旨を警告する。
        /// </summary>
        /// <param name="guidToPath">GUID からパスを返す関数。通常は <see cref="AssetDatabase.GUIDToAssetPath(string)"/>。</param>
        public static List<GraphIssue> GetIssues(
            NodeGraphAsset graph, ICollection<string> enabledSceneGuids, System.Func<string, string> guidToPath)
        {
            var issues = new List<GraphIssue>();
            if (graph == null)
            {
                return issues;
            }

            foreach (var node in graph.Nodes.OfType<SceneNode>())
            {
                var scene = node.Scene;
                if (string.IsNullOrEmpty(scene.Guid) || enabledSceneGuids.Contains(scene.Guid))
                {
                    continue;
                }

                var message = string.IsNullOrEmpty(guidToPath(scene.Guid))
                    ? $"'{node.Title}' uses scene '{scene.Name}', which no longer exists."
                    : $"'{node.Title}' uses scene '{scene.Name}', which is not enabled in Build Settings.";
                issues.Add(new GraphIssue(GraphIssueSeverity.Warning, message, node.Id));
            }

            return issues;
        }

        /// <summary><see cref="Apply"/> の結果のメッセージ。追加・有効化した数と、削除済みで飛ばした数を伝える。</summary>
        public static string GetResultMessage(string graphName, int addedCount, int enabledCount, int missingCount)
        {
            var missing = missingCount > 0 ? $" {missingCount} scene(s) no longer exist and were skipped." : string.Empty;
            if (addedCount == 0 && enabledCount == 0)
            {
                return missingCount > 0
                    ? $"Nothing to add for '{graphName}'.{missing}"
                    : $"All scenes used by '{graphName}' are already in Build Settings.";
            }

            return $"Build Settings: added {addedCount} scene(s) and enabled {enabledCount} scene(s) used by '{graphName}'.{missing}";
        }

        /// <summary>
        /// <paramref name="current"/> に <paramref name="scenes"/> を反映した新しい一覧を返す。
        /// 無効なものは有効にし、無いものは末尾に追加する。既存の並び順は変えない（ビルドは index 0 のシーンから始まるため）。
        /// GUID か Path が空の参照は無視する。<paramref name="current"/> 自体は変更しない。
        /// </summary>
        public static EditorBuildSettingsScene[] AddScenes(
            IReadOnlyList<EditorBuildSettingsScene> current,
            IEnumerable<SceneReference> scenes,
            out int addedCount,
            out int enabledCount)
        {
            addedCount = 0;
            enabledCount = 0;
            var result = current.Where(s => s != null)
                .Select(s => new EditorBuildSettingsScene(s.path, s.enabled) { guid = s.guid })
                .ToList();

            foreach (var scene in scenes)
            {
                if (scene == null || string.IsNullOrEmpty(scene.Guid) || string.IsNullOrEmpty(scene.Path)
                    || !GUID.TryParse(scene.Guid, out var guid))
                {
                    continue;
                }

                var existing = result.FirstOrDefault(s => s.guid == guid);
                if (existing == null)
                {
                    result.Add(new EditorBuildSettingsScene(scene.Path, true) { guid = guid });
                    addedCount++;
                }
                else if (!existing.enabled)
                {
                    existing.enabled = true;
                    enabledCount++;
                }
            }

            return result.ToArray();
        }

        /// <summary>Build Settings の最初のシーン（ビルドが始まるシーン）が <paramref name="sceneGuid"/> か。</summary>
        public static bool IsFirst(IReadOnlyList<EditorBuildSettingsScene> current, string sceneGuid) =>
            current != null && current.Count > 0 && current[0] != null && GUID.TryParse(sceneGuid, out var guid) && current[0].guid == guid;

        /// <summary>
        /// <paramref name="sceneGuid"/> のシーンを先頭へ移した新しい一覧を返す（有効にする。無ければ先頭に追加する）。
        /// ほかのシーンの並びは変えない。<paramref name="current"/> 自体は変更しない。
        /// </summary>
        public static EditorBuildSettingsScene[] MoveToFront(IReadOnlyList<EditorBuildSettingsScene> current, string sceneGuid, string scenePath)
        {
            var result = current.Where(s => s != null)
                .Select(s => new EditorBuildSettingsScene(s.path, s.enabled) { guid = s.guid })
                .ToList();
            if (!GUID.TryParse(sceneGuid, out var guid))
            {
                return result.ToArray();
            }

            var existing = result.FirstOrDefault(s => s.guid == guid);
            if (existing != null)
            {
                result.Remove(existing);
                existing.enabled = true;
            }

            result.Insert(0, existing ?? new EditorBuildSettingsScene(scenePath, true) { guid = guid });
            return result.ToArray();
        }

        /// <summary>
        /// グラフのシーンを Build Settings に追加・有効化し、結果のメッセージを返す。削除済みのシーンは追加しない。
        /// </summary>
        public static string Apply(NodeGraphAsset graph)
        {
            if (graph == null)
            {
                return "No graph is open.";
            }

            // 移動・改名に追従するよう、Path は GUID から引き直したものを使う。引けないものは削除済みとして飛ばす
            var resolved = new GraphQuery(graph).GetScenes()
                .Select(s => new SceneReference(s.Guid, AssetDatabase.GUIDToAssetPath(s.Guid)))
                .ToList();
            var scenes = resolved.Where(s => !string.IsNullOrEmpty(s.Path)).ToList();
            var missing = resolved.Count - scenes.Count;

            var updated = AddScenes(EditorBuildSettings.scenes, scenes, out var added, out var enabled);
            var message = GetResultMessage(graph.name, added, enabled, missing);
            if (added == 0 && enabled == 0)
            {
                return message;
            }

            EditorBuildSettings.scenes = updated;
            Debug.Log("[VisualNodeEditor] " + message, graph);
            return message;
        }
    }
}
