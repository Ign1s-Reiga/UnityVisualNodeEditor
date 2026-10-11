using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>
    /// グラフごとのセーブの置き場（<see cref="NodeGraphAsset.SaveKey"/>）: 新しいグラフはキーを持ち、複製は別のキーになり、
    /// キーを持たない古いグラフは書き換えず、キーを持つ前のセーブも読める。
    /// </summary>
    public sealed class GraphSaveKeyTests
    {
        private string _root;
        private readonly List<GraphRunner> _runners = new();
        private readonly List<NodeGraphAsset> _graphs = new();

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "VneSaveKey_" + Guid.NewGuid().ToString("N"));
            GraphSaveSlots.RootOverride = _root;
        }

        [TearDown]
        public void TearDown()
        {
            _runners.ForEach(runner => runner.Stop());
            _runners.Clear();
            _graphs.ForEach(Object.DestroyImmediate);
            _graphs.Clear();
            GraphSaveSlots.RootOverride = null;
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }

        [Test]
        public void GraphsWithTheSameName_KeepTheirSlotsApart()
        {
            var first = ScriptableObject.CreateInstance<NodeGraphAsset>();
            var second = ScriptableObject.CreateInstance<NodeGraphAsset>();
            try
            {
                first.name = second.name = "Main";
                first.SaveKey = "aaaa";
                second.SaveKey = "bbbb";

                var firstFolder = Path.GetFileName(Path.GetDirectoryName(GraphSaveSlots.GetPath(first, "slot1")));
                var secondFolder = Path.GetFileName(Path.GetDirectoryName(GraphSaveSlots.GetPath(second, "slot1")));

                Assert.That(firstFolder, Is.EqualTo("Main-aaaa"));
                Assert.That(secondFolder, Is.EqualTo("Main-bbbb"));
            }
            finally
            {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void GraphsWithoutAKey_UseTheirName()
        {
            var graph = ScriptableObject.CreateInstance<NodeGraphAsset>();
            try
            {
                graph.name = "Main";

                Assert.That(Path.GetFileName(Path.GetDirectoryName(GraphSaveSlots.GetPath(graph, "slot1"))), Is.EqualTo("Main"));
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void NewGraphs_GetTheirOwnKey()
        {
            var first = NodeGraphFactory.CreateNew();
            var second = NodeGraphFactory.CreateNew();
            try
            {
                Assert.That(first.SaveKey, Is.Not.Empty);
                Assert.That(second.SaveKey, Is.Not.EqualTo(first.SaveKey));
            }
            finally
            {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void MakeUnique_ChangesOnlyATakenKey()
        {
            var graph = ScriptableObject.CreateInstance<NodeGraphAsset>();
            try
            {
                var taken = new HashSet<string>();
                Assert.That(GraphSaveKeySync.MakeUnique(graph, taken, out _), Is.False, "a graph without a key is left alone");

                graph.SaveKey = "same";
                Assert.That(GraphSaveKeySync.MakeUnique(graph, taken, out _), Is.False, "first one keeps its key");
                Assert.That(GraphSaveKeySync.MakeUnique(graph, taken, out _), Is.True, "a second graph with the same key gets a new one");
                Assert.That(graph.SaveKey, Is.Not.EqualTo("same"));
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void CopiedGraph_GetsANewKey_AndGraphsWithoutAKeyAreNotRewritten()
        {
            using var temp = new McpTempGraphs();
            var keyedPath = temp.Save(NodeGraphFactory.CreateNew(), "Keyed");
            var key = AssetDatabase.LoadAssetAtPath<NodeGraphAsset>(keyedPath).SaveKey;
            var oldPath = temp.Save(ScriptableObject.CreateInstance<NodeGraphAsset>(), "Old");
            var copyPath = McpTempGraphs.Folder + "/Keyed Copy.asset";

            Assert.That(AssetDatabase.CopyAsset(keyedPath, copyPath), Is.True);

            Assert.That(AssetDatabase.LoadAssetAtPath<NodeGraphAsset>(keyedPath).SaveKey, Is.EqualTo(key), "the original keeps its key");
            var copyKey = AssetDatabase.LoadAssetAtPath<NodeGraphAsset>(copyPath).SaveKey;
            Assert.That(copyKey, Is.Not.Empty.And.Not.EqualTo(key), "a copy does not share the original's slots");
            Assert.That(AssetDatabase.LoadAssetAtPath<NodeGraphAsset>(oldPath).SaveKey, Is.Empty, "graphs without a key are not rewritten");
        }

        // ---- キーを持つ前（グラフ名だけのフォルダ）のセーブ ----

        [Test]
        public void SavesFromBeforeTheKey_AreMovedIntoTheKeyedFolder()
        {
            var runner = Run("Main");
            GraphSaveSlots.Save(runner, "slot1");

            // 後からキーを持っても、グラフ名だけのフォルダのセーブを見つけ、キーのフォルダへ移す
            runner.Graph.SaveKey = "k";
            Assert.That(GraphSaveSlots.Exists(runner.Graph, "slot1"), Is.True);
            Assert.That(File.Exists(LegacyPath("Main", "slot1")), Is.False);
            Assert.That(File.Exists(GraphSaveSlots.GetPath(runner.Graph, "slot1")), Is.True);
            Assert.That(GraphSaveSlots.Load(runner, "slot1"), Is.True);
        }

        [Test]
        public void DeletedSlots_DoNotComeBackFromBeforeTheKey()
        {
            // 0.13.2 は、キーを持つ前のセーブを残したままキーのフォルダへ保存していた
            var runner = Run("Main");
            GraphSaveSlots.Save(runner, "slot1");
            runner.Graph.SaveKey = "k";
            Directory.CreateDirectory(Path.GetDirectoryName(GraphSaveSlots.GetPath(runner.Graph, "slot1")));
            File.Copy(LegacyPath("Main", "slot1"), GraphSaveSlots.GetPath(runner.Graph, "slot1"));

            Assert.That(GraphSaveSlots.Delete(runner.Graph, "slot1"), Is.True);

            Assert.That(GraphSaveSlots.Exists(runner.Graph, "slot1"), Is.False);
            Assert.That(File.Exists(LegacyPath("Main", "slot1")), Is.False);
        }

        [Test]
        public void SavesOfAnotherGraphWithTheSameName_AreLeftAlone()
        {
            // キーを持たない古いグラフと、キーを持つ新しいグラフが、どちらも Main
            var old = Run("Main");
            GraphSaveSlots.Save(old, "slot1");
            var keyed = Run("Main");
            keyed.Graph.SaveKey = "k";

            Assert.That(GraphSaveSlots.Exists(keyed.Graph, "slot1"), Is.False);
            Assert.That(GraphSaveSlots.Load(keyed, "slot1"), Is.False);
            Assert.That(GraphSaveSlots.Delete(keyed.Graph, "slot1"), Is.False);
            Assert.That(GraphSaveSlots.Exists(old.Graph, "slot1"), Is.True, "the old graph keeps its save");
        }

        // その名前の新しいグラフ（Entry → State）を作って動かす
        private GraphRunner Run(string name)
        {
            var graph = ScriptableObject.CreateInstance<NodeGraphAsset>();
            graph.name = name;
            var entry = new EntryNode();
            var state = new StateNode { Title = "State" };
            graph.AddNode(entry);
            graph.AddNode(state);
            graph.AddEdge(new EdgeData(entry.Id, "out", state.Id, "in"));
            _graphs.Add(graph);
            var runner = new GraphRunner(graph);
            runner.Start();
            _runners.Add(runner);
            return runner;
        }

        private string LegacyPath(string graphName, string slot) => Path.Combine(_root, graphName, slot + ".json");
    }
}
