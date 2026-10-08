using System.Linq;
using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor;
using Reiga.VisualNodeEditor.Editor.Views;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>シーンアセットのドロップで Scene ノードを作る操作と、Scene ノードのタイトル。</summary>
    public sealed class SceneDropTests
    {
        private NodeGraphAsset _graph;

        [TearDown]
        public void TearDown()
        {
            if (_graph != null)
            {
                Object.DestroyImmediate(_graph);
            }
        }

        // ---- ドラッグ中のシーンの取り出し ----

        [Test]
        public void FromPaths_KeepsScenesOnlyInOrderWithoutDuplicates()
        {
            var scenes = SceneDrop.FromPaths(
                new[] { "Assets/Game.unity", "Assets/Icon.png", "Assets/Title.unity", "Assets/Game.unity", "Assets/Result.UNITY", null, "" },
                path => "guid:" + path);

            Assert.That(scenes.Select(s => s.Path), Is.EqualTo(new[] { "Assets/Game.unity", "Assets/Title.unity", "Assets/Result.UNITY" }));
            Assert.That(scenes[0].Guid, Is.EqualTo("guid:Assets/Game.unity"));
            Assert.That(scenes[1].Name, Is.EqualTo("Title"));
        }

        [Test]
        public void FromPaths_NothingToDrop()
        {
            Assert.That(SceneDrop.FromPaths(null, p => p), Is.Empty);
            Assert.That(SceneDrop.FromPaths(new[] { "Assets/Graph.asset" }, p => p), Is.Empty);
        }

        [Test]
        public void Positions_GoRightFromTheDropPoint()
        {
            var origin = new Vector2(10f, 20f);

            Assert.That(SceneDrop.GetPosition(origin, 0), Is.EqualTo(origin));
            Assert.That(SceneDrop.GetPosition(origin, 2), Is.EqualTo(origin + new Vector2(SceneDrop.Spacing * 2, 0f)));
        }

        // ---- Scene ノードを作る ----

        [Test]
        public void CreateSceneNodes_BindsEachSceneAndSelectsThem()
        {
            _graph = NodeGraphFactory.CreateNew();
            var view = new NodeGraphView();
            view.Populate(_graph);
            var scenes = SceneDrop.FromPaths(new[] { "Assets/Title.unity", "Assets/Game.unity" }, p => "guid:" + p);

            var created = view.CreateSceneNodes(scenes, new Vector2(100f, 50f));

            var nodes = _graph.Nodes.OfType<SceneNode>().ToList();
            Assert.That(nodes.Select(n => n.SceneName), Is.EqualTo(new[] { "Title", "Game" }));
            Assert.That(nodes[0].Position, Is.EqualTo(new Vector2(100f, 50f)));
            Assert.That(nodes[1].Position, Is.EqualTo(new Vector2(100f + SceneDrop.Spacing, 50f)));
            Assert.That(created.Select(v => v.NodeId), Is.EqualTo(nodes.Select(n => n.Id)));
            Assert.That(view.selection.OfType<NodeView>().Select(v => v.NodeId), Is.EquivalentTo(nodes.Select(n => n.Id)));
            Assert.That(view.EmptyState, Is.EqualTo(EmptyStateKind.None));
        }

        [Test]
        public void CreateSceneNodes_InsideAContainerGoesToThatLevel()
        {
            _graph = NodeGraphFactory.CreateNew();
            var view = new NodeGraphView();
            view.Populate(_graph);
            var container = view.CreateNode(typeof(ContainerNode), Vector2.zero).Data;
            view.EnterLevel(container.Id);

            view.CreateSceneNodes(SceneDrop.FromPaths(new[] { "Assets/Game.unity" }, p => p), Vector2.zero);

            Assert.That(_graph.Nodes.OfType<SceneNode>().Single().ParentId, Is.EqualTo(container.Id));
        }

        // ---- Scene ノードのタイトル ----

        [Test]
        public void SceneNode_IsTitledWithItsSceneUnlessTheUserTitledIt()
        {
            var scene = new SceneNode { Scene = new SceneReference("guid", "Assets/Title.unity") };

            var view = NodeViewFactory.Create(scene);
            Assert.That(view.title, Is.EqualTo("Title"));
            Assert.That(view.Summary, Is.Empty, "the scene name is already the title");

            scene.Title = "Opening";
            view.Rebind(scene);
            Assert.That(view.title, Is.EqualTo("Opening"));
            Assert.That(view.Summary, Is.EqualTo("Title"));

            Assert.That(NodeViewFactory.Create(new SceneNode()).title, Is.EqualTo("Scene"), "no scene yet");
        }
    }
}
