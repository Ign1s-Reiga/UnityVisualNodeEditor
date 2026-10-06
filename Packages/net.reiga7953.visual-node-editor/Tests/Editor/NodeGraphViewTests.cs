using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor.Views;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Tests
{
    public sealed class NodeGraphViewTests
    {
        [Test]
        public void MiniMap_IsHiddenByDefaultAndToggles()
        {
            var view = new NodeGraphView();
            Assert.That(view.MiniMapVisible, Is.False);
            Assert.That(view.Q<MiniMap>(), Is.Null);

            view.MiniMapVisible = true;
            view.MiniMapVisible = true;

            Assert.That(view.MiniMapVisible, Is.True);
            Assert.That(view.Query<MiniMap>().ToList(), Has.Count.EqualTo(1), "toggling on twice must not add a second minimap");

            view.MiniMapVisible = false;

            Assert.That(view.MiniMapVisible, Is.False);
            Assert.That(view.Q<MiniMap>(), Is.Null);
        }

        [Test]
        public void CopyPasteCallbacks_AreConnected()
        {
            var view = new NodeGraphView();

            Assert.That(view.serializeGraphElements, Is.Not.Null);
            Assert.That(view.canPasteSerializedData, Is.Not.Null);
            Assert.That(view.unserializeAndPaste, Is.Not.Null);
        }
    }
}
