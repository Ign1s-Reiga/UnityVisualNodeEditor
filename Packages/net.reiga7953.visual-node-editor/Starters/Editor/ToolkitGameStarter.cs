using System;
using System.Collections.Generic;
using System.IO;
using Reiga.VisualNodeEditor.Editor.Play;
using Reiga.VisualNodeEditor.Editor.Scenes;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Starters.Editor
{
    /// <summary>
    /// ゲームの Starter の UI Toolkit 版。グラフ・キー・Title の Graph Runner は uGUI 版（<see cref="Game"/>）と同じで、画面だけを UI Toolkit にする:
    /// <c>&lt;名前&gt; Title.uxml</c> / <c>&lt;名前&gt; Game.uxml</c> / <c>&lt;名前&gt;.uss</c> と、その Panel Settings（<c>&lt;名前&gt; Panel Settings.asset</c> と <c>Runtime Theme.tss</c>）を作り、
    /// シーンには UIDocument（GameObject <c>UI</c>）を置く。ゲームのスクリプトは Game のシーンの <c>UI</c> に付ける。画面は UI Builder で直せる。
    /// </summary>
    internal abstract class ToolkitGameStarter : GameStarter
    {
        /// <summary>画面を出す GameObject の名前（ゲームのスクリプトもここに付ける）。</summary>
        internal const string DocumentObjectName = "UI";

        /// <summary>同じゲームの uGUI 版（グラフ・名前・操作の説明・キーを借りる）。</summary>
        internal abstract GameStarter Game { get; }

        internal override string BaseName => Game.BaseName;

        internal override string Controls => Game.Controls;

        internal override NodeGraphAsset BuildGraph() => Game.BuildGraph();

        /// <summary>Game の画面の中身（<see cref="StarterScreens.Screen"/> の <c>body</c>。行ごとに字下げ済みの UXML）。</summary>
        internal abstract string GameScreenBody(NodeGraphAsset graph);

        /// <summary>Game のシーンのキーを足す（uGUI 版と同じ）。</summary>
        internal abstract void AddGameKeys(GameObject keys, NodeGraphAsset graph);

        /// <summary>Title の画面の UXML のパス。</summary>
        internal static string TitleScreenPath(NodeGraphAsset graph) => $"{FolderOf(graph)}/{graph.name} Title.uxml";

        /// <summary>Game の画面の UXML のパス。</summary>
        internal static string GameScreenPath(NodeGraphAsset graph) => $"{FolderOf(graph)}/{graph.name} Game.uxml";

        /// <summary>画面の USS のパス。</summary>
        internal static string StylePath(NodeGraphAsset graph) => $"{FolderOf(graph)}/{graph.name}.uss";

        /// <summary>画面の Panel Settings のパス。</summary>
        internal static string PanelSettingsPath(NodeGraphAsset graph) => $"{FolderOf(graph)}/{graph.name} Panel Settings.asset";

        internal override void CreateScreenAssets(NodeGraphAsset graph, string folder, List<string> created)
        {
            var styleFile = Path.GetFileName(StylePath(graph));
            WriteNew(StylePath(graph), StarterScreens.Style, created);
            WriteNew(TitleScreenPath(graph), StarterScreens.Title(graph.name, Controls, StartEvent, styleFile), created);
            WriteNew(GameScreenPath(graph), StarterScreens.Screen(styleFile, GameScreenBody(graph)), created);

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
        }

        /// <summary>Title: Main Camera、Title の画面の UIDocument、Return / Space で Start の Graph Event Key、このグラフの Graph Runner。</summary>
        internal override void BuildTitle(Scene scene, NodeGraphAsset graph)
        {
            StarterScenes.AddCamera(scene);
            AddDocument(scene, graph, TitleScreenPath(graph));
            var keys = StarterScenes.CreateRoot(scene, "Keys");
            StarterScenes.AddKey(keys, graph, KeyCode.Return, StartEvent);
            StarterScenes.AddKey(keys, graph, KeyCode.Space, StartEvent);
            PlaySetup.CreateRunner(scene, graph);
        }

        /// <summary>Game: Main Camera、Game の画面の UIDocument（ゲームのスクリプトを付ける）、キー。</summary>
        internal override GameObject BuildGame(Scene scene, NodeGraphAsset graph)
        {
            StarterScenes.AddCamera(scene);
            var document = AddDocument(scene, graph, GameScreenPath(graph));
            AddGameKeys(StarterScenes.CreateRoot(scene, "Keys"), graph);
            return document.gameObject;
        }

        private static UIDocument AddDocument(Scene scene, NodeGraphAsset graph, string screenPath)
        {
            var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath(graph));
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(screenPath);
            if (settings == null || tree == null)
            {
                throw new InvalidOperationException($"The screen '{screenPath}' or its panel settings could not be loaded.");
            }

            return ScreenAssets.AddDocument(scene, settings, tree, DocumentObjectName);
        }

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
