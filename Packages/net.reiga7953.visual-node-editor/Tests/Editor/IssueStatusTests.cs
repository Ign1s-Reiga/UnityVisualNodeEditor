using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor.Issues;

namespace Reiga.VisualNodeEditor.Tests
{
    public sealed class IssueStatusTests
    {
        [TestCase(0, 0, "No issues", IssueDisplayState.None)]
        [TestCase(1, 0, "1 error", IssueDisplayState.Error)]
        [TestCase(2, 0, "2 errors", IssueDisplayState.Error)]
        [TestCase(0, 1, "1 warning", IssueDisplayState.Warning)]
        [TestCase(0, 3, "3 warnings", IssueDisplayState.Warning)]
        [TestCase(2, 1, "2 errors, 1 warning", IssueDisplayState.Error)]
        public void TextAndState_FollowCounts(int errors, int warnings, string expectedText, IssueDisplayState expectedState)
        {
            Assert.That(IssueStatus.GetText(errors, warnings), Is.EqualTo(expectedText));
            Assert.That(IssueStatus.GetState(errors, warnings), Is.EqualTo(expectedState));
        }

        [Test]
        public void HasNewErrors_OnlyWhenAnErrorIsNotKnownYet()
        {
            Assert.That(IssueStatus.HasNewErrors(new string[0], new[] { "a" }), Is.True);
            Assert.That(IssueStatus.HasNewErrors(new[] { "a" }, new[] { "a" }), Is.False);
            Assert.That(IssueStatus.HasNewErrors(new[] { "a", "b" }, new[] { "a" }), Is.False);
            Assert.That(IssueStatus.HasNewErrors(new[] { "a" }, new[] { "a", "b" }), Is.True);
            Assert.That(IssueStatus.HasNewErrors(new[] { "a" }, new string[0]), Is.False);
        }

        [Test]
        public void Key_DistinguishesNodeAndMessage()
        {
            var a = new GraphIssue(GraphIssueSeverity.Error, "Same message", "node-a");
            var b = new GraphIssue(GraphIssueSeverity.Error, "Same message", "node-b");
            var c = new GraphIssue(GraphIssueSeverity.Error, "Same message", "node-a");

            Assert.That(IssueStatus.GetKey(a), Is.Not.EqualTo(IssueStatus.GetKey(b)));
            Assert.That(IssueStatus.GetKey(a), Is.EqualTo(IssueStatus.GetKey(c)));
        }
    }
}
