using System.Linq;
using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor.Build;
using Reiga.VisualNodeEditor.Editor.Inspector;
using Reiga.VisualNodeEditor.Editor.Issues;
using Reiga.VisualNodeEditor.Editor.Scenes;
using Reiga.VisualNodeEditor.Editor.Views;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>行き止まりの次の一手（問題を直すボタン、シーンの Pick… / Create Scene…、同じシーンへ戻ったときの説明）。</summary>
    public sealed class NextStepTests
    {
        private NodeGraphAsset _graph;

        [SetUp]
        public void SetUp()
        {
            _graph = ScriptableObject.CreateInstance<NodeGraphAsset>();
            _graph.name = "Flow";
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_graph);

        // ---- 問題の種類と直すボタン ----

        [Test]
        public void Issues_SayWhatKindOfProblemTheyAre()
        {
            var scene = new SceneNode();
            _graph.AddNode(scene);

            var issues = GraphValidator.Validate(_graph);

            Assert.That(issues.Single(i => i.NodeId == null && i.Severity == GraphIssueSeverity.Error).Kind, Is.EqualTo(GraphIssueKind.MissingEntry));
            Assert.That(issues.Single(i => i.NodeId == scene.Id).Kind, Is.EqualTo(GraphIssueKind.SceneNotSet));
        }

        [Test]
        public void BuildSettingsIssues_TellAMissingSceneFromADisabledOne()
        {
            var node = new SceneNode { Scene = new SceneReference("aaaa", "Assets/Game.unity") };
            _graph.AddNode(node);

            var notEnabled = BuildSettingsSync.GetIssues(_graph, new string[0], _ => "Assets/Game.unity").Single();
            var missing = BuildSettingsSync.GetIssues(_graph, new string[0], _ => string.Empty).Single();

            Assert.That(notEnabled.Kind, Is.EqualTo(GraphIssueKind.SceneNotInBuildSettings));
            Assert.That(missing.Kind, Is.EqualTo(GraphIssueKind.SceneMissing));
        }

        [Test]
        public void FixLabels_OnlyForProblemsWithAKnownFix()
        {
            Assert.That(IssueFixes.GetLabel(Issue(GraphIssueKind.MissingEntry)), Is.EqualTo("Add Entry"));
            Assert.That(IssueFixes.GetLabel(Issue(GraphIssueKind.SceneNotSet, "node")), Is.EqualTo("Pick Scene…"));
            Assert.That(IssueFixes.GetLabel(Issue(GraphIssueKind.SceneMissing, "node")), Is.EqualTo("Pick Scene…"));
            Assert.That(IssueFixes.GetLabel(Issue(GraphIssueKind.SceneNotInBuildSettings, "node")), Is.EqualTo("Add to Build Settings"));
            Assert.That(IssueFixes.GetLabel(Issue(GraphIssueKind.NoGraphRunner)), Is.EqualTo("Add Graph Runner…"));
            Assert.That(IssueFixes.GetLabel(Issue(GraphIssueKind.Other, "node")), Is.Null);
            Assert.That(IssueFixes.GetLabel(Issue(GraphIssueKind.SceneNotSet)), Is.Null, "no node to pick the scene for");
            Assert.That(IssueFixes.GetLabel(null), Is.Null);
        }

        [Test]
        public void PickScene_SetsTheSceneInOneUndoStep()
        {
            _graph.AddNode(new EntryNode());
            var node = new SceneNode();
            _graph.AddNode(node);
            var view = new NodeGraphView();
            view.Populate(_graph);
            Undo.IncrementCurrentGroup();

            Assert.That(view.AssignScene(node.Id, new SceneReference("bbbb", "Assets/Scenes/Title.unity")), Is.True);
            Assert.That(view.FindNodeView(node.Id).title, Is.EqualTo("Title"));
            Assert.That(GraphValidator.Validate(_graph).Any(i => i.Kind == GraphIssueKind.SceneNotSet), Is.False);

            Undo.PerformUndo();
            Assert.That(((SceneNode)_graph.FindNode(node.Id)).Scene.IsEmpty, Is.True);
            Assert.That(view.AssignScene(_graph.Nodes.OfType<EntryNode>().Single().Id, new SceneReference("bbbb", "Assets/A.unity")), Is.False,
                "not a Scene node");
        }

        // ---- シーンを選ぶ・作る ----

        [Test]
        public void SceneMenu_ShowsPathsWithoutAssetsAndExtension()
        {
            Assert.That(ScenePicker.GetMenuLabel("Assets/Scenes/Title.unity"), Is.EqualTo("Scenes/Title"));
            Assert.That(ScenePicker.GetMenuLabel("Assets/Game.unity"), Is.EqualTo("Game"));
            Assert.That(ScenePicker.FindScenePaths(), Is.All.StartsWith("Assets/"));
        }

        [Test]
        public void CreateScene_ExplainsWhenUnityCannotCreateOne()
        {
            Assert.That(ScenePicker.GetCreateProblem(new[] { "Assets/Title.unity" }, false), Is.Null);
            Assert.That(ScenePicker.GetCreateProblem(new[] { "Assets/Title.unity", "" }, false), Does.Contain("untitled"));
            Assert.That(ScenePicker.GetCreateProblem(new[] { "Assets/Title.unity" }, true), Does.Contain("Stop Play"));
        }

        [Test]
        public void CreateScene_SuggestsTheNodeTitleAsTheFileName()
        {
            Assert.That(ScenePicker.ToFileName("Boss Stage"), Is.EqualTo("Boss Stage"));
            Assert.That(ScenePicker.ToFileName("Boss/Stage"), Is.EqualTo("Boss_Stage"));
            Assert.That(ScenePicker.ToFileName("  "), Is.EqualTo("New Scene"));
            Assert.That(ScenePicker.ToFileName(null), Is.EqualTo("New Scene"));
        }

        [Test]
        public void SceneField_OffersPickAndCreateOnlyWhileEmpty()
        {
            var node = new SceneNode { Title = "Boss" };
            _graph.AddNode(node);
            var serialized = new SerializedObject(_graph);
            var property = serialized.FindProperty("_nodes").GetArrayElementAtIndex(0).FindPropertyRelative("_scene");

            var root = new SceneReferenceDrawer().CreatePropertyGUI(property);
            var actions = root.Q(className: SceneReferenceDrawer.ActionsClassName);

            Assert.That(actions.Query<Button>().ToList().Select(b => b.text), Is.EqualTo(new[] { "Pick…", "Create Scene…" }));
            Assert.That(actions.style.display.value, Is.EqualTo(DisplayStyle.Flex));
        }

        // ---- 同じシーンへ戻ったとき ----

        [Test]
        public void SceneInspector_ExplainsThatComingBackDoesNotReload()
        {
            var scene = new SceneNode();
            var state = new StateNode();
            _graph.AddNode(scene);
            _graph.AddNode(state);
            var inspector = new NodeInspectorView();

            inspector.Show(_graph, scene.Id);
            Assert.That(inspector.Q<Label>(className: "vne-inspector-view__note")?.text, Is.EqualTo(NodeInspectorView.SceneReloadNote));

            inspector.Show(_graph, state.Id);
            Assert.That(inspector.Q<Label>(className: "vne-inspector-view__note"), Is.Null);
        }

        private static GraphIssue Issue(GraphIssueKind kind, string nodeId = null) =>
            new GraphIssue(GraphIssueSeverity.Warning, "message", nodeId, kind);
    }
}
