using System;
using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor.Views;
using UnityEngine;
using GraphViewFontSize = UnityEditor.Experimental.GraphView.StickyNoteFontSize;
using GraphViewTheme = UnityEditor.Experimental.GraphView.StickyNoteTheme;
using Object = UnityEngine.Object;

namespace Reiga.VisualNodeEditor.Tests
{
    public sealed class StickyNoteTests
    {
        [Test]
        public void Asset_AddsFindsAndRemovesStickyNotes()
        {
            var graph = ScriptableObject.CreateInstance<NodeGraphAsset>();
            var note = new StickyNoteData();
            graph.AddStickyNote(note);

            Assert.That(graph.FindStickyNote(note.Id), Is.SameAs(note));
            Assert.That(graph.RemoveStickyNote(note), Is.True);
            Assert.That(graph.StickyNotes, Is.Empty);
            Object.DestroyImmediate(graph);
        }

        [Test]
        public void NewStickyNote_HasDefaultSizeAndUniqueId()
        {
            var a = new StickyNoteData();
            var b = new StickyNoteData();

            Assert.That(a.Id, Is.Not.EqualTo(b.Id));
            Assert.That(a.Rect.size, Is.EqualTo(StickyNoteData.DefaultSize));
        }

        [Test]
        public void ThemeAndFontSize_RoundTripThroughGraphView()
        {
            foreach (StickyNoteTheme theme in Enum.GetValues(typeof(StickyNoteTheme)))
            {
                Assert.That(StickyNoteView.FromGraphView(StickyNoteView.ToGraphView(theme)), Is.EqualTo(theme));
            }

            foreach (StickyNoteFontSize size in Enum.GetValues(typeof(StickyNoteFontSize)))
            {
                Assert.That(StickyNoteView.FromGraphView(StickyNoteView.ToGraphView(size)), Is.EqualTo(size));
            }

            // GraphView 側の値の数と一致していること（GraphView に値が増えたら気付けるように）
            Assert.That(Enum.GetValues(typeof(GraphViewTheme)).Length, Is.EqualTo(Enum.GetValues(typeof(StickyNoteTheme)).Length));
            Assert.That(Enum.GetValues(typeof(GraphViewFontSize)).Length, Is.EqualTo(Enum.GetValues(typeof(StickyNoteFontSize)).Length));
        }

        [Test]
        public void View_ShowsDataAndKeepsId()
        {
            var data = new StickyNoteData
            {
                Title = "TODO",
                Contents = "Boss scene",
                Theme = StickyNoteTheme.Black,
                FontSize = StickyNoteFontSize.Large,
            };

            var view = new StickyNoteView(data);

            Assert.That(view.StickyNoteId, Is.EqualTo(data.Id));
            Assert.That(view.title, Is.EqualTo("TODO"));
            Assert.That(view.contents, Is.EqualTo("Boss scene"));
            Assert.That(view.theme, Is.EqualTo(GraphViewTheme.Black));
            Assert.That(view.fontSize, Is.EqualTo(GraphViewFontSize.Large));
        }
    }
}
