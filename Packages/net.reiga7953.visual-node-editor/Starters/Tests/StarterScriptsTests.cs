using System;
using System.IO;
using NUnit.Framework;
using Reiga.VisualNodeEditor.Starters.Editor;

namespace Reiga.VisualNodeEditor.Starters.Tests
{
    /// <summary>
    /// Starter が書き出すゲームのスクリプト。ひな形はそのままコンパイルできる C#（このテストのアセンブリが参照してコンパイルしている）で、
    /// 書き出すときはクラス名だけを置き換える。
    /// </summary>
    public sealed class StarterScriptsTests
    {
        private string _folder;

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "VneStarterScripts_" + Guid.NewGuid().ToString("N")).Replace('\\', '/');
            Directory.CreateDirectory(_folder);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_folder))
            {
                Directory.Delete(_folder, true);
            }
        }

        [TestCase("SnakeGameTemplate")]
        [TestCase("SlidingPuzzleGameTemplate")]
        public void Templates_AreInThePackage(string templateClass)
        {
            var template = StarterScripts.LoadTemplate(templateClass);

            Assert.That(template, Does.Contain($"public class {templateClass} : MonoBehaviour"));
        }

        [Test]
        public void Writing_RenamesOnlyTheClass()
        {
            var path = StarterScripts.Write(_folder, "SnakeGame", "SnakeGameTemplate");

            var text = File.ReadAllText(path);
            Assert.That(path, Does.EndWith("/SnakeGame.cs"), "Unity needs the file name to match the class");
            Assert.That(text, Does.Contain("public class SnakeGame : MonoBehaviour"));
            Assert.That(text, Does.Not.Contain("SnakeGameTemplate"));
            Assert.That(text, Does.Contain("GraphInput.GetKeyDown"), "the rest of the template is kept");
        }

        [Test]
        public void Render_ReplacesWholeWordsOnly()
        {
            Assert.That(StarterScripts.Render("class AGame : B { AGame x; AGameX y; }", "AGame", "Snake1Game"),
                Is.EqualTo("class Snake1Game : B { Snake1Game x; AGameX y; }"));
        }

        [Test]
        public void ClassNames_ComeFromTheGraphName_AndSkipTakenOnes()
        {
            Assert.That(StarterScripts.ChooseClassName("Snake", _folder, _ => false), Is.EqualTo("SnakeGame"));
            Assert.That(StarterScripts.ChooseClassName("Snake 1", _folder, _ => false), Is.EqualTo("Snake1Game"));
            Assert.That(StarterScripts.ChooseClassName("Sliding Puzzle", _folder, _ => false), Is.EqualTo("SlidingPuzzleGame"));
            Assert.That(StarterScripts.ChooseClassName("Snake", _folder, name => name == "SnakeGame" || name == "SnakeGame2"), Is.EqualTo("SnakeGame3"));

            // 既定では、同じ名前のファイルがフォルダにあれば使わない
            File.WriteAllText(Path.Combine(_folder, "PuzzleTestGame.cs"), "// taken");
            Assert.That(StarterScripts.ChooseClassName("Puzzle Test", _folder), Is.EqualTo("PuzzleTestGame2"));
        }
    }
}
