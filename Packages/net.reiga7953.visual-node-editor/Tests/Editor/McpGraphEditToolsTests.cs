using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor.Mcp;
using UnityEditor;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>グラフを書き換える MCP のツール（エディタと同じ規則・Undo・保存・ウィンドウへの通知）。</summary>
    public sealed class McpGraphEditToolsTests
    {
        private McpTempGraphs _temp;
        private McpProtocol _protocol;
        private string _path;
        private NodeGraphAsset _graph;

        [SetUp]
        public void SetUp()
        {
            _temp = new McpTempGraphs();
            _protocol = new McpProtocol("test", "0", McpServerHost.CreateTools());
            _path = McpTempGraphs.Folder + "/Flow.asset";
            McpTestClient.CallToolForObject(_protocol, "create_graph", Args(("path", _path)));
            _graph = AssetDatabase.LoadAssetAtPath<NodeGraphAsset>(_path);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var runner in GraphRunner.Running.ToList())
            {
                runner.Stop();
            }

            _temp.Dispose();
        }

        [Test]
        public void CreateGraph_StartsWithAnEntry_AndRefusesBadPaths()
        {
            Assert.That(_graph, Is.Not.Null);
            Assert.That(_graph.Nodes.OfType<EntryNode>().Count(), Is.EqualTo(1));

            Assert.That(McpTestClient.CallTool(_protocol, "create_graph", Args(("path", "Flow.asset"))).IsError, Is.True);
            Assert.That(McpTestClient.CallTool(_protocol, "create_graph", Args(("path", _path))).Text, Does.Contain("already exists"));
        }

        [Test]
        public void CreateGraph_RefusesAFileUnityHasNotImportedYet()
        {
            // 自動更新が切れているときなど、ディスクにはあるが読み込まれていないファイル。後片付けで消してしまわないよう、作る前に断る
            var path = McpTempGraphs.Folder + "/NotImported.asset";
            try
            {
                System.IO.File.WriteAllText(path, "not imported");

                var (text, isError) = McpTestClient.CallTool(_protocol, "create_graph", Args(("path", path)));

                Assert.That(isError, Is.True, text);
                Assert.That(text, Does.Contain("already exists"));
                Assert.That(System.IO.File.ReadAllText(path), Is.EqualTo("not imported"), "the file is left alone");
            }
            finally
            {
                // .asset として読み込まれると Unity が読めずにエラーを出す（別のテストの失敗になる）ので、読み込まれる前に消す
                System.IO.File.Delete(path);
            }
        }

        [Test]
        [TestCase("Docs~")]
        [TestCase(".hidden")]
        public void CreateGraph_ExplainsFoldersUnityIgnores(string name)
        {
            // Unity が読み込まない名前のフォルダは、更新しても読み込まれない。更新を促さず、その理由を返す
            var folder = McpTempGraphs.Folder + "/" + name;
            try
            {
                System.IO.Directory.CreateDirectory(folder);

                var (text, isError) = McpTestClient.CallTool(_protocol, "create_graph", Args(("path", folder + "/Main.asset")));

                Assert.That(isError, Is.True, text);
                Assert.That(text, Does.Contain("Unity ignores"));
                Assert.That(text, Does.Not.Contain("Refresh"));
            }
            finally
            {
                System.IO.Directory.Delete(folder, true);
            }
        }

        [Test]
        public void BuiltFlow_RunsWithRaise()
        {
            var entry = _graph.Nodes.OfType<EntryNode>().Single().Id;
            var title = AddNode(("type", "State"), ("title", "Title"));
            var start = AddNode(("type", "Event"), ("eventName", "StartGame"));
            var game = AddNode(("type", "State"), ("title", "Game"));
            Connect(entry, title);
            Connect(title, start);
            Connect(start, game);

            var runner = new GraphRunner(_graph);
            runner.Start();
            Assert.That(runner.Current.Id, Is.EqualTo(title));
            Assert.That(runner.Raise("StartGame"), Is.True);
            Assert.That(runner.Current.Id, Is.EqualTo(game));
            Assert.That(EditorUtility.IsDirty(_graph), Is.False, "edits are saved");
        }

        [Test]
        public void AddNode_FollowsTheEditorRules()
        {
            var stage = McpTestClient.CallToolForObject(_protocol, "add_node",
                Args(("graph", _path), ("type", "Container"), ("title", "Stage"), ("exits", new List<object> { "Clear", "GameOver" })));
            var stageId = (string)stage["id"];
            var children = _graph.GetChildren(stageId).ToList();
            Assert.That(children.OfType<ContainerEntryNode>().Count(), Is.EqualTo(1), "a container gets its Entry");
            Assert.That(children.OfType<ContainerExitNode>().Count(), Is.EqualTo(2), "and one Exit node per exit");

            var exit = McpTestClient.CallToolForObject(_protocol, "add_node",
                Args(("graph", _path), ("type", "Exit"), ("parent", stageId), ("exit", "GameOver")));
            Assert.That(((Dictionary<string, object>)exit["exit"])["name"], Is.EqualTo("GameOver"));

            Assert.That(Error(("type", "Exit")), Does.Contain("inside a container"));
            Assert.That(Error(("type", "Entry"), ("parent", stageId)), Does.Contain("root"));
            Assert.That(Error(("type", "Spaceship")), Does.Contain("Unknown node type").And.Contain("Scene"));
            Assert.That(Error(("type", "State"), ("eventName", "Go")), Does.Contain("'eventName' is not used by State"));
            Assert.That(Error(("type", "Scene"), ("scene", "Assets/Missing.unity")), Does.Contain("No scene"));
            Assert.That(_graph.Nodes.OfType<StateNode>(), Is.Empty, "a refused node is not added");
        }

        [Test]
        public void Connect_UsesPortNamesAndKeepsEdgesInOneLevel()
        {
            var stage = (string)McpTestClient.CallToolForObject(_protocol, "add_node",
                Args(("graph", _path), ("type", "Container"), ("exits", new List<object> { "Clear", "GameOver" })))["id"];
            var result = AddNode(("type", "State"), ("title", "Result"));
            var inner = (string)McpTestClient.CallToolForObject(_protocol, "add_node",
                Args(("graph", _path), ("type", "State"), ("parent", stage)))["id"];

            var edge = McpTestClient.CallToolForObject(_protocol, "connect", Args(("graph", _path), ("from", stage), ("fromPort", "clear"), ("to", result)));
            Assert.That(edge["fromPort"], Is.EqualTo(_graph.FindNode(stage) is ContainerNode c && c.TryGetExit("Clear", out var clear) ? clear.Id : null));

            Assert.That(McpTestClient.CallTool(_protocol, "connect", Args(("graph", _path), ("from", stage), ("to", result))).Text,
                Does.Contain("several output ports"));
            Assert.That(McpTestClient.CallTool(_protocol, "connect", Args(("graph", _path), ("from", result), ("to", inner))).Text,
                Does.Contain("different containers"));
            Assert.That(McpTestClient.CallTool(_protocol, "connect", Args(("graph", _path), ("from", stage), ("fromPort", "Clear"), ("to", result))).Text,
                Does.Contain("already connected"));

            var removed = McpTestClient.CallToolForObject(_protocol, "disconnect", Args(("graph", _path), ("from", stage), ("to", result)));
            Assert.That(removed["removed"], Is.EqualTo(1L));
        }

        [Test]
        public void UpdateAndRemove_ChangeOnlyWhatIsAsked()
        {
            var state = AddNode(("type", "State"), ("title", "Title"), ("x", 100), ("y", 50));
            McpTestClient.CallToolForObject(_protocol, "update_node", Args(("graph", _path), ("node", state), ("description", "The title screen"), ("y", 80)));

            var node = (StateNode)_graph.FindNode(state);
            Assert.That(node.Title, Is.EqualTo("Title"));
            Assert.That(node.Description, Is.EqualTo("The title screen"));
            Assert.That(node.Position, Is.EqualTo(new Vector2(100f, 80f)));

            var stage = (string)McpTestClient.CallToolForObject(_protocol, "add_node", Args(("graph", _path), ("type", "Container")))["id"];
            var stageEntry = _graph.GetChildren(stage).OfType<ContainerEntryNode>().Single().Id;
            Assert.That(McpTestClient.CallTool(_protocol, "remove_node", Args(("graph", _path), ("node", stageEntry))).IsError, Is.True);

            McpTestClient.CallTool(_protocol, "remove_node", Args(("graph", _path), ("node", stage)));
            Assert.That(_graph.FindNode(stage), Is.Null);
            Assert.That(_graph.FindNode(stageEntry), Is.Null, "the container's contents go with it");
        }

        [Test]
        public void FailedCleanUp_DoesNotHideTheOriginalError()
        {
            // 後片付けの例外は記録だけして投げない（投げると元の失敗の理由が分からなくなる）
            var logged = new List<Exception>();

            Assert.DoesNotThrow(() => GraphEdits.TryCleanUp(() => throw new InvalidOperationException("cannot delete"), logged.Add));
            GraphEdits.TryCleanUp(() => { }, logged.Add);

            Assert.That(logged.Select(e => e.Message), Is.EqualTo(new[] { "cannot delete" }));
        }

        [Test]
        public void EachToolCall_IsItsOwnUndoStep()
        {
            // Unity はマウスやキーの入力でしか Undo を区切らない。ツールの呼び出しごとに区切らないと、1 回の Ctrl+Z でまとめて戻ってしまう
            var state = AddNode(("type", "State"), ("title", "Title"));
            Undo.IncrementCurrentGroup(); // ユーザーのクリックで区切られる
            Undo.RecordObject(_graph, "User edit");
            _graph.FindNode(state).Title = "Edited by the user";

            var first = AddNode(("type", "State"));
            var second = AddNode(("type", "State"));

            Undo.PerformUndo();
            Assert.That(_graph.FindNode(second), Is.Null);
            Assert.That(_graph.FindNode(first), Is.Not.Null, "only the last tool call is undone");

            Undo.PerformUndo();
            Assert.That(_graph.FindNode(first), Is.Null);
            Assert.That(_graph.FindNode(state).Title, Is.EqualTo("Edited by the user"), "the user's own edit is a separate step");
        }

        [TestCase("Assets/__VneMcpTestTemp/Ma:in.asset")]
        [TestCase("Assets/__VneMcpTestTemp/a|b/X.asset")]
        [TestCase("Assets/../X.asset")]
        [TestCase("Assets//X.asset")]
        [TestCase("Assets/__VneMcpTestTemp/Flows./Main.asset")]
        [TestCase("Assets/__VneMcpTestTemp/Flows /Main.asset")]
        [TestCase("Assets/__VneMcpTestTemp/CON/Main.asset")]
        [TestCase("Assets/__VneMcpTestTemp/nul.asset")]
        [TestCase("Assets/__VneMcpTestTemp/CONIN$/Main.asset")]
        [TestCase("Assets/__VneMcpTestTemp/COM0.asset")]
        [TestCase("Assets/__VneMcpTestTemp/LPT¹/Main.asset")]
        [TestCase("Assets/__VneMcpTestTemp/CON .asset")]
        public void CreateGraph_RefusesUnusablePaths(string path)
        {
            var (text, isError) = McpTestClient.CallTool(_protocol, "create_graph", Args(("path", path)));

            Assert.That(isError, Is.True, text);
            Assert.That(text, Does.Contain("not a usable asset path"));
            Assert.That(AssetDatabase.GetSubFolders(McpTempGraphs.Folder), Is.Empty, "nothing is created for a refused path");
        }

        [TestCase("CON .asset", false)]
        [TestCase("CON  .asset", false)]
        [TestCase("CON　.asset", true)]
        [TestCase("NUL\t", false)]
        [TestCase("Game.asset", true)]
        public void ReservedNames_IgnoreOnlyTheSpacesWindowsIgnores(string name, bool usable)
        {
            // Windows が無視するのは拡張子の前の半角スペースだけ。全角スペースが付いた名前は作れるので断らない
            // （タブなどの制御文字は、それ自体が使えない文字として断る）
            Assert.That(GraphEdits.IsUsableName(name), Is.EqualTo(usable));
        }

        [Test]
        public void CreateGraph_ThatFails_RemovesTheFoldersItMade()
        {
            // 失敗した呼び出しでプロジェクトを変えない（作りかけのフォルダを残さない）
            Assert.Throws<InvalidOperationException>(() => GraphEdits.CreateGraph(
                McpTempGraphs.Folder + "/A/B/Main.asset", (_, _) => throw new InvalidOperationException("disk full")));

            Assert.That(AssetDatabase.IsValidFolder(McpTempGraphs.Folder + "/A"), Is.False);
            Assert.That(AssetDatabase.IsValidFolder(McpTempGraphs.Folder), Is.True, "folders that already existed stay");
        }

        [Test]
        public void CreateGraph_RefusesAFolderUnityHasNotImportedYet()
        {
            // ディスクにはあるが読み込まれていないフォルダ。作ったフォルダとして失敗したときに消すと、中のファイルまで消えてしまう
            var folder = McpTempGraphs.Folder + "/NotImported";
            var kept = folder + "/Keep.txt";
            try
            {
                System.IO.Directory.CreateDirectory(folder);
                System.IO.File.WriteAllText(kept, "the user's file");

                var (text, isError) = McpTestClient.CallTool(_protocol, "create_graph", Args(("path", folder + "/Sub/Main.asset")));

                Assert.That(isError, Is.True, text);
                Assert.That(text, Does.Contain("has not imported"));
                Assert.That(System.IO.File.ReadAllText(kept), Is.EqualTo("the user's file"), "the folder and its files are left alone");
            }
            finally
            {
                System.IO.Directory.Delete(folder, true);
            }
        }

        [Test]
        public void CreateGraph_ThatFailsAfterWriting_RemovesTheAsset()
        {
            // アセットを書いた後で失敗しても（既にあったフォルダの中でも）、書いたアセットを残さない
            var path = McpTempGraphs.Folder + "/Half.asset";
            Assert.Throws<InvalidOperationException>(() => GraphEdits.CreateGraph(path, (asset, assetPath) =>
            {
                AssetDatabase.CreateAsset(asset, assetPath);
                throw new InvalidOperationException("save failed");
            }));

            Assert.That(AssetDatabase.LoadMainAssetAtPath(path), Is.Null);
            Assert.That(System.IO.File.Exists(path), Is.False);
            Assert.That(System.IO.File.Exists(path + ".meta"), Is.False, "no orphan .meta is left");
        }

        [TestCase("NaN")]
        [TestCase("Infinity")]
        public void Positions_MustBeFinite(string value)
        {
            var state = AddNode(("type", "State"), ("x", 10));

            var (text, isError) = McpTestClient.CallTool(_protocol, "update_node", Args(("graph", _path), ("node", state), ("x", value)));
            Assert.That(isError, Is.True, text);
            Assert.That(McpTestClient.CallTool(_protocol, "update_node", Args(("graph", _path), ("node", state), ("y", 1e300))).IsError, Is.True,
                "too large for a float");
            Assert.That(_graph.FindNode(state).Position.x, Is.EqualTo(10f));
        }

        [Test]
        public void RefusedUpdate_ChangesNothing()
        {
            // title は正しいが eventName は State に無い: 何も変えずに理由を返す（一部だけ変わったまま残さない）
            var state = AddNode(("type", "State"), ("title", "Title"));
            var (text, isError) = McpTestClient.CallTool(_protocol, "update_node",
                Args(("graph", _path), ("node", state), ("title", "Changed"), ("eventName", "Go")));

            Assert.That(isError, Is.True, text);
            Assert.That(_graph.FindNode(state).Title, Is.EqualTo("Title"));
        }

        [Test]
        public void CreateGraph_ReturnsThePathItWasSavedAt()
        {
            var result = McpTestClient.CallToolForObject(_protocol, "create_graph",
                Args(("path", "  " + McpTempGraphs.Folder.Replace('/', '\\') + "\\Other.asset ")));

            Assert.That(result["path"], Is.EqualTo(McpTempGraphs.Folder + "/Other.asset"));
            Assert.That(McpTestClient.CallTool(_protocol, "get_graph", Args(("graph", result["path"]))).IsError, Is.False,
                "the returned path can be used straight away");
        }

        [Test]
        public void Names_MatchExactlyFirst_AndRefuseToGuess()
        {
            var stage = (string)McpTestClient.CallToolForObject(_protocol, "add_node",
                Args(("graph", _path), ("type", "Container"), ("exits", new List<object> { "Clear", "clear" })))["id"];
            var next = AddNode(("type", "State"));
            var container = (ContainerNode)_graph.FindNode(stage);

            var edge = McpTestClient.CallToolForObject(_protocol, "connect", Args(("graph", _path), ("from", stage), ("fromPort", "clear"), ("to", next)));
            Assert.That(edge["fromPort"], Is.EqualTo(container.Exits[1].Id), "the exact name wins over a case-insensitive one");

            var ambiguous = McpTestClient.CallTool(_protocol, "connect", Args(("graph", _path), ("from", stage), ("fromPort", "CLEAR"), ("to", next)));
            Assert.That(ambiguous.IsError, Is.True);
            Assert.That(ambiguous.Text, Does.Contain("More than one"));
        }

        [Test]
        public void Group_MakesANamedContainer()
        {
            var entry = _graph.Nodes.OfType<EntryNode>().Single().Id;
            var title = AddNode(("type", "State"), ("title", "Title"));
            var game = AddNode(("type", "State"), ("title", "Game"));
            Connect(entry, title);
            Connect(title, game);

            var container = McpTestClient.CallToolForObject(_protocol, "group_into_container",
                Args(("graph", _path), ("nodes", new List<object> { game }), ("title", "Stage")));

            Assert.That(container["label"], Is.EqualTo("Stage"));
            Assert.That(_graph.FindNode(game).ParentId, Is.EqualTo(container["id"]));
        }

        [Test]
        public void Edits_CanBeUndone_AndTellTheWindow()
        {
            NodeGraphAsset edited = null;
            void OnEdited(NodeGraphAsset asset) => edited = asset;
            GraphEdits.Edited += OnEdited;
            try
            {
                Undo.IncrementCurrentGroup();
                var state = AddNode(("type", "State"));
                Assert.That(edited, Is.SameAs(_graph));

                Undo.PerformUndo();
                Assert.That(_graph.FindNode(state), Is.Null);
            }
            finally
            {
                GraphEdits.Edited -= OnEdited;
            }
        }

        // ---- 実行中の流れ ----

        [Test]
        public void RuntimeTools_ShowWhereTheFlowIsAndMoveIt()
        {
            var entry = _graph.Nodes.OfType<EntryNode>().Single().Id;
            var title = AddNode(("type", "State"), ("title", "Title"));
            var start = AddNode(("type", "Event"), ("eventName", "StartGame"));
            var game = AddNode(("type", "State"), ("title", "Game"));
            Connect(entry, title);
            Connect(title, start);
            Connect(start, game);

            var idle = McpTestClient.CallToolForObject(_protocol, "get_runtime_state");
            Assert.That(idle["runners"], Is.Empty);
            Assert.That(McpTestClient.CallTool(_protocol, "send_event", Args(("event", "StartGame"))).Text, Does.Contain("No graph is running"));

            var runner = new GraphRunner(_graph);
            runner.Start();
            var state = McpTestClient.CallToolForObject(_protocol, "get_runtime_state", Args(("graph", _path)));
            var running = (Dictionary<string, object>)((List<object>)state["runners"]).Single();
            Assert.That(running["location"], Is.EqualTo("Title"));
            Assert.That(((List<object>)running["actions"]).Cast<Dictionary<string, object>>().Select(a => a["event"]), Is.EqualTo(new[] { "StartGame" }));

            var wrong = McpTestClient.CallTool(_protocol, "send_event", Args(("event", "Finish")));
            Assert.That(wrong.IsError, Is.True);
            Assert.That(wrong.Text, Does.Contain("It can: StartGame"));

            var moved = McpTestClient.CallToolForObject(_protocol, "send_event", Args(("event", "StartGame")));
            Assert.That(((Dictionary<string, object>)moved["current"])["id"], Is.EqualTo(game));
        }

        // ---- 失敗したときに残さないもの ----

        [Test]
        public void CreateGraph_ThatFails_DestroysTheUnsavedGraph()
        {
            // アセットにならなかったグラフはメモリに残るだけ。失敗のたびに溜まらないよう消す
            UnityEngine.Object unsaved = null;
            Assert.Throws<InvalidOperationException>(() => GraphEdits.CreateGraph(McpTempGraphs.Folder + "/Lost.asset", (asset, _) =>
            {
                unsaved = asset;
                throw new InvalidOperationException("disk full");
            }));

            Assert.That(unsaved == null, Is.True, "the in-memory graph is destroyed");
        }

        [Test]
        public void CreateGraph_ThatUnitySilentlyDidNotSave_DestroysTheUnsavedGraph()
        {
            // Unity が例外を出さずに（ログだけで）作らなかったときも、アセットにならなかったグラフを消す
            UnityEngine.Object unsaved = null;
            Assert.Throws<McpToolException>(() => GraphEdits.CreateGraph(
                McpTempGraphs.Folder + "/NotSaved.asset", (asset, _) => unsaved = asset));

            Assert.That(unsaved == null, Is.True, "the in-memory graph is destroyed");
            Assert.That(AssetDatabase.LoadMainAssetAtPath(McpTempGraphs.Folder + "/NotSaved.asset"), Is.Null);
        }

        [Test]
        public void CreateGraph_ThatFails_RemovesAFileUnityCouldNotLoad()
        {
            // 書いたが Unity が読み込めなかったファイルも消す。残すと、そのパスでのやり直しが「既にある」で断られ続ける
            var path = McpTempGraphs.Folder + "/HalfWritten.asset";
            Assert.Throws<InvalidOperationException>(() => GraphEdits.CreateGraph(path, (_, assetPath) =>
            {
                System.IO.File.WriteAllText(assetPath, "half written");
                throw new InvalidOperationException("disk full");
            }));

            Assert.That(System.IO.File.Exists(path), Is.False);
            Assert.That(GraphEdits.CreateGraph(path), Is.Not.Null, "the same path can be used again");
        }

        private string AddNode(params (string Key, object Value)[] fields)
        {
            var args = Args(fields);
            args["graph"] = _path;
            return (string)McpTestClient.CallToolForObject(_protocol, "add_node", args)["id"];
        }

        private void Connect(string from, string to) =>
            McpTestClient.CallToolForObject(_protocol, "connect", Args(("graph", _path), ("from", from), ("to", to)));

        private string Error(params (string Key, object Value)[] fields)
        {
            var args = Args(fields);
            args["graph"] = _path;
            var (text, isError) = McpTestClient.CallTool(_protocol, "add_node", args);
            Assert.That(isError, Is.True, text);
            return text;
        }

        private static Dictionary<string, object> Args(params (string Key, object Value)[] fields) =>
            fields.ToDictionary(f => f.Key, f => f.Value is int i ? (object)(long)i : f.Value);
    }
}
