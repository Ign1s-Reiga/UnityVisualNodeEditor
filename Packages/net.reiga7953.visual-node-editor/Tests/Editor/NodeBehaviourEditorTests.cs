using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor.Behaviours;
using Reiga.VisualNodeEditor.Editor.Inspector;
using Reiga.VisualNodeEditor.Editor.Views;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>ノードの振る舞いのエディタ側（型の一覧、スクリプトの骨組み、追加・削除、インスペクタ、ノード上の表示）。</summary>
    public sealed class NodeBehaviourEditorTests
    {
        private NodeGraphAsset _graph;
        private EntryNode _entry;
        private StateNode _state;

        [SetUp]
        public void SetUp()
        {
            _graph = ScriptableObject.CreateInstance<NodeGraphAsset>();
            _entry = new EntryNode();
            _state = new StateNode { Title = "Play" };
            _graph.AddNode(_entry);
            _graph.AddNode(_state);
        }

        [TearDown]
        public void TearDown()
        {
            NodeBehaviourScriptCreator.SetPending(null);
            Object.DestroyImmediate(_graph);
        }

        // ---- 型の一覧 ----

        [Test]
        public void Catalog_ListsOnlyCreatablePublicTypes()
        {
            Assert.That(NodeBehaviourCatalog.IsCreatable(typeof(RecordingBehaviour)), Is.True);
            Assert.That(NodeBehaviourCatalog.IsCreatable(typeof(NodeBehaviour)), Is.False, "abstract");
            Assert.That(NodeBehaviourCatalog.IsCreatable(typeof(StateNode)), Is.False, "not a behaviour");
            Assert.That(NodeBehaviourCatalog.IsListed(typeof(RecordingBehaviour)), Is.False, "internal test types stay out of the menu");

            var listed = NodeBehaviourCatalog.GetTypes();
            Assert.That(listed, Has.No.Member(typeof(NodeBehaviour)));
            Assert.That(listed, Has.No.Member(typeof(RecordingBehaviour)));
        }

        [Test]
        public void Catalog_SaveableNeedsSerializable()
        {
            Assert.That(NodeBehaviourCatalog.IsSaveable(typeof(RecordingBehaviour)), Is.True);
            Assert.That(NodeBehaviourCatalog.IsSaveable(typeof(NonSerializableTestBehaviour)), Is.False);
        }

        [Test]
        public void Catalog_MenuPathFollowsTheNamespace()
        {
            Assert.That(NodeBehaviourCatalog.GetMenuPath(typeof(RecordingBehaviour)),
                Is.EqualTo("Reiga/VisualNodeEditor/Tests/RecordingBehaviour"));
        }

        // ---- スクリプトの骨組み ----

        [TestCase("PlayerMovement", "PlayerMovement")]
        [TestCase("player movement", "PlayerMovement")]
        [TestCase("enemy-ai_brain", "EnemyAi_brain")]
        [TestCase("3D Camera", "_3DCamera")]
        [TestCase("  ", NodeBehaviourScriptTemplate.DefaultClassName)]
        [TestCase(null, NodeBehaviourScriptTemplate.DefaultClassName)]
        public void Template_ClassNameFromFileName(string fileName, string expected)
        {
            Assert.That(NodeBehaviourScriptTemplate.ToClassName(fileName), Is.EqualTo(expected));
        }

        [Test]
        public void Template_HasASerializableClassWithEveryCallback()
        {
            var source = NodeBehaviourScriptTemplate.Generate("PlayerMovement");

            Assert.That(source, Does.Contain("[System.Serializable]"));
            Assert.That(source, Does.Contain("public class PlayerMovement : NodeBehaviour"));
            Assert.That(source, Does.Contain("using Reiga.VisualNodeEditor;"));
            Assert.That(source, Does.Contain("public override void OnEnter()"));
            Assert.That(source, Does.Contain("public override void OnUpdate(float deltaTime)"));
            Assert.That(source, Does.Contain("public override void OnFixedUpdate(float fixedDeltaTime)"));
            Assert.That(source, Does.Contain("public override void OnExit()"));
        }

        // ---- 追加・削除・並べ替え ----

        [Test]
        public void Add_AppendsANewInstance()
        {
            var added = NodeBehaviourEditing.Add(_graph, _state.Id, typeof(RecordingBehaviour));

            Assert.That(added, Is.TypeOf<RecordingBehaviour>());
            Assert.That(_state.Behaviours, Is.EqualTo(new NodeBehaviour[] { added }));
        }

        [Test]
        public void Add_RefusesNodesAndTypesThatCannotHoldIt()
        {
            Assert.That(NodeBehaviourEditing.Add(_graph, _entry.Id, typeof(RecordingBehaviour)), Is.Null, "Entry is not a host");
            Assert.That(NodeBehaviourEditing.Add(_graph, _state.Id, typeof(NonSerializableTestBehaviour)), Is.Null,
                "could not be saved in the graph");
            Assert.That(NodeBehaviourEditing.Add(_graph, _state.Id, typeof(NodeBehaviour)), Is.Null, "abstract");
            Assert.That(NodeBehaviourEditing.Add(_graph, "missing", typeof(RecordingBehaviour)), Is.Null);
            Assert.That(_state.Behaviours, Is.Empty);
        }

        [Test]
        public void RemoveAndMove()
        {
            var a = NodeBehaviourEditing.Add(_graph, _state.Id, typeof(RecordingBehaviour));
            var b = NodeBehaviourEditing.Add(_graph, _state.Id, typeof(ControlBehaviour));

            Assert.That(NodeBehaviourEditing.Move(_graph, _state.Id, 1, 0), Is.True);
            Assert.That(_state.Behaviours, Is.EqualTo(new[] { b, a }));
            Assert.That(NodeBehaviourEditing.Move(_graph, _state.Id, 0, 0), Is.False, "same place");
            Assert.That(NodeBehaviourEditing.Move(_graph, _state.Id, 0, 5), Is.False, "out of range");
            Assert.That(NodeBehaviourEditing.Remove(_graph, _state.Id, 0), Is.True);
            Assert.That(_state.Behaviours, Is.EqualTo(new[] { a }));
            Assert.That(NodeBehaviourEditing.Remove(_graph, _state.Id, 3), Is.False);
        }

        [Test]
        public void Add_CanBeUndone()
        {
            Undo.IncrementCurrentGroup();
            NodeBehaviourEditing.Add(_graph, _state.Id, typeof(RecordingBehaviour));

            Undo.PerformUndo();

            Assert.That(((IBehaviourHost)_graph.FindNode(_state.Id)).Behaviours, Is.Empty);
        }

        // ---- Create Script… の追加待ち ----

        [Test]
        public void FindType_UsesTheFullClassName()
        {
            Assert.That(NodeBehaviourScriptCreator.FindType(typeof(RecordingBehaviour).FullName), Is.EqualTo(typeof(RecordingBehaviour)));
            Assert.That(NodeBehaviourScriptCreator.FindType("NoSuchBehaviour"), Is.Null);
        }

        [Test]
        public void PendingScript_IsClearedEvenWhenItCannotBeAdded()
        {
            NodeBehaviourScriptCreator.SetPending(new PendingBehaviourScript(string.Empty, _state.Id, "NoSuchBehaviour"));
            Assert.That(NodeBehaviourScriptCreator.HasPending, Is.True);

            Assert.That(NodeBehaviourScriptCreator.CompletePending(), Is.False);

            Assert.That(NodeBehaviourScriptCreator.HasPending, Is.False, "a renamed class must not be retried forever");
        }

        // ---- インスペクタ ----

        [Test]
        public void Inspector_ShowsTheBehavioursWithTheirFields()
        {
            _state.AddBehaviour(new RecordingBehaviour { Tag = "Hello" });
            var inspector = new NodeInspectorView();

            inspector.Show(_graph, _state.Id);

            var list = inspector.Q<NodeBehaviourListView>();
            Assert.That(list, Is.Not.Null);
            Assert.That(list.Query<Label>(className: "vne-behaviour-list__item-title").ToList().Select(l => l.text),
                Is.EqualTo(new[] { "Recording Behaviour" }));
            Assert.That(list.Query<PropertyField>().ToList().Select(f => f.label), Is.EqualTo(new[] { "Tag" }));

            // リストそのものは生の PropertyField として出さない（専用の一覧だけ）
            var outsideList = inspector.Query<PropertyField>().ToList().Where(f => f.GetFirstAncestorOfType<NodeBehaviourListView>() == null);
            Assert.That(outsideList.Select(f => f.label), Is.EqualTo(new[] { "Description" }));
        }

        [Test]
        public void Inspector_MissingBehaviourCanStillBeRemoved()
        {
            BehaviourList(_state).Add(null);
            var inspector = new NodeInspectorView();
            inspector.Show(_graph, _state.Id);
            var list = inspector.Q<NodeBehaviourListView>();

            Assert.That(list.Query<Label>(className: "vne-behaviour-list__item-title").First().text,
                Is.EqualTo(NodeBehaviourListView.MissingBehaviourLabel));
            Assert.That(list.RemoveBehaviour(0), Is.True);
            Assert.That(_state.Behaviours, Is.Empty);
        }

        [Test]
        public void Inspector_OnlyForNodesThatCanHaveBehaviours()
        {
            var inspector = new NodeInspectorView();

            inspector.Show(_graph, _entry.Id);

            Assert.That(inspector.Q<NodeBehaviourListView>(), Is.Null);
        }

        [Test]
        public void Inspector_ReportsListChangesAsStructureChanges()
        {
            var inspector = new NodeInspectorView();
            var changed = new List<string>();
            inspector.StructureChanged += changed.Add;
            inspector.Show(_graph, _state.Id);

            Assert.That(inspector.Q<NodeBehaviourListView>().AddBehaviour(typeof(RecordingBehaviour)), Is.True);

            Assert.That(changed, Is.EqualTo(new[] { _state.Id }));
        }

        // ---- ノード上の表示 ----

        [Test]
        public void NodeView_ListsItsBehaviours()
        {
            Assert.That(NodeViewFactory.Create(_state, _graph).BehavioursText, Is.Empty);

            _state.AddBehaviour(new RecordingBehaviour());
            BehaviourList(_state).Add(null);
            var view = NodeViewFactory.Create(_state, _graph);

            Assert.That(view.BehavioursText, Is.EqualTo("Recording Behaviour, " + NodeDisplay.MissingBehaviourName));
            Assert.That(view.extensionContainer.Q<Label>(className: "vne-node__behaviours"), Is.Not.Null);
        }

        [Test]
        public void NodeView_UpdatesTheListOnRebind()
        {
            var view = NodeViewFactory.Create(_state, _graph);
            _state.AddBehaviour(new ControlBehaviour());

            view.Rebind(_state);
            Assert.That(view.BehavioursText, Is.EqualTo("Control Behaviour"));

            _state.RemoveBehaviourAt(0);
            view.Rebind(_state);
            Assert.That(view.BehavioursText, Is.Empty);
        }

        [Test]
        public void BehavioursFieldIsReachable()
        {
            _graph.AddNode(new SceneNode());
            var serializedObject = new SerializedObject(_graph);
            var nodes = serializedObject.FindProperty(NodeInspectorView.NodesPropertyName);

            for (var i = 1; i <= 2; i++)
            {
                var behaviours = nodes.GetArrayElementAtIndex(i).FindPropertyRelative(NodeBehaviourListView.BehavioursPropertyName);
                Assert.That(behaviours, Is.Not.Null, _graph.Nodes[i].GetType().Name);
                Assert.That(behaviours.isArray, Is.True);
            }
        }

        private static List<NodeBehaviour> BehaviourList(StateNode node) =>
            (List<NodeBehaviour>)typeof(StateNode)
                .GetField("_behaviours", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(node);
    }
}
