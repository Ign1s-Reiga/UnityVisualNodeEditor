using System;
using System.Collections.Generic;
using System.IO;
using Reiga.VisualNodeEditor.Editor;
using Reiga.VisualNodeEditor.Editor.Scenes;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Reiga.VisualNodeEditor.Starters.Editor
{
    /// <summary>
    /// ゲームの Starter（Snake・スライドパズル）の共通の流れ。選んでいるフォルダに、グラフ・ゲームのスクリプト・Title と Game のシーン（UI Toolkit 版は 1 つのシーン）を作り、
    /// スクリプトのコンパイルが終わったら Game のシーンの盤（UI Toolkit 版は Graph Runner）に付ける（<see cref="PendingStarterComponents"/>）。ツールバーの Play だけで最初から遊べる。
    /// 遊び（蛇の動き・タイルの入れ替え）はスクリプトに、流れ・画面の出し分け・スコア・レベルはグラフとゲーム向けのコンポーネントに置く。
    /// </summary>
    internal abstract class GameStarter
    {
        /// <summary>Title のシーンが入る Scene ノードのタイトル。</summary>
        internal const string TitleSceneNode = "Title";

        /// <summary>Game のシーンが入る Scene ノードのタイトル。</summary>
        internal const string GameSceneNode = "Game";

        /// <summary>Title の Start のボタン・キーで Raise するイベント名。</summary>
        internal const string StartEvent = "Start";

        /// <summary>メニューとメッセージに出す名前（例: "Snake Starter"）。</summary>
        internal abstract string StarterName { get; }

        /// <summary>グラフのファイル名の元（例: "Snake"。同じ名前があれば番号を付ける）。</summary>
        internal abstract string BaseName { get; }

        /// <summary>スクリプトのひな形のクラス名（<c>Starters/Templates</c> のファイル名と同じ）。</summary>
        internal abstract string TemplateClass { get; }

        /// <summary>作るシーンの呼び方（メッセージに使う。例: "the Title and Game scenes"）。</summary>
        internal virtual string ScenesName => "the Title and Game scenes";

        /// <summary>Title に出す操作の説明。</summary>
        internal abstract string Controls { get; }

        /// <summary>ひな形のグラフ（まだアセットとして保存していない）。Scene ノードは <see cref="TitleSceneNode"/> と <see cref="GameSceneNode"/>。</summary>
        internal abstract NodeGraphAsset BuildGraph();

        /// <summary>
        /// Game のシーン（UI Toolkit 版は、ただ 1 つのシーン）を組み立て、ゲームのスクリプトを付ける GameObject
        /// （uGUI 版は盤、UI Toolkit 版は画面を出す Graph Runner）を返す。
        /// </summary>
        internal abstract GameObject BuildGame(Scene scene, NodeGraphAsset graph);

        /// <summary>Title のシーンを組み立てる。既定は uGUI（<see cref="StarterScenes.BuildGameTitle"/>）。</summary>
        internal virtual void BuildTitle(Scene scene, NodeGraphAsset graph) => StarterScenes.BuildGameTitle(scene, graph, Controls, StartEvent);

        /// <summary>
        /// シーンより先に、画面に要るアセットを作る（UI Toolkit 版の UXML・USS・Panel Settings。uGUI 版は無し）。
        /// 作ったものは <paramref name="created"/> に足す（シーンを作れなかったときに一緒に消す）。
        /// </summary>
        internal virtual void CreateScreenAssets(NodeGraphAsset graph, string folder, List<string> created)
        {
        }

        /// <summary>
        /// メニューから: シーンを作れないとき（未保存の無題のシーン・Play 中）は、シーン無しで作るか尋ねる。作ったらグラフを開く。
        /// </summary>
        internal void CreateFromMenu()
        {
            var problem = ScenePicker.GetCreateProblem();
            if (problem != null && !EditorUtility.DisplayDialog(StarterName,
                    $"{problem}\n\nCreate the graph and the game script without {ScenesName}? " +
                    "You can pick or create the scenes for the Scene nodes later, and add the script to a board in your own scene.",
                    "Create Without Scenes", "Cancel"))
            {
                return;
            }

            NodeGraphAsset graph;
            try
            {
                graph = CreateIn(GetSelectedFolder(), problem == null);
            }
            catch (Exception exception)
            {
                // ひな形が読めないなど。何かを作る前に失敗しているので、理由を出して終える
                Debug.LogException(exception);
                EditorUtility.DisplayDialog(StarterName, $"{StarterName} could not create the game: {exception.Message}", "OK");
                return;
            }

            Selection.activeObject = graph;
            NodeGraphEditorWindow.Open(graph);
        }

        /// <summary>
        /// <paramref name="folder"/> にグラフと（<paramref name="writeScript"/> なら）ゲームのスクリプトを作り、保存したグラフを返す。
        /// <paramref name="withScenes"/> ならシーン（<see cref="BuildScenes"/>）も作ってグラフの Scene ノードに入れ、スクリプトのコンパイルが終わったら盤に付ける。
        /// シーンを作れなければ、作ったシーンを消してエラーを出す（グラフとスクリプトはそのまま使える）。
        /// スクリプトのひな形は何かを作る前に読む（読めなければ、何も作らずに例外。作りかけの物を残さない）。
        /// </summary>
        internal NodeGraphAsset CreateIn(string folder, bool withScenes, bool writeScript = true)
        {
            var graphPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{BaseName}.asset");
            var className = StarterScripts.ChooseClassName(Path.GetFileNameWithoutExtension(graphPath), folder);
            var script = writeScript ? StarterScripts.Generate(TemplateClass, className) : null;
            var graph = BuildGraph();
            AssetDatabase.CreateAsset(graph, graphPath);
            AssetDatabase.SaveAssets();

            string gameScenePath = null;
            string boardPath = null;
            if (withScenes)
            {
                try
                {
                    (gameScenePath, boardPath) = CreateScenes(graph, folder);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    Debug.LogError($"[VisualNodeEditor] {StarterName} created the graph, but could not create {ScenesName}. " +
                                   $"Pick or create the scenes for the Scene nodes (the issue list has buttons for it), then add {className} to a board in your scene.");
                }
            }
            else
            {
                // シーン無しでも、画面（UI Toolkit 版の UXML・USS・Panel Settings）は作る。自分のシーンに置けば、スクリプトをそのまま使える
                var created = new List<string>();
                try
                {
                    CreateScreenAssets(graph, folder, created);
                }
                catch (Exception exception)
                {
                    DeleteInReverse(created);
                    Unassign(graph);
                    Debug.LogException(exception);
                    Debug.LogError($"[VisualNodeEditor] {StarterName} created the graph, but could not create the screen files.");
                }

                // 画面をノードに入れていれば、それも保存する
                EditorUtility.SetDirty(graph);
                AssetDatabase.SaveAssetIfDirty(graph);
            }

            if (script != null)
            {
                // 付ける予定は、スクリプトを書けてから覚える（書けなければ、いつまでも付けられない）
                var scriptPath = StarterScripts.Write(folder, className, script);
                if (gameScenePath != null)
                {
                    PendingStarterComponents.Add(new PendingStarterComponent(gameScenePath, boardPath, className, AssetDatabase.AssetPathToGUID(graphPath)));
                }

                AssetDatabase.ImportAsset(scriptPath);
            }

            return graph;
        }

        /// <summary>選んでいるフォルダ（ファイルを選んでいればそのフォルダ。何も無ければ Assets）。</summary>
        internal static string GetSelectedFolder()
        {
            var path = Selection.activeObject != null ? AssetDatabase.GetAssetPath(Selection.activeObject) : string.Empty;
            if (string.IsNullOrEmpty(path))
            {
                return "Assets";
            }

            return AssetDatabase.IsValidFolder(path) ? path : Path.GetDirectoryName(path)?.Replace('\\', '/') ?? "Assets";
        }

        /// <summary>
        /// シーンを作ってグラフの Scene ノードに入れ、スクリプトを付けるシーンのパスと、その中の GameObject のパスを返す。作ったシーンは <paramref name="created"/> に足す。
        /// 既定は Title と Game の 2 つ（<see cref="BuildTitle"/> / <see cref="BuildGame"/>）。
        /// </summary>
        internal virtual (string scenePath, string boardPath) BuildScenes(NodeGraphAsset graph, string folder, List<string> created)
        {
            var titlePath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{graph.name} Title.unity");
            StarterScenes.CreateScene(titlePath, scene => BuildTitle(scene, graph), StarterName);
            created.Add(titlePath);

            var gamePath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{graph.name} Game.unity");
            string boardPath = null;
            StarterScenes.CreateScene(gamePath, scene => boardPath = StarterScenes.GetPath(BuildGame(scene, graph).transform), StarterName);
            created.Add(gamePath);

            StarterScenes.SetScene(graph, TitleSceneNode, titlePath);
            StarterScenes.SetScene(graph, GameSceneNode, gamePath);
            return (gamePath, boardPath);
        }

        /// <summary>
        /// 作ったものを消したとき、グラフがそれを指したまま残らないよう外す（<see cref="BuildScenes"/> と <see cref="CreateScreenAssets"/> が入れたもの）。
        /// 既定は Title と Game の Scene ノードを空にする。
        /// </summary>
        internal virtual void Unassign(NodeGraphAsset graph)
        {
            StarterScenes.ClearScene(graph, TitleSceneNode);
            StarterScenes.ClearScene(graph, GameSceneNode);
        }

        // 画面のアセットとシーンを作ってグラフに入れ、スクリプトを付けるシーンのパスと、その中の GameObject のパスを返す。
        // どれかを作れなければ、作ったものを消して例外
        private (string scenePath, string boardPath) CreateScenes(NodeGraphAsset graph, string folder)
        {
            var created = new List<string>();
            try
            {
                CreateScreenAssets(graph, folder, created);
                var result = BuildScenes(graph, folder, created);
                EditorUtility.SetDirty(graph);
                AssetDatabase.SaveAssetIfDirty(graph);
                return result;
            }
            catch
            {
                DeleteInReverse(created);

                // グラフを保存するところで失敗したときも、消したシーン・画面を指したまま残さない
                Unassign(graph);
                EditorUtility.SetDirty(graph);
                AssetDatabase.SaveAssetIfDirty(graph);
                throw;
            }
        }

        // 作った順の逆に消す（UXML より先に、それが読む USS を消すと、UXML の読み込み直しでエラーが出る）
        private static void DeleteInReverse(List<string> created)
        {
            for (var i = created.Count - 1; i >= 0; i--)
            {
                AssetDatabase.DeleteAsset(created[i]);
            }
        }
    }
}
