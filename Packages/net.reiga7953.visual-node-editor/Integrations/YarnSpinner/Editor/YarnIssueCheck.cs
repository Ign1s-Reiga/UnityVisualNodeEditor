using System;
using System.Collections.Generic;
using System.Linq;
using Reiga.VisualNodeEditor.Editor.Issues;

namespace Reiga.VisualNodeEditor.YarnSpinner.Editor
{
    /// <summary>
    /// エディタでの Yarn Spinner 連携の検証（<see cref="IGraphIssueCheck"/>。グラフの問題一覧に出る）:
    /// どの Yarn プロジェクトにも無いノード名の Dialogue ノード、Blackboard のパラメータと型の違う同じ名前の Yarn の変数。
    /// </summary>
    public sealed class YarnIssueCheck : IGraphIssueCheck
    {
        /// <inheritdoc />
        public IEnumerable<GraphIssue> Check(NodeGraphAsset graph) =>
            Check(graph, YarnProjects.All);

        internal static IEnumerable<GraphIssue> Check(NodeGraphAsset graph, IReadOnlyList<YarnProjectInfo> projects)
        {
            var issues = new List<GraphIssue>();
            var dialogues = graph.Nodes.OfType<DialogueNode>().ToList();
            if (dialogues.Count == 0)
            {
                // Yarn を使っていないグラフには何も言わない
                return issues;
            }

            if (projects.Count == 0)
            {
                issues.Add(new GraphIssue(GraphIssueSeverity.Warning,
                    "The graph has Dialogue nodes, but the project has no Yarn Project yet. Create one (Assets > Create > Yarn Spinner > Yarn Project)."));
                return issues;
            }

            foreach (var node in dialogues.Where(n => !string.IsNullOrWhiteSpace(n.YarnNode)))
            {
                var name = node.YarnNode.Trim();
                if (!projects.Any(p => p.NodeNames.Contains(name)))
                {
                    issues.Add(new GraphIssue(GraphIssueSeverity.Warning,
                        $"'{node.Title}' runs the Yarn node '{name}', but no Yarn Project has a node with that name.", node.Id));
                }
            }

            foreach (var parameter in graph.Parameters.Where(p => p != null && !string.IsNullOrEmpty(p.Name)))
            {
                var variable = "$" + parameter.Name;
                foreach (var project in projects)
                {
                    if (project.Variables.TryGetValue(variable, out var value) && !Fits(parameter.Type, value))
                    {
                        issues.Add(new GraphIssue(GraphIssueSeverity.Warning,
                            $"The parameter '{parameter.Name}' is {parameter.Type}, but {variable} in '{project.Name}' is a {DescribeYarnType(value)}. " +
                            "They are the same variable, so make them the same type."));
                    }
                }
            }

            return issues;
        }

        // Yarn の数は Int と Float のどちらとも共有できる
        private static bool Fits(GraphParameterType type, IConvertible value) => type switch
        {
            GraphParameterType.Bool => value is bool,
            GraphParameterType.String => value is string,
            _ => value is float || value is double || value is int,
        };

        private static string DescribeYarnType(IConvertible value) => value switch
        {
            bool _ => "bool",
            string _ => "string",
            _ => "number",
        };
    }
}
