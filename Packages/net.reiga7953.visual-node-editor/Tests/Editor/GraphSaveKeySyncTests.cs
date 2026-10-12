using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor;
using UnityEditor;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>
    /// 重なったセーブキーの直し方（<see cref="GraphSaveKeySync"/>）: どのグラフがキーを残すか、覚えているキーの直し方、
    /// スクリプトの再読み込みの後の確かめ直し。
    /// </summary>
    public sealed class GraphSaveKeySyncTests
    {
        private McpTempGraphs _temp;

        [SetUp]
        public void SetUp() => _temp = new McpTempGraphs();

        [TearDown]
        public void TearDown() => _temp.Dispose();

        [Test]
        public void TheGraphWhoseGuidIsTheKey_KeepsIt_WhateverThePathOrder()
        {
            // 0.13.1 はアセットの GUID をキーにしたので、GUID と同じキーを持つほうが元のグラフ
            var first = Create("A");
            var second = Create("B");
            var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(second));
            WriteKeysWithoutChecking(guid, first, second);

            GraphSaveKeySync.CheckAll();

            Assert.That(second.SaveKey, Is.EqualTo(guid));
            Assert.That(first.SaveKey, Is.Not.Empty.And.Not.EqualTo(guid));
            Assert.That(EditorUtility.IsDirty(first), Is.False, "the new key is saved");
        }

        [Test]
        public void CheckAll_FixesDuplicatesLeftOnDisk()
        {
            // 未保存の変更があったので保存しなかった複製が、重なったキーのままディスクに残った
            var original = Create("A");
            var copy = Create("B");
            WriteKeysWithoutChecking(original.SaveKey, copy);
            var key = original.SaveKey;

            GraphSaveKeySync.CheckAll();

            Assert.That(original.SaveKey, Is.EqualTo(key), "the first by name keeps the key");
            Assert.That(copy.SaveKey, Is.Not.Empty.And.Not.EqualTo(key));
            Assert.That(EditorUtility.IsDirty(copy), Is.False, "the new key is saved");
        }

        // Unity の複製は "Main 1"・"Stage 9" の次の "Stage 10"。文字の順では、どちらも複製のほうが先になる
        // （"Main 1.asset" は ' ' が '.' より前、"Stage 10" は '1' が '9' より前）
        [TestCase("Main", "Main 1")]
        [TestCase("Stage 9", "Stage 10")]
        public void UnitysCopy_DoesNotTakeTheOriginalsKey(string originalName, string copyName)
        {
            var original = Create(originalName);
            var copy = Create(copyName);
            var key = original.SaveKey;
            WriteKeysWithoutChecking(key, copy);

            GraphSaveKeySync.CheckAll();

            Assert.That(original.SaveKey, Is.EqualTo(key));
            Assert.That(copy.SaveKey, Is.Not.Empty.And.Not.EqualTo(key));
        }

        [Test]
        public void DeletedGraphs_FreeTheirKey_AndMovedGraphsKeepIt()
        {
            var moved = Create("Moved");
            var key = moved.SaveKey;
            Assert.That(AssetDatabase.MoveAsset(AssetDatabase.GetAssetPath(moved), McpTempGraphs.Folder + "/Renamed.asset"), Is.Empty);

            // 動かしたグラフはキーを持ち続けるので、同じキーの新しいグラフは入れ直される
            var clash = NodeGraphFactory.CreateNew();
            clash.SaveKey = key;
            _temp.Save(clash, "Clash");
            Assert.That(clash.SaveKey, Is.Not.EqualTo(key));

            // 消したグラフのキーは空くので、同じキーの新しいグラフはそのまま
            Assert.That(AssetDatabase.DeleteAsset(McpTempGraphs.Folder + "/Renamed.asset"), Is.True);
            var reuse = NodeGraphFactory.CreateNew();
            reuse.SaveKey = key;
            _temp.Save(reuse, "Reuse");
            Assert.That(reuse.SaveKey, Is.EqualTo(key));
        }

        private NodeGraphAsset Create(string name)
        {
            var path = _temp.Save(NodeGraphFactory.CreateNew(), name);
            return AssetDatabase.LoadAssetAtPath<NodeGraphAsset>(path);
        }

        // 重なったキーを、直されないままディスクに書く
        private static void WriteKeysWithoutChecking(string key, params NodeGraphAsset[] graphs) =>
            GraphSaveKeySync.WithoutFixing(() =>
            {
                foreach (var graph in graphs)
                {
                    graph.SaveKey = key;
                    EditorUtility.SetDirty(graph);
                    AssetDatabase.SaveAssetIfDirty(graph);
                }
            });
    }
}
