using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor.Inspector;
using UnityEditor;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>
    /// パラメータ名の欄（<see cref="GraphParameterNameDrawer"/>）の IMGUI の「▾」: 一覧を作らずに出すかを決め、
    /// 選んだ名前は、対象のオブジェクトから作り直した SerializedObject で書く（描いたときのものが捨てられていても書ける）。
    /// </summary>
    public sealed class GraphParameterNameDrawerTests
    {
        private const string TimeLeftPath = "_nodes.Array.data[0]._behaviours.Array.data[0]._timeLeft";

        private NodeGraphAsset _graph;

        [SetUp]
        public void SetUp()
        {
            _graph = ScriptableObject.CreateInstance<NodeGraphAsset>();
            _graph.AddParameter(new GraphParameter("Time", GraphParameterType.Float));
            var state = new StateNode { Title = "Play" };
            state.AddBehaviour(new TimerBehaviour());
            _graph.AddNode(state);
        }

        [TearDown]
        public void TearDown()
        {
            if (_graph != null)
            {
                Object.DestroyImmediate(_graph);
            }
        }

        [Test]
        public void TheMenuButton_NeedsANamedParameter()
        {
            Assert.That(GraphParameterNameDrawer.HasChoices(null), Is.False);
            Assert.That(GraphParameterNameDrawer.HasChoices(_graph), Is.True);

            var unnamed = ScriptableObject.CreateInstance<NodeGraphAsset>();
            try
            {
                unnamed.AddParameter(new GraphParameter(string.Empty, GraphParameterType.Int));
                Assert.That(GraphParameterNameDrawer.HasChoices(unnamed), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(unnamed);
            }
        }

        [Test]
        public void APickedName_IsWritten_AfterTheInspectorsSerializedObjectIsGone()
        {
            var inspector = new SerializedObject(_graph);
            Assert.That(inspector.FindProperty(TimeLeftPath), Is.Not.Null);
            var targets = inspector.targetObjects;
            inspector.Dispose();

            GraphParameterNameDrawer.Write(targets, TimeLeftPath, "Time");

            Assert.That(TimerOf(_graph).TimeLeft, Is.EqualTo("Time"));
        }

        [Test]
        public void AGoneTarget_OrAMissingField_WritesNothing()
        {
            var targets = new Object[] { _graph };

            Assert.DoesNotThrow(() => GraphParameterNameDrawer.Write(targets, "_nodes.Array.data[0]._missing", "Time"));
            Assert.That(TimerOf(_graph).TimeLeft, Is.Empty);

            Object.DestroyImmediate(_graph);
            Assert.DoesNotThrow(() => GraphParameterNameDrawer.Write(targets, TimeLeftPath, "Time"));
        }

        private static TimerBehaviour TimerOf(NodeGraphAsset graph) => (TimerBehaviour)((StateNode)graph.Nodes[0]).Behaviours[0];
    }
}
