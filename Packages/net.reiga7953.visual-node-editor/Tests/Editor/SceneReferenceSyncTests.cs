using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor.Scenes;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Tests
{
    public sealed class SceneReferenceSyncTests
    {
        private NodeGraphAsset _graph;

        [SetUp]
        public void SetUp() => _graph = ScriptableObject.CreateInstance<NodeGraphAsset>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_graph);

        [Test]
        public void Resync_UpdatesRenamedScenePath()
        {
            var node = AddScene("guid-title", "Assets/Scenes/Title.unity");

            var changed = SceneReferenceSync.Resync(_graph, guid => guid == "guid-title" ? "Assets/Scenes/TitleScreen.unity" : "");

            Assert.That(changed, Is.True);
            var scene = ((SceneNode)_graph.FindNode(node.Id)).Scene;
            Assert.That(scene.Path, Is.EqualTo("Assets/Scenes/TitleScreen.unity"));
            Assert.That(scene.Name, Is.EqualTo("TitleScreen"));
        }

        [Test]
        public void Resync_LeavesDeletedAndUnassignedScenesAlone()
        {
            var deleted = AddScene("guid-deleted", "Assets/Scenes/Old.unity");
            var unassigned = AddScene(null, null);

            var changed = SceneReferenceSync.Resync(_graph, _ => "");

            Assert.That(changed, Is.False);
            Assert.That(((SceneNode)_graph.FindNode(deleted.Id)).Scene.Path, Is.EqualTo("Assets/Scenes/Old.unity"));
            Assert.That(((SceneNode)_graph.FindNode(unassigned.Id)).Scene.IsEmpty, Is.True);
        }

        [Test]
        public void Resync_ReturnsFalseWhenPathsAreCurrent()
        {
            AddScene("guid-title", "Assets/Scenes/Title.unity");

            Assert.That(SceneReferenceSync.Resync(_graph, _ => "Assets/Scenes/Title.unity"), Is.False);
            Assert.That(SceneReferenceSync.Resync(null, _ => "x"), Is.False);
        }

        private SceneNode AddScene(string guid, string path)
        {
            var node = new SceneNode { Scene = new SceneReference(guid, path) };
            _graph.AddNode(node);
            return node;
        }
    }
}
