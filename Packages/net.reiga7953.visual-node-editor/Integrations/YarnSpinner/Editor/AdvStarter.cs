using System;
using System.IO;
using System.Linq;
using System.Text;
using Reiga.VisualNodeEditor.Editor;
using Reiga.VisualNodeEditor.Editor.Views;
using UnityEditor;
using UnityEngine;
using Yarn.Unity;
using Yarn.Unity.Editor;

namespace Reiga.VisualNodeEditor.YarnSpinner.Editor
{
    /// <summary>
    /// ADV のひな形（<c>Assets &gt; Create &gt; Visual Node Editor &gt; ADV Starter</c>）。選んでいるフォルダに、グラフ・Yarn スクリプト・
    /// （そのフォルダと親のフォルダに無ければ）Yarn プロジェクト・Title と Story のシーン（<see cref="AdvStarterScenes"/>）を作り、グラフを開く。
    /// ツールバーの Play だけで、最初から最後まで遊べる。
    /// <code>
    /// Entry → [Scene: Title] ─(NewGame)→ [Scene: Story] ─(自動)→ [Container: Story] ─(Ending)→ [Scene: Title]
    /// Story の中: Entry → [Dialogue: Chapter1] → [Dialogue: Chapter2] ─(Done, Affection &gt;= 3)→ [Dialogue: GoodEnd] → Exit (Ending)
    ///                                                                └(Done)→ [Dialogue: NormalEnd] → Exit (Ending)
    /// </code>
    /// Yarn のノード名はプロジェクトの中で重ならないようにする（既にあれば、グラフのファイル名を頭に付ける。例: <c>ADV1_Chapter1</c>）。
    /// </summary>
    public static class AdvStarter
    {
        private const string BaseName = "ADV";
        private const string In = NodeView.InputPortName;
        private const string Out = NodeView.OutputPortName;

        /// <summary>ひな形の Yarn のノード名（頭に付ける文字の前の部分）。</summary>
        public static readonly string[] NodeNames = { "Chapter1", "Chapter2", "GoodEnd", "NormalEnd" };

        [MenuItem("Assets/Create/Visual Node Editor/ADV Starter", priority = 81)]
        private static void CreateFromMenu()
        {
            // シーンを作れないとき（未保存の無題のシーン・Play 中）は、シーン無しで作るか尋ねる
            var problem = AdvStarterScenes.GetProblem();
            if (problem != null && !EditorUtility.DisplayDialog("ADV Starter",
                    $"{problem}\n\nCreate the graph and the Yarn files without the Title and Story scenes? " +
                    "You can pick or create the scenes for the Scene nodes later.",
                    "Create Without Scenes", "Cancel"))
            {
                return;
            }

            var graph = CreateIn(GetSelectedFolder(), problem == null);
            Selection.activeObject = graph;
            NodeGraphEditorWindow.Open(graph);
        }

        /// <summary>
        /// <paramref name="folder"/> にグラフ・Yarn スクリプト・（無ければ）Yarn プロジェクトを作り、保存したグラフを返す。同じ名前のファイルがあれば番号を付ける。
        /// <paramref name="withScenes"/> なら Title と Story のシーンも作り、グラフの Scene ノードに入れる
        /// （未保存の無題のシーンが開いている・Play 中は作れないので、<see cref="AdvStarterScenes.GetProblem"/> で先に確かめること）。
        /// </summary>
        public static NodeGraphAsset CreateIn(string folder, bool withScenes = false)
        {
            var graphPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{BaseName}.asset");
            var name = Path.GetFileNameWithoutExtension(graphPath);

            // 同じ名前の Yarn のノードが既にあれば（2 つ目のひな形など）、Yarn プロジェクトがコンパイルできなくなるので頭に付ける
            YarnProjects.Invalidate();
            var prefix = ChoosePrefix(name, YarnProjects.HasNode);

            var scriptPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{name}.yarn");
            File.WriteAllText(scriptPath, BuildYarnScript(prefix));
            AssetDatabase.ImportAsset(scriptPath);

            // フォルダ（と親のフォルダ）の Yarn プロジェクトは、その下の .yarn を自動で含めるので、無いときだけ作る
            var projects = GetYarnProjectsFor(folder);
            string storyProjectPath;
            if (projects.Length == 0)
            {
                var project = new Yarn.Compiler.Project
                {
                    // Yarn Spinner の Create メニューで作るものと同じ（~ で終わるフォルダとサンプルは含めない）
                    ExcludeFilePatterns = new[] { "**/*~/*", "./Samples/Yarn Spinner*/*" },
                };
                storyProjectPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{name}.yarnproject");
                YarnEditorUtility.CreateYarnProject(storyProjectPath, project);
            }
            else
            {
                // Yarn Spinner は新しいスクリプトを次のフレームでプロジェクトに入れるので、開いたグラフが「無いノード」の警告を出さないよう今入れる
                foreach (var projectPath in projects)
                {
                    AssetDatabase.ImportAsset(projectPath, ImportAssetOptions.ForceUpdate);
                }

                storyProjectPath = projects[0];
            }

            YarnProjects.Invalidate();
            var graph = BuildGraph(prefix);
            AssetDatabase.CreateAsset(graph, graphPath);
            AssetDatabase.SaveAssets();

            if (withScenes)
            {
                AdvStarterScenes.Create(graph, folder, AssetDatabase.LoadAssetAtPath<YarnProject>(storyProjectPath));
            }

            return graph;
        }

        /// <summary>
        /// ひな形の Yarn スクリプト（4 つのノード。Chapter1・Chapter2 の選択肢で <c>$Affection</c> を増やす）。
        /// <c>$Affection</c> は宣言せず、使い方から数と決めさせる（同じプロジェクトにひな形を 2 つ作っても、宣言が重ならないように）。
        /// </summary>
        public static string BuildYarnScript(string prefix = "")
        {
            var script = new StringBuilder();
            void Node(string title, params string[] lines)
            {
                script.Append("title: ").Append(prefix).Append(title).Append('\n').Append("---\n");
                foreach (var line in lines)
                {
                    script.Append(line).Append('\n');
                }

                script.Append("===\n");
            }

            Node(NodeNames[0],
                "Narrator: It is the first day of the summer festival.",
                "Alice: Will you come with me to see the fireworks?",
                "-> Sure, let's go together.",
                "    <<set $Affection to $Affection + 2>>",
                "    Alice: Great! I'll meet you at the gate.",
                "-> Sorry, I'm busy.",
                "    Alice: Oh... maybe next time.");
            Node(NodeNames[1],
                "Narrator: The night of the fireworks.",
                "-> Wait at the gate.",
                "    <<set $Affection to $Affection + 1>>",
                "-> Stay home.",
                "Narrator: The fireworks are about to begin.");
            Node(NodeNames[2],
                "Alice: You came! The fireworks are beautiful.",
                "Narrator: Good ending.");
            Node(NodeNames[3],
                "Narrator: You watch the fireworks alone.",
                "Narrator: Normal ending.");
            return script.ToString();
        }

        /// <summary>ひな形のグラフ（まだアセットとして保存していない）。<paramref name="prefix"/> は Yarn のノード名の頭に付ける文字。</summary>
        public static NodeGraphAsset BuildGraph(string prefix = "")
        {
            var graph = NodeGraphFactory.CreateNew();
            var affection = new GraphParameter("Affection", GraphParameterType.Int);
            graph.AddParameter(affection);

            // ルート: Title → Story（シーン）→ Story（コンテナ）→ Title
            var entry = graph.Nodes.OfType<EntryNode>().Single();
            var title = Add(graph, new SceneNode { Title = AdvStarterScenes.TitleSceneNode }, 250f, 0f);
            var newGame = Add(graph, new EventNode { EventName = AdvStarterScenes.NewGameEvent }, 500f, 0f);
            var storyScene = Add(graph, new SceneNode { Title = AdvStarterScenes.StorySceneNode }, 750f, 0f);
            var begin = Add(graph, new EventNode { Title = "Begin", Trigger = EventTriggerMode.Automatic }, 1000f, 0f);
            var story = Add(graph, new ContainerNode { Title = "Story" }, 1250f, 0f);
            foreach (var exit in story.Exits.ToList())
            {
                story.RemoveExit(exit.Id);
            }

            var ending = story.AddExit("Ending");
            Connect(graph, entry, title);
            Connect(graph, title, newGame);
            Connect(graph, newGame, storyScene);
            Connect(graph, storyScene, begin);
            Connect(graph, begin, story);
            graph.AddEdge(new EdgeData(story.Id, ending.Id, title.Id, In));

            // コンテナの中: 2 つのチャプターの後、Affection でエンディングを分ける
            var inside = new ContainerEntryNode { ParentId = story.Id };
            graph.AddNode(inside);
            var chapter1 = AddIn(graph, story, new DialogueNode { YarnNode = prefix + NodeNames[0] }, 250f, 0f);
            var chapter2 = AddIn(graph, story, new DialogueNode { YarnNode = prefix + NodeNames[1] }, 500f, 0f);
            var toGood = AddIn(graph, story, new EventNode { EventName = DialogueNode.DefaultCompleteEvent, Title = "Good route" }, 750f, -100f);
            toGood.AddCondition(new TransitionCondition(affection.Id, ConditionOperator.GreaterOrEqual) { IntValue = 3 });
            var toNormal = AddIn(graph, story, new EventNode { EventName = DialogueNode.DefaultCompleteEvent, Title = "Otherwise" }, 750f, 100f);
            var good = AddIn(graph, story, new DialogueNode { YarnNode = prefix + NodeNames[2] }, 1000f, -100f);
            var normal = AddIn(graph, story, new DialogueNode { YarnNode = prefix + NodeNames[3] }, 1000f, 100f);
            var exitNode = AddIn(graph, story, new ContainerExitNode { ExitId = ending.Id }, 1250f, 0f);
            Connect(graph, inside, chapter1);
            Connect(graph, chapter1, chapter2);
            Connect(graph, chapter2, toGood);
            Connect(graph, toGood, good);
            Connect(graph, chapter2, toNormal);
            Connect(graph, toNormal, normal);
            Connect(graph, good, exitNode);
            Connect(graph, normal, exitNode);
            return graph;
        }

        // 選んでいるフォルダ（ファイルを選んでいればそのフォルダ。何も無ければ Assets）
        private static string GetSelectedFolder()
        {
            var path = Selection.activeObject != null ? AssetDatabase.GetAssetPath(Selection.activeObject) : string.Empty;
            if (string.IsNullOrEmpty(path))
            {
                return "Assets";
            }

            return AssetDatabase.IsValidFolder(path) ? path : Path.GetDirectoryName(path)?.Replace('\\', '/') ?? "Assets";
        }

        // そのフォルダか親のフォルダにある Yarn プロジェクトのパス
        private static string[] GetYarnProjectsFor(string folder) =>
            AssetDatabase.FindAssets("t:YarnProject")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path =>
                {
                    var projectFolder = Path.GetDirectoryName(path)?.Replace('\\', '/');
                    return projectFolder != null && (folder == projectFolder || folder.StartsWith(projectFolder + "/", StringComparison.Ordinal));
                })
                .ToArray();

        /// <summary>
        /// ひな形のノード名の頭に付ける文字。どのノード名もまだ無ければ付けない。あれば、グラフのファイル名（<c>ADV1_</c>）、
        /// それを付けた名前も既にあれば番号を足していく（<c>ADV2_</c>、<c>ADV3_</c> …）。<paramref name="hasNode"/> は Yarn にそのノードがあるか。
        /// </summary>
        internal static string ChoosePrefix(string graphName, Func<string, bool> hasNode)
        {
            bool IsFree(string prefix) => !NodeNames.Any(node => hasNode(prefix + node));
            if (IsFree(string.Empty))
            {
                return string.Empty;
            }

            var stem = ToIdentifier(graphName);
            for (var number = 1; ; number++)
            {
                var prefix = (number == 1 ? stem : stem + number) + "_";
                if (IsFree(prefix))
                {
                    return prefix;
                }
            }
        }

        // Yarn のノード名に使える形（英数字と '_' だけ。例: "ADV 1" → "ADV1"）
        private static string ToIdentifier(string name)
        {
            var identifier = new string(name.Where(c => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_').ToArray());
            return identifier.Length == 0 || char.IsDigit(identifier[0]) ? "ADV" + identifier : identifier;
        }

        private static T Add<T>(NodeGraphAsset graph, T node, float x, float y) where T : NodeData
        {
            node.Position = new Vector2(x, y);
            graph.AddNode(node);
            return node;
        }

        private static T AddIn<T>(NodeGraphAsset graph, ContainerNode container, T node, float x, float y) where T : NodeData
        {
            node.ParentId = container.Id;
            return Add(graph, node, x, y);
        }

        private static void Connect(NodeGraphAsset graph, NodeData from, NodeData to) => graph.AddEdge(new EdgeData(from.Id, Out, to.Id, In));
    }
}
