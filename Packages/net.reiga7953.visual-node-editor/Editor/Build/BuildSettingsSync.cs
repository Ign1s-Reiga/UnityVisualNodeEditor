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

        /// <summary>Build Settings に有効な状態で入っていないシーンを参照する Scene ノードの警告。</summary>
        public static List<GraphIssue> GetIssues(NodeGraphAsset graph, ICollection<string> enabledSceneGuids)
        {
            var issues = new List<GraphIssue>();
            if (graph == null)
            {
                return issues;
            }

            foreach (var node in graph.Nodes.OfType<SceneNode>())
            {
                var scene = node.Scene;
                if (!string.IsNullOrEmpty(scene.Guid) && !enabledSceneGuids.Contains(scene.Guid))
                {
                    issues.Add(new GraphIssue(GraphIssueSeverity.Warning,
                        $"'{node.Title}' uses scene '{scene.Name}', which is not enabled in Build Settings.", node.Id));
                }
            }

            return issues;
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

        /// <summary>
        /// グラフのシーンを Build Settings に追加・有効化し、結果のメッセージを返す。削除済みのシーンは追加しない。
        /// </summary>
        public static string Apply(NodeGraphAsset graph)
        {
            if (graph == null)
            {
                return "No graph is open.";
            }

            // 移動・改名に追従するよう、Path は GUID から引き直したものを使う
            var scenes = new GraphQuery(graph).GetScenes()
                .Select(s => new SceneReference(s.Guid, AssetDatabase.GUIDToAssetPath(s.Guid)))
                .Where(s => !string.IsNullOrEmpty(s.Path));

            var updated = AddScenes(EditorBuildSettings.scenes, scenes, out var added, out var enabled);
            if (added == 0 && enabled == 0)
            {
                return $"All scenes used by '{graph.name}' are already in Build Settings.";
            }

            EditorBuildSettings.scenes = updated;
            var message = $"Build Settings: added {added} scene(s) and enabled {enabled} scene(s) used by '{graph.name}'.";
            Debug.Log("[VisualNodeEditor] " + message, graph);
            return message;
        }
    }
}
