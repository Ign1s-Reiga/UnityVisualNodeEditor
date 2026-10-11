using System;
using System.IO;
using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>グラフごとのセーブの置き場（<see cref="NodeGraphAsset.SaveKey"/>）と、それをアセットの GUID にそろえる <see cref="GraphSaveKeySync"/>。</summary>
    public sealed class GraphSaveKeyTests
    {
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
        public void Sync_SetsTheKeyOnce()
        {
            var graph = ScriptableObject.CreateInstance<NodeGraphAsset>();
            try
            {
                Assert.That(GraphSaveKeySync.Sync(graph, "guid-1", out _), Is.True);
                Assert.That(graph.SaveKey, Is.EqualTo("guid-1"));
                Assert.That(GraphSaveKeySync.Sync(graph, "guid-1", out _), Is.False, "already in sync");
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void SavedAndCopiedGraphs_GetTheirOwnGuidAsKey()
        {
            using var temp = new McpTempGraphs();
            var path = temp.Save(ScriptableObject.CreateInstance<NodeGraphAsset>(), "Keyed");
            var copyPath = McpTempGraphs.Folder + "/Keyed Copy.asset";

            Assert.That(AssetDatabase.CopyAsset(path, copyPath), Is.True);

            Assert.That(AssetDatabase.LoadAssetAtPath<NodeGraphAsset>(path).SaveKey, Is.EqualTo(AssetDatabase.AssetPathToGUID(path)));
            Assert.That(AssetDatabase.LoadAssetAtPath<NodeGraphAsset>(copyPath).SaveKey, Is.EqualTo(AssetDatabase.AssetPathToGUID(copyPath)),
                "a copy does not share the original's slots");
        }
    }
}
