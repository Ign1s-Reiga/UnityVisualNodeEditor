using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Reiga.VisualNodeEditor.YarnSpinner.Editor;
using UnityEditor;
using UnityEngine;
using Yarn.Unity;

namespace Reiga.VisualNodeEditor.YarnSpinner.Tests
{
    /// <summary>ADV のひな形（<see cref="AdvStarter"/>）: グラフがそのまま流れ、Yarn スクリプトがコンパイルできる。</summary>
    public sealed class AdvStarterTests
    {
        private const string Folder = "Assets/__VneAdvStarterTemp";

        [Test]
        public void Prefix_SkipsNamesThatAreAlreadyTaken()
        {
            // 別のフォルダのひな形も同じファイル名（ADV）になりうるので、頭に付けた名前も使われていれば番号を足す
            var taken = new HashSet<string> { "Chapter1", "ADV_Chapter1", "ADV2_GoodEnd" };

            Assert.That(AdvStarter.ChoosePrefix("ADV", taken.Contains), Is.EqualTo("ADV3_"));
            Assert.That(AdvStarter.ChoosePrefix("ADV", new HashSet<string> { "Chapter2" }.Contains), Is.EqualTo("ADV_"));
            Assert.That(AdvStarter.ChoosePrefix("ADV", _ => false), Is.Empty, "no prefix while the names are free");
        }

        [Test]
        public void Graph_HasNoErrors()
        {
            var graph = AdvStarter.BuildGraph();
            try
            {
                var errors = GraphValidator.Validate(graph).Where(i => i.Severity == GraphIssueSeverity.Error).ToList();

                Assert.That(errors, Is.Empty, string.Join("\n", errors));
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [TestCase(5, "GoodEnd")]
        [TestCase(1, "NormalEnd")]
        public void Graph_PlaysThroughToAnEnding(int affection, string ending)
        {
            var graph = AdvStarter.BuildGraph();
            var runner = new GraphRunner(graph);
            var player = new FakeDialoguePlayer();
            var flow = new DialogueFlow(player) { Warn = message => Assert.Fail(message) };
            try
            {
                runner.Start();
                flow.Attach(runner);
                Assert.That(runner.Current.Title, Is.EqualTo("Title"));

                // NewGame → Story のシーン → 自動で Story のコンテナ → Chapter1
                runner.Raise("NewGame");
                flow.Tick();
                Assert.That(player.Started, Is.EqualTo(new[] { "Chapter1" }));

                flow.OnDialogueCompleted();
                flow.Tick();
                runner.SetInt("Affection", affection);
                flow.OnDialogueCompleted();
                flow.Tick();
                Assert.That(player.Started.Last(), Is.EqualTo(ending));

                // エンディングの後はコンテナを出てタイトルへ
                flow.OnDialogueCompleted();
                flow.Tick();
                Assert.That(runner.Current.Title, Is.EqualTo("Title"));
            }
            finally
            {
                flow.Detach();
                runner.Stop();
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void CreateIn_WritesAGraphAndAYarnProjectThatCompiles()
        {
            AssetDatabase.CreateFolder("Assets", "__VneAdvStarterTemp");
            try
            {
                var graph = AdvStarter.CreateIn(Folder);

                Assert.That(AssetDatabase.GetAssetPath(graph), Is.EqualTo(Folder + "/ADV.asset"));
                var project = AssetDatabase.LoadAssetAtPath<YarnProject>(Folder + "/ADV.yarnproject");
                Assert.That(project, Is.Not.Null);
                Assert.That(project.NodeNames, Is.SupersetOf(new[] { "Chapter1", "Chapter2", "GoodEnd", "NormalEnd" }));
                Assert.That(project.InitialValues.Keys, Has.Member("$Affection"));

                // もう 1 つ作ると、番号付きの名前で、同じ Yarn プロジェクトに入る（2 つ目のプロジェクトは作らない）。
                // ノード名が重なるとプロジェクトがコンパイルできないので、頭にファイル名を付ける
                var second = AdvStarter.CreateIn(Folder);
                Assert.That(AssetDatabase.GetAssetPath(second), Is.EqualTo(Folder + "/ADV 1.asset"));
                Assert.That(AssetDatabase.FindAssets("t:YarnProject", new[] { Folder }), Has.Length.EqualTo(1));
                Assert.That(second.Nodes.OfType<DialogueNode>().Select(n => n.YarnNode), Has.Member("ADV1_Chapter1"));
                project = AssetDatabase.LoadAssetAtPath<YarnProject>(Folder + "/ADV.yarnproject");
                Assert.That(project.NodeNames, Is.SupersetOf(new[] { "Chapter1", "ADV1_Chapter1", "ADV1_NormalEnd" }));
            }
            finally
            {
                AssetDatabase.DeleteAsset(Folder);
            }
        }
    }
}
