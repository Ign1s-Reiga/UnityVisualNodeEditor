using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor.Views;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Tests
{
    public sealed class RunningHighlightTests
    {
        private NodeGraphAsset _graph;
        private EntryNode _entry;
        private StateNode _play;
        private StateNode _result;
        private NodeGraphView _view;

        [SetUp]
        public void SetUp()
        {
            _graph = ScriptableObject.CreateInstance<NodeGraphAsset>();
            _entry = new EntryNode();
            _play = new StateNode();
            _result = new StateNode();
            _graph.AddNode(_entry);
            _graph.AddNode(_play);
            _graph.AddNode(_result);
            _graph.AddEdge(new EdgeData(_entry.Id, "out", _play.Id, "in"));
            _graph.AddEdge(new EdgeData(_play.Id, "out", _result.Id, "in"));
            _view = new NodeGraphView();
            _view.Populate(_graph);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_graph);

        [Test]
        public void SetRunningNode_MovesHighlight()
        {
            _view.SetRunningNode(_play.Id);
            Assert.That(_view.FindNodeView(_play.Id).IsRunning, Is.True);

            _view.SetRunningNode(_result.Id);
            Assert.That(_view.FindNodeView(_play.Id).IsRunning, Is.False);
            Assert.That(_view.FindNodeView(_result.Id).IsRunning, Is.True);
            Assert.That(_view.FindNodeView(_result.Id).ClassListContains("vne-node--running"), Is.True);

            _view.SetRunningNode(null);
            Assert.That(_view.FindNodeView(_result.Id).IsRunning, Is.False);
            Assert.That(_view.RunningNodeId, Is.Null);
        }

        [Test]
        public void Highlight_SurvivesRebuild()
        {
            _view.SetRunningNode(_play.Id);

            _view.Populate(_graph);

            Assert.That(_view.FindNodeView(_play.Id).IsRunning, Is.True);
        }

        [Test]
        public void Highlight_FollowsRunnerTransitions()
        {
            var runner = new GraphRunner(_graph);
            runner.NodeEntered += node => _view.SetRunningNode(node.Id);
            runner.Start();
            Assert.That(_view.FindNodeView(_play.Id).IsRunning, Is.True);

            runner.Advance();
            runner.Stop();

            Assert.That(_view.FindNodeView(_play.Id).IsRunning, Is.False);
            Assert.That(_view.FindNodeView(_result.Id).IsRunning, Is.True);
        }
    }
}
