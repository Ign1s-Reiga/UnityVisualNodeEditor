using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor.Views;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Tests
{
    public sealed class GraphParameterTests
    {
        private NodeGraphAsset _graph;

        [SetUp]
        public void SetUp()
        {
            _graph = ScriptableObject.CreateInstance<NodeGraphAsset>();
            _graph.name = "Params";
            _graph.AddNode(new EntryNode());
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_graph);

        [Test]
        public void Asset_AddFindMoveRemove()
        {
            var a = new GraphParameter("A", GraphParameterType.Int);
            var b = new GraphParameter("B", GraphParameterType.Bool);
            _graph.AddParameter(a);
            _graph.AddParameter(b);

            Assert.That(_graph.FindParameter(b.Id), Is.SameAs(b));
            Assert.That(_graph.FindParameterByName("A"), Is.SameAs(a));
            Assert.That(_graph.MoveParameter(b, 0), Is.True);
            Assert.That(_graph.Parameters, Is.EqualTo(new[] { b, a }));
            Assert.That(_graph.MoveParameter(a, 99), Is.True, "out-of-range index is clamped");
            Assert.That(_graph.RemoveParameter(a), Is.True);
            Assert.That(_graph.Parameters, Is.EqualTo(new[] { b }));
        }

        [Test]
        public void Validator_ReportsDuplicateIds()
        {
            // Inspector のリストで「+」を押したときと同じく、ID ごと複製してから名前だけ変える
            var original = new GraphParameter("Lives", GraphParameterType.Int);
            var copy = JsonUtility.FromJson<GraphParameter>(JsonUtility.ToJson(original));
            copy.Name = "Score";
            _graph.AddParameter(original);
            _graph.AddParameter(copy);
            Assert.That(copy.Id, Is.EqualTo(original.Id), "precondition: the copy shares the ID");

            var issue = GraphValidator.Validate(_graph).Single();

            Assert.That(issue.Severity, Is.EqualTo(GraphIssueSeverity.Error));
            Assert.That(issue.Message, Does.Contain("same ID"));
        }

        [Test]
        public void Validator_ReportsEmptyAndDuplicateNamesOnce()
        {
            _graph.AddParameter(new GraphParameter("", GraphParameterType.Bool));
            _graph.AddParameter(new GraphParameter("Lives", GraphParameterType.Int));
            _graph.AddParameter(new GraphParameter("Lives", GraphParameterType.Int));
            _graph.AddParameter(new GraphParameter("Lives", GraphParameterType.Float));

            var issues = GraphValidator.Validate(_graph);

            Assert.That(issues, Has.Count.EqualTo(2));
            Assert.That(issues.All(i => i.Severity == GraphIssueSeverity.Error), Is.True);
            Assert.That(issues.Count(i => i.Message.Contains("'Lives'")), Is.EqualTo(1));
        }

        [Test]
        public void Runner_StartsFromDefaultsAndResetsOnStart()
        {
            _graph.AddParameter(new GraphParameter("Lives", GraphParameterType.Int) { IntValue = 3 });
            _graph.AddParameter(new GraphParameter("Hard", GraphParameterType.Bool) { BoolValue = true });
            _graph.AddParameter(new GraphParameter("Speed", GraphParameterType.Float) { FloatValue = 1.5f });
            _graph.AddParameter(new GraphParameter("Player", GraphParameterType.String) { StringValue = "Ann" });
            var runner = new GraphRunner(_graph);
            var changed = new List<string>();
            runner.ParameterChanged += changed.Add;

            Assert.That(runner.GetInt("Lives"), Is.EqualTo(3), "readable before Start");
            runner.Start();
            runner.SetInt("Lives", 2);
            runner.SetInt("Lives", 2);
            runner.SetBool("Hard", false);
            runner.SetFloat("Speed", 2f);
            runner.SetString("Player", "Bob");

            Assert.That(runner.GetInt("Lives"), Is.EqualTo(2));
            Assert.That(runner.GetBool("Hard"), Is.False);
            Assert.That(runner.GetFloat("Speed"), Is.EqualTo(2f));
            Assert.That(runner.GetString("Player"), Is.EqualTo("Bob"));
            Assert.That(changed, Is.EqualTo(new[] { "Lives", "Hard", "Speed", "Player" }), "setting the same value does not notify");
            Assert.That(_graph.FindParameterByName("Lives").IntValue, Is.EqualTo(3), "asset defaults are untouched");

            runner.Stop();
            runner.Start();
            Assert.That(runner.GetInt("Lives"), Is.EqualTo(3), "each run starts from the defaults");
            runner.Stop();
        }

        [Test]
        public void Runner_RejectsMissingNameAndWrongType()
        {
            _graph.AddParameter(new GraphParameter("Lives", GraphParameterType.Int));
            var runner = new GraphRunner(_graph);

            Assert.That(runner.HasParameter("Lives"), Is.True);
            Assert.That(runner.HasParameter("Score"), Is.False);
            Assert.Throws<KeyNotFoundException>(() => runner.GetInt("Score"));
            Assert.Throws<System.InvalidOperationException>(() => runner.GetBool("Lives"));
            Assert.Throws<System.InvalidOperationException>(() => runner.SetFloat("Lives", 1f));
        }

        [TestCase(new string[0], "Bool", "Bool")]
        [TestCase(new[] { "Bool" }, "Bool", "Bool1")]
        [TestCase(new[] { "Bool", "Bool1", "Bool2" }, "Bool", "Bool3")]
        [TestCase(new[] { "bool" }, "Bool", "Bool")]
        public void Names_MakeUnique(string[] existing, string baseName, string expected)
        {
            Assert.That(ParameterNames.MakeUnique(existing, baseName), Is.EqualTo(expected));
        }

        [TestCase("Score", true)]
        [TestCase("  Score  ", true)]
        [TestCase("Lives", false)]
        [TestCase("", false)]
        [TestCase("   ", false)]
        public void Names_IsValidRename(string name, bool expected)
        {
            Assert.That(ParameterNames.IsValidName(new[] { "Lives", "Hard" }, name), Is.EqualTo(expected));
        }

        [Test]
        public void Blackboard_ListsParametersAndEditsThem()
        {
            _graph.AddParameter(new GraphParameter("Lives", GraphParameterType.Int));
            var view = new NodeGraphView();
            view.BlackboardVisible = true;
            view.Populate(_graph);
            var blackboard = view.Blackboard;
            var changes = 0;
            blackboard.Changed += () => changes++;

            Assert.That(view.BlackboardVisible, Is.True, "Populate must not remove the blackboard");
            Assert.That(FieldTexts(blackboard), Is.EqualTo(new[] { "Lives" }));

            var added = blackboard.AddParameter(GraphParameterType.Bool);
            Assert.That(added.Name, Is.EqualTo("Bool"));
            Assert.That(FieldTexts(blackboard), Is.EqualTo(new[] { "Lives", "Bool" }));

            Assert.That(blackboard.RenameParameter(added.Id, "Lives"), Is.False, "duplicate name is rejected");
            Assert.That(blackboard.RenameParameter(added.Id, " Hard "), Is.True);
            Assert.That(added.Name, Is.EqualTo("Hard"));

            Assert.That(blackboard.MoveParameter(added.Id, 0), Is.True);
            Assert.That(FieldTexts(blackboard), Is.EqualTo(new[] { "Hard", "Lives" }));

            Assert.That(blackboard.RemoveParameter(added.Id), Is.True);
            Assert.That(FieldTexts(blackboard), Is.EqualTo(new[] { "Lives" }));
            Assert.That(changes, Is.EqualTo(4));
        }

        [TestCase(0, 2, 1)]
        [TestCase(0, 1, 0)]
        [TestCase(0, 3, 2)]
        [TestCase(2, 0, 0)]
        [TestCase(1, 1, 1)]
        public void DropIndex_IsConvertedToFinalIndex(int current, int drop, int expected)
        {
            Assert.That(ParameterBlackboard.ToFinalIndex(current, drop), Is.EqualTo(expected));
        }

        [TestCase(0, 2, new[] { "B", "A", "C" })]
        [TestCase(0, 1, new[] { "A", "B", "C" })]
        [TestCase(0, 3, new[] { "B", "C", "A" })]
        [TestCase(2, 0, new[] { "C", "A", "B" })]
        [TestCase(2, 1, new[] { "A", "C", "B" })]
        public void Blackboard_DragAndDropLandsWhereDropped(int from, int dropIndex, string[] expected)
        {
            // Blackboard のドロップ位置は、ドラッグ中の行がまだ一覧にある状態で数える
            foreach (var name in new[] { "A", "B", "C" })
            {
                _graph.AddParameter(new GraphParameter(name, GraphParameterType.Int));
            }

            var view = new NodeGraphView();
            view.Populate(_graph);
            var moved = _graph.Parameters[from];

            var result = view.Blackboard.MoveParameterToDropIndex(moved.Id, dropIndex);

            Assert.That(_graph.Parameters.Select(p => p.Name), Is.EqualTo(expected));
            Assert.That(FieldTexts(view.Blackboard), Is.EqualTo(expected));
            Assert.That(result, Is.EqualTo(!expected.SequenceEqual(new[] { "A", "B", "C" })),
                "dropping a row where it already is does nothing");
        }

        [Test]
        public void Runner_NotifiesValuesResetOnStart()
        {
            _graph.AddParameter(new GraphParameter("Score", GraphParameterType.Int));
            _graph.AddParameter(new GraphParameter("Hard", GraphParameterType.Bool));
            var runner = new GraphRunner(_graph);
            runner.Start();
            runner.SetInt("Score", 5);
            var changed = new List<string>();
            var scoreSeenInHandler = -1;
            runner.ParameterChanged += name =>
            {
                changed.Add(name);
                scoreSeenInHandler = runner.GetInt("Score");
            };

            runner.Stop();
            runner.Start();

            Assert.That(changed, Is.EqualTo(new[] { "Score" }), "only parameters whose value actually changed");
            Assert.That(scoreSeenInHandler, Is.Zero, "handlers already see the reset value");
            runner.Stop();
        }

        private static List<string> FieldTexts(ParameterBlackboard blackboard) =>
            blackboard.Query<BlackboardField>().ToList().Select(f => f.text).ToList();
    }
}
