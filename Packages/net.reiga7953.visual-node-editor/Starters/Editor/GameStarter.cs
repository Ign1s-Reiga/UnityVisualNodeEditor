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
    /// ゲームの Starter（Snake・スライドパズル）の共通の流れ。選んでいるフォルダに、グラフ・ゲームのスクリプト・Title と Game のシーンを作り、
    /// スクリプトのコンパイルが終わったら Game のシーンの盤に付ける（<see cref="PendingStarterComponents"/>）。ツールバーの Play だけで最初から遊べる。
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

        /// <summary>Title に出す操作の説明。</summary>
        internal abstract string Controls { get; }

        /// <summary>ひな形のグラフ（まだアセットとして保存していない）。Scene ノードは <see cref="TitleSceneNode"/> と <see cref="GameSceneNode"/>。</summary>
        internal abstract NodeGraphAsset BuildGraph();

        /// <summary>Game のシーンを組み立て、ゲームのスクリプトを付ける盤を返す。</summary>
        internal abstract RectTransform BuildGame(Scene scene, NodeGraphAsset graph);

        /// <summary>
        /// メニューから: シーンを作れないとき（未保存の無題のシーン・Play 中）は、シーン無しで作るか尋ねる。作ったらグラフを開く。
        /// </summary>
        internal void CreateFromMenu()
        {
            var problem = ScenePicker.GetCreateProblem();
            if (problem != null && !EditorUtility.DisplayDialog(StarterName,
                    $"{problem}\n\nCreate the graph and the game script without the Title and Game scenes? " +
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
        /// <paramref name="withScenes"/> なら Title と Game のシーンも作ってグラフの Scene ノードに入れ、スクリプトのコンパイルが終わったら盤に付ける。
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
                    Debug.LogError($"[VisualNodeEditor] {StarterName} created the graph, but could not create the Title and Game scenes. " +
                                   $"Pick or create the scenes for the Scene nodes (the issue list has buttons for it), then add {className} to a board in your scene.");
                }
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

        // Title と Game のシーンを作ってグラフに入れ、Game のシーンのパスと盤のパスを返す。どちらかを作れなければ、作ったものを消して例外
        private (string scenePath, string boardPath) CreateScenes(NodeGraphAsset graph, string folder)
        {
            var created = new List<string>();
            try
            {
                var titlePath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{graph.name} Title.unity");
                StarterScenes.CreateScene(titlePath, scene => StarterScenes.BuildGameTitle(scene, graph, Controls, StartEvent), StarterName);
                created.Add(titlePath);

                var gamePath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{graph.name} Game.unity");
                string boardPath = null;
                StarterScenes.CreateScene(gamePath, scene => boardPath = StarterScenes.GetPath(BuildGame(scene, graph)), StarterName);
                created.Add(gamePath);

                StarterScenes.SetScene(graph, TitleSceneNode, titlePath);
                StarterScenes.SetScene(graph, GameSceneNode, gamePath);
                EditorUtility.SetDirty(graph);
                AssetDatabase.SaveAssetIfDirty(graph);
                return (gamePath, boardPath);
            }
            catch
            {
                foreach (var path in created)
                {
                    AssetDatabase.DeleteAsset(path);
                }

                // グラフを保存するところで失敗したときも、消したシーンを指したまま残さない
                StarterScenes.ClearScene(graph, TitleSceneNode);
                StarterScenes.ClearScene(graph, GameSceneNode);
                throw;
            }
        }
    }
}
