using NUnit.Framework;

namespace Reiga.VisualNodeEditor.Tests
{
    public sealed class SceneReferenceTests
    {
        [Test]
        public void Name_IsFileNameWithoutExtension()
        {
            var reference = new SceneReference("guid", "Assets/Scenes/Title.unity");

            Assert.That(reference.Name, Is.EqualTo("Title"));
            Assert.That(reference.IsEmpty, Is.False);
        }

        [Test]
        public void Default_IsEmpty()
        {
            var reference = new SceneReference();

            Assert.That(reference.IsEmpty, Is.True);
            Assert.That(reference.Name, Is.Empty);
        }

        [Test]
        public void SceneNode_NeverReturnsNullScene()
        {
            var node = new SceneNode { Scene = null };

            Assert.That(node.Scene, Is.Not.Null);
            Assert.That(node.SceneName, Is.Empty);
        }
    }
}
