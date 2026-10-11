using System;
using System.Collections.Generic;
using System.Linq;
using Reiga.VisualNodeEditor.Editor.Build;
using Reiga.VisualNodeEditor.Editor.Play;
using UnityEditor;
using UnityEngine;

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

            // シーンを遷移するグラフなのに、どのシーンにもこのグラフの Graph Runner が無ければ、Play しても何も起きない
            if (graph.Nodes.Any(n => n is SceneNode scene && !scene.Scene.IsEmpty) && !RunnerUsage.IsUsedInBuildScenes(graph))
            {
                issues.Add(new GraphIssue(GraphIssueSeverity.Warning, NoRunnerMessage, kind: GraphIssueKind.NoGraphRunner));
            }

            // アドオンの検証（IGraphIssueCheck）。1 つの不具合で問題の一覧ごと出なくならないよう、例外はログに出して次へ進む
            foreach (var check in Checks)
            {
                try
                {
                    issues.AddRange(check.Check(graph) ?? Enumerable.Empty<GraphIssue>());
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }

            return issues;
        }

        // IGraphIssueCheck の実装（抽象でなく、引数なしのコンストラクタを持つもの）。ドメインリロードのたびに集め直される
        private static IReadOnlyList<IGraphIssueCheck> _checks;

        private static IReadOnlyList<IGraphIssueCheck> Checks => _checks ??= TypeCache.GetTypesDerivedFrom<IGraphIssueCheck>()
            .Where(type => !type.IsAbstract && !type.IsGenericTypeDefinition && type.GetConstructor(Type.EmptyTypes) != null)
            .Select(type => (IGraphIssueCheck)Activator.CreateInstance(type))
            .ToList();

        /// <summary>Build Settings のシーンにこのグラフの Graph Runner が無いときの警告。</summary>
        public const string NoRunnerMessage =
            "No scene in Build Settings has a Graph Runner for this graph, so nothing runs it. " +
            "Press Play in the toolbar to add one (ignore this if you start the graph from code).";
    }
}
