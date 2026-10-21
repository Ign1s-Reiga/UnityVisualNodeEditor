using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>コードからの組み立て（<see cref="NodeGraphBuilder"/>）。組んだグラフが検証を通り、Runner で思ったとおりに進むこと。</summary>
    public sealed class NodeGraphBuilderTests
    {
        private readonly List<NodeGraphAsset> _graphs = new();
        private readonly List<GraphRunner> _runners = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var runner in _runners)
            {
                runner.Stop();
            }

            foreach (var graph in _graphs)
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void ABranchingEnding_ValidatesAndRuns()
        {
            var builder = Builder();
            var affection = builder.AddParameter("Affection", GraphParameterType.Int);
            var story = builder.Add(new StateNode { Title = "Story" }, 250f, 0f);
            var romance = builder.Add(new StateNode { Title = "Romance" }, 750f, -100f);
            var normal = builder.Add(new StateNode { Title = "Normal" }, 750f, 100f);
            builder.Connect(builder.Entry, story);
            var loved = builder.AddEvent("Finish", 500f, -100f);
            loved.AddCondition(new TransitionCondition(affection.Id, ConditionOperator.GreaterOrEqual) { IntValue = 60 });
            builder.Connect(story, loved, romance);
            builder.Connect(story, builder.AddEvent("Finish", 500f, 100f), normal);

            Assert.That(GraphValidator.Validate(builder.Graph), Is.Empty);
            Assert.That(builder.Graph.SaveKey, Is.Not.Empty, "a new graph, like Assets > Create");
            Assert.That(story.Position, Is.EqualTo(new Vector2(250f, 0f)));

            var plain = Run(builder.Graph);
            plain.Raise("Finish");
            Assert.That(plain.Current, Is.SameAs(normal));

            var lover = Run(builder.Graph);
            lover.SetInt("Affection", 80);
            lover.Raise("Finish");
            Assert.That(lover.Current, Is.SameAs(romance));
        }

        [Test]
        public void AContainer_HasItsEntryAndExits_AndLeadsOut()
        {
            var builder = Builder();
            var stage = builder.AddContainer("Stage", 250f, 0f, null, "Clear");
            var play = builder.Add(new StateNode { Title = "Play" }, 250f, 0f, stage);
            var result = builder.Add(new StateNode { Title = "Result" }, 500f, 0f);
            builder.Connect(builder.Entry, stage);
            builder.Connect(builder.EntryOf(stage), play);
            builder.Connect(play, builder.AddEvent("Win", 500f, 0f, stage), builder.AddExit(stage, "Clear", 750f, 0f));
            builder.ConnectExit(stage, "Clear", result);
            builder.SetOnEnter(result, new ParameterChange(builder.AddParameter("Cleared", GraphParameterType.Bool).Id) { BoolValue = true });

            Assert.That(stage.Exits.Select(exit => exit.Name), Is.EqualTo(new[] { "Clear" }));
            Assert.That(builder.EntryOf(stage).ParentId, Is.EqualTo(stage.Id));
            Assert.That(play.ParentId, Is.EqualTo(stage.Id));
            Assert.That(GraphValidator.Validate(builder.Graph), Is.Empty);

            var runner = Run(builder.Graph);
            Assert.That(runner.Current, Is.SameAs(play));
            runner.Raise("Win");
            Assert.That(runner.Current, Is.SameAs(result));
            Assert.That(runner.GetBool("Cleared"), Is.True);
        }

        [Test]
        public void Mistakes_Throw()
        {
            var builder = Builder();
            var stage = builder.AddContainer("Stage", 0f, 0f, null, "Clear");

            Assert.Throws<ArgumentException>(() => builder.AddExit(stage, "Lose", 0f, 0f));
            Assert.Throws<ArgumentException>(() => builder.ConnectExit(stage, "Lose", builder.Entry));
            Assert.Throws<ArgumentNullException>(() => builder.Connect(null, stage));
            Assert.Throws<ArgumentNullException>(() => builder.Add<StateNode>(null, 0f, 0f));

            // ポートの無い向きには繋がない（Runner が辿らないエッジを黙って作らない）
            var title = builder.Add(new StateNode { Title = "Title" }, 0f, 0f);
            Assert.That(Assert.Throws<ArgumentException>(() => builder.Connect(stage, title)).Message, Does.Contain("ConnectExit"));
            Assert.Throws<ArgumentException>(() => builder.Connect(builder.AddExit(stage, "Clear", 0f, 0f), title));
            Assert.Throws<ArgumentException>(() => builder.Connect(title, builder.Entry));
            Assert.Throws<ArgumentException>(() => builder.ConnectExit(stage, "Clear", builder.EntryOf(stage)));

            var note = builder.Add(new NoteNode(), 0f, 0f);
            Assert.That(Assert.Throws<ArgumentException>(() => builder.Connect(note, title)).Message, Does.Contain("Note"));
            Assert.Throws<ArgumentException>(() => builder.Connect(title, note));

            // Event を挟むときは、2 本とも繋げるときだけ足す（片方だけ繋がった Event を残さない）
            var go = builder.AddEvent("Go", 0f, 0f);
            Assert.Throws<ArgumentException>(() => builder.Connect(title, go, builder.Entry));
            Assert.Throws<ArgumentException>(() => builder.Connect(stage, go, title));
            Assert.Throws<ArgumentNullException>(() => builder.Connect(title, null, title));
            Assert.That(builder.Graph.Edges, Is.Empty);
        }

        [Test]
        public void AnExistingGraph_GetsNewEdgesAfterItsOwn()
        {
            var graph = Track(ScriptableObject.CreateInstance<NodeGraphAsset>());
            var entry = new EntryNode();
            var title = new StateNode { Title = "Title" };
            graph.AddNode(entry);
            graph.AddNode(title);
            graph.AddEdge(new EdgeData(entry.Id, "out", title.Id, "in"));

            var builder = new NodeGraphBuilder(graph);
            var credits = builder.Add(new StateNode { Title = "Credits" }, 0f, 200f);
            builder.Connect(builder.Entry, credits);

            Assert.That(builder.Entry, Is.SameAs(entry));
            Assert.That(graph.Edges.Select(e => e.ToNodeId), Is.EqualTo(new[] { title.Id, credits.Id }));
            var issue = GraphValidator.Validate(graph).Single();
            Assert.That(issue.Message, Does.Contain("'Credits' is never reached"), "connected last, so never reached");
            Assert.That(issue.NodeId, Is.EqualTo(entry.Id), "on the node to reorder");
        }

        private NodeGraphBuilder Builder()
        {
            var builder = new NodeGraphBuilder();
            Track(builder.Graph);
            return builder;
        }

        private NodeGraphAsset Track(NodeGraphAsset graph)
        {
            _graphs.Add(graph);
            return graph;
        }

        private GraphRunner Run(NodeGraphAsset graph)
        {
            var runner = new GraphRunner(graph);
            _runners.Add(runner);
            runner.Start();
            return runner;
        }
    }
}
