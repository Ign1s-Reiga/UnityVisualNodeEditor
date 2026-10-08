using System.Linq;
using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor.Clipboard;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>コンテナを含むコピー・貼り付け（子孫ごとのコピーと ParentId の付け替え）。</summary>
    public sealed class ContainerClipboardTests
    {
        private static readonly Vector2 Offset = new Vector2(30f, 30f);

        private ContainerTestGraph _g;

        [SetUp]
        public void SetUp() => _g = new ContainerTestGraph();

        [TearDown]
        public void TearDown() => _g.Dispose();

        [Test]
        public void CopyingAContainer_IncludesAllDescendantsAndTheirEdges()
        {
            var content = Paste(new[] { _g.Stage.Id }, string.Empty);

            // Stage + (StageEntry, Play, Inner, ClearExit) + (InnerEntry, Boss, InnerExit)
            Assert.That(content.Nodes, Has.Count.EqualTo(8));
            Assert.That(content.Edges, Has.Count.EqualTo(3), "StageEntry → Play → ClearExit, InnerEntry → Boss");
            Assert.That(content.Nodes.Select(n => n.Id).Intersect(_g.Asset.Nodes.Select(n => n.Id)), Is.Empty, "all new ids");
        }

        [Test]
        public void PastedContainer_ContentsMoveIntoTheNewContainersAtAnyDepth()
        {
            var content = Paste(new[] { _g.Stage.Id }, string.Empty);

            var stage = content.Nodes.OfType<StageTestContainer>().Single();
            var inner = content.Nodes.OfType<ContainerNode>().Single(n => n.Title == "Inner");
            var play = content.Nodes.Single(n => n.Title == "Play");
            var boss = content.Nodes.Single(n => n.Title == "Boss");
            Assert.That(stage.IsAtRoot, Is.True);
            Assert.That(inner.ParentId, Is.EqualTo(stage.Id));
            Assert.That(play.ParentId, Is.EqualTo(stage.Id));
            Assert.That(boss.ParentId, Is.EqualTo(inner.Id));
            Assert.That(content.Nodes.OfType<ContainerEntryNode>().Select(e => e.ParentId), Is.EquivalentTo(new[] { stage.Id, inner.Id }));
        }

        [Test]
        public void PastedContainer_OnlyTheTopLevelIsOffset()
        {
            var content = Paste(new[] { _g.Stage.Id }, string.Empty);

            Assert.That(content.Nodes.OfType<StageTestContainer>().Single().Position, Is.EqualTo(_g.Stage.Position + Offset));
            Assert.That(content.Nodes.Single(n => n.Title == "Play").Position, Is.EqualTo(_g.Play.Position),
                "positions inside a container are in that container's own canvas");
        }

        [Test]
        public void PastedContainer_IsValid()
        {
            var content = Paste(new[] { _g.Stage.Id }, string.Empty);
            foreach (var node in content.Nodes)
            {
                _g.Asset.AddNode(node);
            }

            foreach (var edge in content.Edges)
            {
                _g.Asset.AddEdge(edge);
            }

            // 出口の ID はそのコンテナの中だけで使うので、そのままで Exit ノード・ポートと一致する
            Assert.That(GraphValidator.Validate(_g.Asset).Where(i => i.Severity == GraphIssueSeverity.Error), Is.Empty);
        }

        [Test]
        public void GroupsAndStickyNotesInsideAContainer_AreCopiedWithIt()
        {
            var content = Paste(new[] { _g.Stage.Id }, string.Empty);

            var stage = content.Nodes.OfType<StageTestContainer>().Single();
            var play = content.Nodes.Single(n => n.Title == "Play");
            var group = content.Groups.Single();
            Assert.That(group.ParentId, Is.EqualTo(stage.Id));
            Assert.That(group.NodeIds, Is.EqualTo(new[] { play.Id }));
            Assert.That(group.Position, Is.EqualTo(_g.StageGroup.Position), "inside the container, not offset");
            Assert.That(content.StickyNotes.Single().ParentId, Is.EqualTo(stage.Id));
        }

        [Test]
        public void ContainerEntry_IsNotCopiedOnItsOwn()
        {
            Assert.That(GraphClipboard.Serialize(_g.Asset, new[] { _g.StageEntry.Id }, null, null), Is.Empty);

            var content = Paste(new[] { _g.StageEntry.Id, _g.Play.Id }, _g.Stage.Id);

            Assert.That(content.Nodes.Single().Title, Is.EqualTo("Play"));
            Assert.That(content.Edges, Is.Empty, "the edge from the Entry is not copied either");
        }

        [Test]
        public void ContainerEntry_InAGroup_IsNotCopiedWithTheGroup()
        {
            _g.StageGroup.AddNode(_g.StageEntry.Id);

            var content = Paste(null, _g.Stage.Id, new[] { _g.StageGroup.Id });

            Assert.That(content.Nodes.OfType<ContainerEntryNode>(), Is.Empty);
            Assert.That(content.Groups.Single().NodeIds, Has.Count.EqualTo(1), "only Play");
        }

        [Test]
        public void PasteIntoAContainer_PutsTheElementsOnThatLevel()
        {
            var data = GraphClipboard.Serialize(_g.Asset, new[] { _g.Result.Id }, null, null);

            var content = GraphClipboard.Deserialize(data, Offset, _g.Stage.Id);

            Assert.That(content.Nodes.Single().ParentId, Is.EqualTo(_g.Stage.Id));
            Assert.That(content.Nodes.Single().Position, Is.EqualTo(_g.Result.Position + Offset));
        }

        [Test]
        public void PasteCopiedFromInsideAContainer_ToTheRoot()
        {
            var content = Paste(new[] { _g.Play.Id }, string.Empty, null, new[] { _g.StageNote.Id });

            Assert.That(content.Nodes.Single().IsAtRoot, Is.True);
            Assert.That(content.StickyNotes.Single().ParentId, Is.Empty);
        }

        private GraphClipboardContent Paste(string[] nodeIds, string targetParentId, string[] groupIds = null, string[] stickyNoteIds = null)
        {
            var data = GraphClipboard.Serialize(_g.Asset, nodeIds, groupIds, stickyNoteIds);
            Assert.That(data, Is.Not.Empty);
            return GraphClipboard.Deserialize(data, Offset, targetParentId);
        }
    }
}
