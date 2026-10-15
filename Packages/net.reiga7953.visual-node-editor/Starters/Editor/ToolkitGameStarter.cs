using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Reiga.VisualNodeEditor.Editor.Play;
using Reiga.VisualNodeEditor.Editor.Scenes;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Starters.Editor
{
    /// <summary>
    /// ゲームの Starter の UI Toolkit 版。画面は Unity のシーンでなくグラフのノードに付ける（<see cref="IHasScreen"/>）: 1 つのシーン（Scene ノード <c>Main</c>）の中で、
    /// Title・Game（コンテナに付けて、中にいる間ずっと出す）・その上に重ねるポーズなどの画面を、Graph Runner の Graph Screen Host が出し替える。
    /// 画面は <c>&lt;名前&gt; &lt;画面&gt;.uxml</c> と <c>&lt;名前&gt;.uss</c>、Panel Settings（<c>&lt;名前&gt; Panel Settings.asset</c> と <c>Runtime Theme.tss</c>）。
    /// シーン <c>&lt;名前&gt;.unity</c> には Main Camera、Graph Runner（UIDocument と Graph Screen Host 付き。ゲームのスクリプトもここに付ける）、キー。
    /// 遊び・パラメータ・キーは uGUI 版（<see cref="Game"/>）と同じ。画面は UI Builder で直せる。
    /// </summary>
    internal abstract class ToolkitGameStarter : GameStarter
    {
        /// <summary>ただ 1 つのシーンが入る Scene ノードのタイトル。</summary>
        internal const string MainSceneNode = "Main";

        /// <summary>Title の画面を出す State ノードのタイトル。</summary>
        internal const string TitleNode = "Title";

        /// <summary>Title の画面のキーの案内（Return で Start。Space はゲームの中で使うので、Title では使わない）。</summary>
        internal const string TitleHint = "Enter: start";

        /// <summary>同じゲームの uGUI 版（名前・操作の説明・キーを借りる）。</summary>
        internal abstract GameStarter Game { get; }

        internal override string BaseName => Game.BaseName;

        internal override string Controls => Game.Controls;

        internal override string ScenesName => "the game's scene";

        /// <summary>
        /// Title 以外の画面: (ファイル名の後ろの名前, 画面を付けるノードのタイトル, UXML)。<paramref name="styleFile"/> は同じフォルダの USS のファイル名。
        /// </summary>
        internal abstract IEnumerable<(string Name, string Node, string Uxml)> GameScreens(string styleFile);

        /// <summary>ゲームのキーを足す（uGUI 版と同じ）。</summary>
        internal abstract void AddGameKeys(GameObject keys, NodeGraphAsset graph);

        /// <summary>画面の UXML のパス（例: <c>Snake Title.uxml</c>）。</summary>
        internal static string ScreenPath(NodeGraphAsset graph, string name) => $"{FolderOf(graph)}/{graph.name} {name}.uxml";

        /// <summary>画面の USS のパス。</summary>
        internal static string StylePath(NodeGraphAsset graph) => $"{FolderOf(graph)}/{graph.name}.uss";

        /// <summary>画面の Panel Settings のパス。</summary>
        internal static string PanelSettingsPath(NodeGraphAsset graph) => $"{FolderOf(graph)}/{graph.name} Panel Settings.asset";

        /// <summary>
        /// グラフの頭: Entry → Scene ノード <c>Main</c> ─(自動)→ State ノード <c>Title</c>。Title を返す（Start のイベントから先は呼ぶ側が繋ぐ）。
        /// </summary>
        internal static StateNode AddMainAndTitle(StarterGraphBuilder builder)
        {
            var main = builder.Add(new SceneNode { Title = MainSceneNode }, 250f, 0f);
            var title = builder.Add(new StateNode { Title = TitleNode, Description = "The title screen. Press Start or Enter." }, 750f, 0f);
            builder.Connect(builder.Entry, main);
            builder.Connect(main, builder.AddEvent(string.Empty, 500f, 0f, automatic: true), title);
            return title;
        }

        /// <summary>画面のファイル（USS・UXML・テーマ・Panel Settings）を作り、UXML をノードに入れる（グラフの保存は呼ぶ側）。</summary>
        internal override void CreateScreenAssets(NodeGraphAsset graph, string folder, List<string> created)
        {
            var styleFile = Path.GetFileName(StylePath(graph));
            var screens = new List<(string Name, string Node, string Uxml)>
            {
                ("Title", TitleNode, StarterScreens.Title(graph.name, Controls, StartEvent, styleFile, TitleHint)),
            };
            screens.AddRange(GameScreens(styleFile));

            // 画面を付けるノードが無ければ、何も書く前に失敗する
            foreach (var screen in screens)
            {
                FindScreenNode(graph, screen.Node);
            }

            WriteNew(StylePath(graph), StarterScreens.Style, created);
            foreach (var screen in screens)
            {
                WriteNew(ScreenPath(graph, screen.Name), screen.Uxml, created);
            }

            // テーマはフォルダに 1 つ（既にあれば、ほかの画面のものなので使うだけで消さない）
            var themePath = $"{folder}/Runtime Theme.tss";
            if (AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(themePath) == null)
            {
                ScreenAssets.FindOrCreateTheme(folder);
                created.Add(themePath);
            }

            ThrowIfExists(PanelSettingsPath(graph));
            ScreenAssets.CreatePanelSettings(PanelSettingsPath(graph));
            created.Add(PanelSettingsPath(graph));

            // ファイルがそろってから、ノードに入れる（途中で失敗しても、どのノードも消したファイルを指さない）
            foreach (var screen in screens)
            {
                var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(ScreenPath(graph, screen.Name));
                FindScreenNode(graph, screen.Node).Screen = tree != null
                    ? tree
                    : throw new InvalidOperationException($"The screen '{ScreenPath(graph, screen.Name)}' could not be loaded.");
            }
        }

        /// <summary>1 つのシーン <c>&lt;名前&gt;.unity</c> を作って Scene ノード <c>Main</c> に入れる。スクリプトは Graph Runner に付ける。</summary>
        internal override (string scenePath, string boardPath) BuildScenes(NodeGraphAsset graph, string folder, List<string> created)
        {
            var path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{graph.name}.unity");
            string runnerPath = null;
            StarterScenes.CreateScene(path, scene => runnerPath = StarterScenes.GetPath(BuildGame(scene, graph).transform), StarterName);
            created.Add(path);
            StarterScenes.SetScene(graph, MainSceneNode, path);
            return (path, runnerPath);
        }

        /// <summary>Scene ノード <c>Main</c> と、ノードの画面を空にする。</summary>
        internal override void Unassign(NodeGraphAsset graph)
        {
            StarterScenes.ClearScene(graph, MainSceneNode);
            foreach (var node in graph.Nodes.OfType<IHasScreen>())
            {
                node.Screen = null;
            }
        }

        /// <summary>
        /// ゲームのシーン: Main Camera、キー（Title の Enter で Start と、ゲームのキー）、このグラフの Graph Runner
        /// （ノードに画面があるので UIDocument と Graph Screen Host 付き。Panel Settings はこの Starter のもの）。Graph Runner の GameObject を返す。
        /// </summary>
        internal override GameObject BuildGame(Scene scene, NodeGraphAsset graph)
        {
            StarterScenes.AddCamera(scene);
            var keys = StarterScenes.CreateRoot(scene, "Keys");
            StarterScenes.AddKey(keys, graph, KeyCode.Return, StartEvent);
            AddGameKeys(keys, graph);

            var runner = PlaySetup.CreateRunner(scene, graph);
            var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath(graph));
            if (!runner.TryGetComponent<UIDocument>(out var document) || settings == null)
            {
                throw new InvalidOperationException($"The Graph Runner has no UI Document, or the panel settings '{PanelSettingsPath(graph)}' could not be loaded.");
            }

            document.panelSettings = settings;
            return runner.gameObject;
        }

        // 画面を付けるノード（タイトルで探す）。無ければ例外
        private static IHasScreen FindScreenNode(NodeGraphAsset graph, string title) =>
            graph.Nodes.FirstOrDefault(node => node is IHasScreen && node.Title == title) as IHasScreen
            ?? throw new InvalidOperationException($"The graph has no node '{title}' to put a screen on.");

        // ユーザーのファイルを上書きしないよう、同じ名前があれば作らずに失敗する（作ったものは呼ぶ側が消す）
        private static void WriteNew(string path, string content, List<string> created)
        {
            ThrowIfExists(path);
            File.WriteAllText(path, content);
            created.Add(path);
            AssetDatabase.ImportAsset(path);
        }

        private static void ThrowIfExists(string path)
        {
            if (File.Exists(path))
            {
                throw new InvalidOperationException($"'{path}' already exists, so the starter does not overwrite it.");
            }
        }

        private static string FolderOf(NodeGraphAsset graph) =>
            Path.GetDirectoryName(AssetDatabase.GetAssetPath(graph))?.Replace('\\', '/') ?? "Assets";
    }
}
