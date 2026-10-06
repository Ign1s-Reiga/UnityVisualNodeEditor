using System;
using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor.Views;

namespace Reiga.VisualNodeEditor.Tests
{
    public sealed class NodeDisplayTests
    {
        [TestCase("Flow/Entry", "flow")]
        [TestCase("Flow/Sub/Entry", "flow")]
        [TestCase("FLOW/Entry", "flow")]
        [TestCase("  Flow /Entry", "flow")]
        [TestCase("/Flow/Entry/", "flow")]
        [TestCase("Game Systems/Score", "game-systems")]
        [TestCase("Entry", "")]
        [TestCase("", "")]
        [TestCase("   ", "")]
        [TestCase(null, "")]
        public void Category_IsNormalizedFirstSegment(string menuPath, string expected)
        {
            Assert.That(NodeCategory.FromMenuPath(menuPath), Is.EqualTo(expected));
        }

        [TestCase(typeof(EntryNode), "flow")]
        [TestCase(typeof(SceneNode), "flow")]
        [TestCase(typeof(StateNode), "state")]
        [TestCase(typeof(EventNode), "event")]
        [TestCase(typeof(NoteNode), "misc")]
        [TestCase(typeof(UnlistedNode), "")]
        public void Category_FromBuiltInTypes(Type nodeType, string expected)
        {
            Assert.That(NodeCategory.FromType(nodeType), Is.EqualTo(expected));
        }

        [TestCase(null, "Event")]
        [TestCase("", "Event")]
        [TestCase("   ", "Event")]
        [TestCase("Boss Defeated", "Boss Defeated")]
        [TestCase("  Boss Defeated ", "Boss Defeated")]
        public void ResolveTitle_FallsBackToTypeDisplayName(string title, string expected)
        {
            Assert.That(NodeDisplay.ResolveTitle(title, "Event"), Is.EqualTo(expected));
        }

        [TestCase(null, "")]
        [TestCase("", "")]
        [TestCase("one line", "one line")]
        [TestCase("\n  \n second \nthird", "second")]
        public void FirstLine_SkipsBlankLines(string text, string expected)
        {
            Assert.That(NodeDisplay.FirstLine(text), Is.EqualTo(expected));
        }

        [Test]
        public void TypeDisplayName_UsesMenuLeafOrTypeName()
        {
            Assert.That(NodeDisplay.GetTypeDisplayName(typeof(EntryNode)), Is.EqualTo("Entry"));
            Assert.That(NodeDisplay.GetTypeDisplayName(typeof(UnlistedNode)), Is.EqualTo("Unlisted"));
        }

        [Test]
        public void FieldLabel_IsEnglishFromSerializedName()
        {
            Assert.That(NodeDisplay.GetFieldLabel("_eventName"), Is.EqualTo("Event Name"));
        }

        /// <summary><see cref="NodeMenuAttribute"/> の無いノード型。</summary>
        [Serializable]
        private sealed class UnlistedNode : NodeData
        {
            protected override string DefaultTitle => "Unlisted";
        }
    }
}
