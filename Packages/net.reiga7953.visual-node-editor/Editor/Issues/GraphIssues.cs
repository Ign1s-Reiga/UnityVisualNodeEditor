using System.Collections.Generic;
using Reiga.VisualNodeEditor.Editor.Build;
using UnityEditor;

namespace Reiga.VisualNodeEditor.Editor.Issues
{
    /// <summary>
    /// エディタで表示する問題をまとめて集める（<see cref="GraphValidator"/> と、エディタでしか分からない Build Settings の確認）。
    /// グラフウィンドウとアセットのインスペクタで同じ結果を出すために共有する。
    /// </summary>
    public static class GraphIssues
    {
        /// <summary><paramref name="graph"/> の問題をすべて返す。グラフが null なら空。</summary>
        public static List<GraphIssue> Collect(NodeGraphAsset graph)
        {
            var issues = new List<GraphIssue>();
            if (graph == null)
            {
                return issues;
            }

            issues.AddRange(GraphValidator.Validate(graph));
            issues.AddRange(BuildSettingsSync.GetIssues(
                graph, BuildSettingsSync.GetEnabledSceneGuids(EditorBuildSettings.scenes), AssetDatabase.GUIDToAssetPath));
            return issues;
        }
    }
}
