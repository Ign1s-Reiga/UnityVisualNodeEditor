using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor.Views;
using Reiga.VisualNodeEditor.YarnSpinner.Editor;
using UnityEngine;
using Yarn.Unity;
using Object = UnityEngine.Object;

namespace Reiga.VisualNodeEditor.YarnSpinner.Tests
{
    /// <summary>Dialogue ノードのデータ・表示・検証と、Bridge を足したときの Variable Storage の設定。</summary>
    public sealed class DialogueNodeTests
    {
        private NodeGraphAsset _graph;

        [SetUp]
        public void SetUp() => _graph = ScriptableObject.CreateInstance<NodeGraphAsset>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_graph);

        [Test]
        public void Label_IsTheYarnNodeUntilTitled()
        {
            var node = new DialogueNode { YarnNode = " Chapter1 " };

            Assert.That(NodeDisplay.GetNodeLabel(node), Is.EqualTo("Chapter1"));
            Assert.That(NodeDisplay.GetNodeLabel(new DialogueNode()), Is.EqualTo("Dialogue"));

            node.Title = "Opening";
            Assert.That(NodeDisplay.GetNodeLabel(node), Is.EqualTo("Opening"));
            Assert.That(DialogueNodeView.GetSummary(node), Is.EqualTo("Yarn: Chapter1"));
        }

        [Test]
        public void Summary_ShowsACustomCompleteEvent()
        {
            var node = new DialogueNode { YarnNode = "Chapter1", CompleteEvent = "Finished" };

            Assert.That(DialogueNodeView.GetSummary(node), Is.EqualTo("then Finished"));
        }

        [Test]
        public void MissingYarnNode_IsWarnedByTheValidator()
        {
            var node = new DialogueNode();
            _graph.AddNode(new EntryNode());
            _graph.AddNode(node);

            var issue = GraphValidator.Validate(_graph).Single(i => i.NodeId == node.Id);

            Assert.That(issue.Severity, Is.EqualTo(GraphIssueSeverity.Warning));
            Assert.That(issue.Message, Does.Contain("no Yarn node"));
        }

        [Test]
        public void EditorCheck_FindsUnknownNodesAndMismatchedVariables()
        {
            var known = new DialogueNode { YarnNode = "Chapter1" };
            var unknown = new DialogueNode { YarnNode = "Chapter9" };
            _graph.AddNode(known);
            _graph.AddNode(unknown);
            _graph.AddParameter(new GraphParameter("Affection", GraphParameterType.Int));
            _graph.AddParameter(new GraphParameter("Route", GraphParameterType.Bool));
            var project = new YarnProjectInfo("Assets/Story.yarnproject", "Story", new[] { "Chapter1" },
                new Dictionary<string, IConvertible> { ["$Affection"] = 0f, ["$Route"] = "Alice" });

            var issues = YarnIssueCheck.Check(_graph, new[] { project }).ToList();

            Assert.That(issues.Single(i => i.NodeId != null).NodeId, Is.EqualTo(unknown.Id));
            Assert.That(issues.Single(i => i.NodeId == null).Message, Does.Contain("'Route' is Bool"));
        }

        [Test]
        public void EditorCheck_StaysQuietForGraphsWithoutDialogue()
        {
            _graph.AddParameter(new GraphParameter("Route", GraphParameterType.Bool));

            Assert.That(YarnIssueCheck.Check(_graph, Array.Empty<YarnProjectInfo>()), Is.Empty);
        }

        [Test]
        public void AddingTheBridge_SharesVariablesWithTheGraph()
        {
            var gameObject = new GameObject("Dialogue System");
            try
            {
                var runner = gameObject.AddComponent<DialogueRunner>();

                Assert.That(YarnBridgeSetup.ShareVariables(runner), Is.True);

                var storage = gameObject.GetComponent<GraphVariableStorage>();
                Assert.That(storage, Is.Not.Null);
                Assert.That(new UnityEditor.SerializedObject(runner).FindProperty(YarnBridgeSetup.VariableStoragePropertyName).objectReferenceValue,
                    Is.SameAs(storage));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
