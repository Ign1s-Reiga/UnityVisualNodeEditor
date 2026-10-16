using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor.Inspector;
using UnityEditor;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>
    /// Editor が SerializedProperty で参照している Runtime の private フィールド名が、
    /// リネームで黙って壊れないことを保証する。
    /// </summary>
    public sealed class SerializedFieldNameTests
    {
        private NodeGraphAsset _graph;
        private SerializedObject _serializedObject;

        [SetUp]
        public void SetUp()
        {
            _graph = ScriptableObject.CreateInstance<NodeGraphAsset>();
            _graph.AddNode(new SceneNode());
            _serializedObject = new SerializedObject(_graph);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_graph);

        [Test]
        public void NodeIdIsReachable()
        {
            var node = _serializedObject.FindProperty(NodeInspectorView.NodesPropertyName).GetArrayElementAtIndex(0);

            Assert.That(node.FindPropertyRelative(NodeInspectorView.IdPropertyName).stringValue,
                Is.EqualTo(_graph.Nodes[0].Id));
        }

        [Test]
        public void SceneReferenceFieldsAreReachable()
        {
            var scene = _serializedObject.FindProperty(NodeInspectorView.NodesPropertyName)
                .GetArrayElementAtIndex(0).FindPropertyRelative(NodeInspectorView.ScenePropertyName);

            Assert.That(scene, Is.Not.Null);
            Assert.That(scene.FindPropertyRelative(SceneReferenceDrawer.GuidPropertyName), Is.Not.Null);
            Assert.That(scene.FindPropertyRelative(SceneReferenceDrawer.PathPropertyName), Is.Not.Null);
        }

        [Test]
        public void SceneReloadIsReachable()
        {
            var reload = _serializedObject.FindProperty(NodeInspectorView.NodesPropertyName)
                .GetArrayElementAtIndex(0).FindPropertyRelative(NodeInspectorView.ReloadPropertyName);

            Assert.That(reload?.propertyType, Is.EqualTo(SerializedPropertyType.Boolean));
        }

        [TestCase(typeof(StateNode))]
        [TestCase(typeof(SceneNode))]
        [TestCase(typeof(ContainerNode))]
        public void ScreenFieldIsReachable(System.Type nodeType)
        {
            var graph = ScriptableObject.CreateInstance<NodeGraphAsset>();
            try
            {
                graph.AddNode((NodeData)System.Activator.CreateInstance(nodeType));
                var node = new SerializedObject(graph).FindProperty(NodeInspectorView.NodesPropertyName).GetArrayElementAtIndex(0);

                Assert.That(node.FindPropertyRelative(NodeScreenField.ScreenPropertyName)?.propertyType, Is.EqualTo(SerializedPropertyType.ObjectReference));
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }
    }
}
