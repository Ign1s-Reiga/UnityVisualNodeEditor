using System;
using System.IO;
using System.Linq;
using Reiga.VisualNodeEditor.Editor.Build;
using Reiga.VisualNodeEditor.Editor.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Editor.Play
{
    /// <summary>
    /// ワンクリック Play。グラフのシーンを Build Settings に入れ、グラフが始まるシーンを開き、そこにこのグラフの Graph Runner が無ければ
    /// （確認してから）追加して保存し、Play に入る。シーンや Build Settings の並びを変える前には必ず確認する。
    /// ノードに画面（<see cref="IHasScreen"/>）があれば、Runner にそれを出す <see cref="GraphScreenHost"/>（と UIDocument）も付ける。
    /// </summary>
    public static class PlaySetup
    {
        /// <summary><see cref="GraphRunnerBehaviour"/> のグラフのフィールド名（EditMode テストで存在を検証している）。</summary>
        internal const string RunnerGraphPropertyName = "_graph";

        private const string DialogTitle = "Play";
        private const string KeepOrderKeyPrefix = "Reiga.VisualNodeEditor.KeepBuildOrder.";
        private const string SkipScreenHostKeyPrefix = "Reiga.VisualNodeEditor.SkipScreenHost.";

        /// <summary>
        /// Play の準備をして Play に入る。すでに Play 中なら Play を終える。
        /// 途中でユーザーが取り消したら何もせずに終わる。知らせることがあれば <paramref name="notify"/> に渡す。
        /// </summary>
        public static void Run(NodeGraphAsset graph, Action<string> notify)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.ExitPlaymode();
                return;
            }

            if (Prepare(graph, notify, "Add and Play"))
            {
                EditorApplication.EnterPlaymode();
            }
        }

        /// <summary>
        /// 問題一覧の Add Graph Runner…: Play と同じ準備（Build Settings、始まるシーンを開く、Graph Runner を確認して追加・保存）だけを行い、
        /// Play には入らない。準備できたら true。
        /// </summary>
        public static bool PrepareWithoutPlaying(NodeGraphAsset graph, Action<string> notify)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                notify?.Invoke("Stop Play first.");
                return false;
            }

            if (!Prepare(graph, notify, "Add"))
            {
                return false;
            }

            notify?.Invoke($"'{SceneManager.GetActiveScene().name}' has a Graph Runner for '{graph.name}'.");
            return true;
        }

        // シーンと Graph Runner を用意する。取り消し・失敗なら false
        private static bool Prepare(NodeGraphAsset graph, Action<string> notify, string addButtonLabel)
        {
            var graphPath = graph != null ? AssetDatabase.GetAssetPath(graph) : null;
            if (string.IsNullOrEmpty(graphPath))
            {
                notify?.Invoke("Open a saved Node Graph asset first.");
                return false;
            }

            // Runner を置いて開くシーン。グラフにシーンが無ければ、今開いているシーンを使う
            var start = PlayStart.FindStartScene(graph);
            var hostPath = start != null ? AssetDatabase.GUIDToAssetPath(start.Guid) : SceneManager.GetActiveScene().path;
            if (string.IsNullOrEmpty(hostPath))
            {
                notify?.Invoke(start != null
                    ? $"The scene '{start.Name}' was not found. Pick the scene again on its Scene node."
                    : "Save the open scene first, or add a Scene node to the graph.");
                return false;
            }

            if (!PrepareBuildSettings(graph, graphPath, start, hostPath))
            {
                return false;
            }

            if (SceneManager.GetActiveScene().path != hostPath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    return false;
                }

                EditorSceneManager.OpenScene(hostPath, OpenSceneMode.Single);
            }

            var scene = SceneManager.GetActiveScene();
            var runner = FindRunner(scene, graph);
            if (runner == null)
            {
                if (!EditorUtility.DisplayDialog(DialogTitle,
                        $"'{scene.name}' has no Graph Runner for '{graph.name}', so nothing would run.\n\n" +
                        $"Add one (a new GameObject named '{GetRunnerObjectName(graph)}') and save the scene?" +
                        (GraphScreens.AnyIn(graph) ? "\n\n" + DescribeScreenHost(graph, "It also gets") : string.Empty),
                        addButtonLabel, "Cancel"))
                {
                    return false;
                }

                AddRunner(scene, graph);
                EditorSceneManager.SaveScene(scene);
                return true;
            }

            return PrepareScreenHost(graph, graphPath, scene, runner);
        }

        // 前からある Runner には Graph Screen Host が付いていないことがある。ノードに画面があるのに出すものが無ければ、付けるか尋ねる。
        // 「付けない」を選んだら、このセッションの間はそのグラフについて尋ねない（コードで出しているなど）
        private static bool PrepareScreenHost(NodeGraphAsset graph, string graphPath, Scene scene, GraphRunnerBehaviour runner)
        {
            var skipKey = SkipScreenHostKeyPrefix + AssetDatabase.AssetPathToGUID(graphPath);
            if (!GraphScreens.AnyIn(graph) || FindScreenHost(scene, graph) != null || SessionState.GetBool(skipKey, false))
            {
                return true;
            }

            switch (EditorUtility.DisplayDialogComplex(DialogTitle,
                        $"Nodes of '{graph.name}' have screens, but nothing in '{scene.name}' shows them.\n\n" +
                        $"Let '{runner.gameObject.name}' show them, and save the scene? {DescribeScreenHost(graph, "It gets")}",
                        "Add", "Cancel", "Don't Add"))
            {
                case 0:
                    AddScreenHost(runner.gameObject, graph, true);
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    return true;
                case 2:
                    SessionState.SetBool(skipKey, true);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>シーンの中で、<paramref name="graph"/> を動かす Graph Runner。無ければ null。</summary>
        internal static GraphRunnerBehaviour FindRunner(Scene scene, NodeGraphAsset graph) =>
            !scene.IsValid() || !scene.isLoaded
                ? null
                : scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<GraphRunnerBehaviour>(true))
                    .FirstOrDefault(runner => runner.Graph == graph);

        /// <summary>シーンに、<paramref name="graph"/> を動かす Graph Runner の GameObject を追加する（Undo 可。保存はしない）。</summary>
        internal static GraphRunnerBehaviour AddRunner(Scene scene, NodeGraphAsset graph)
        {
            var runner = CreateRunner(scene, graph);
            Undo.RegisterCreatedObjectUndo(runner.gameObject, "Add Graph Runner");
            EditorSceneManager.MarkSceneDirty(scene);
            return runner;
        }

        /// <summary>
        /// シーンに、<paramref name="graph"/> を動かす Graph Runner の GameObject を作る。ノードに画面があれば、Graph Screen Host も付ける
        /// （<see cref="AddScreenHost"/>）。Undo にも残さず、未保存の印も付けない
        /// （<see cref="AddRunner"/> と、組み立ててすぐ保存して閉じるシーン（Starter）が使う）。
        /// </summary>
        internal static GraphRunnerBehaviour CreateRunner(Scene scene, NodeGraphAsset graph)
        {
            var gameObject = new GameObject(GetRunnerObjectName(graph));
            SceneManager.MoveGameObjectToScene(gameObject, scene);
            var runner = gameObject.AddComponent<GraphRunnerBehaviour>();
            var serializedRunner = new SerializedObject(runner);
            serializedRunner.FindProperty(RunnerGraphPropertyName).objectReferenceValue = graph;
            serializedRunner.ApplyModifiedPropertiesWithoutUndo();
            if (GraphScreens.AnyIn(graph))
            {
                AddScreenHost(gameObject, graph, false);
            }

            return runner;
        }

        /// <summary>
        /// 確認のダイアログに書く、Graph Screen Host を付けるときに足すもの。Panel Settings がプロジェクトに無ければ作ることも書く
        /// （作ったアセットは Undo では消えないので、先に知らせる）。
        /// </summary>
        internal static string DescribeScreenHost(NodeGraphAsset graph, string lead) =>
            $"{lead} a UI Document and a Graph Screen Host, which show the screens on the nodes." +
            (ScreenAssets.FindPanelSettings() == null
                ? $" The project has no Panel Settings, so 'Panel Settings.asset' (and a runtime theme) is created next to '{(graph != null ? graph.name : "the graph")}'."
                : string.Empty);

        /// <summary>シーンの中で、<paramref name="graph"/> の画面を出す Graph Screen Host（グラフが空のものも含む）。無ければ null。</summary>
        internal static GraphScreenHost FindScreenHost(Scene scene, NodeGraphAsset graph) =>
            !scene.IsValid() || !scene.isLoaded
                ? null
                : scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<GraphScreenHost>(true))
                    .FirstOrDefault(host => host.Graph == graph || host.Graph == null);

        /// <summary>
        /// <paramref name="gameObject"/> に、<paramref name="graph"/> のノードの画面を出す UIDocument と Graph Screen Host を付ける（あれば使う）。
        /// UIDocument に Panel Settings が無ければ、プロジェクトのもの（無ければグラフの隣に作る）を入れる。<paramref name="undo"/> なら Undo に残す。
        /// </summary>
        internal static GraphScreenHost AddScreenHost(GameObject gameObject, NodeGraphAsset graph, bool undo)
        {
            if (!gameObject.TryGetComponent<UIDocument>(out var document))
            {
                document = undo ? Undo.AddComponent<UIDocument>(gameObject) : gameObject.AddComponent<UIDocument>();
            }

            if (document.panelSettings == null)
            {
                var graphPath = graph != null ? AssetDatabase.GetAssetPath(graph) : null;
                var folder = string.IsNullOrEmpty(graphPath) ? "Assets" : Path.GetDirectoryName(graphPath)?.Replace('\\', '/') ?? "Assets";
                var settings = ScreenAssets.FindOrCreatePanelSettings(folder);
                if (undo)
                {
                    Undo.RecordObject(document, "Add Graph Screen Host");
                }

                document.panelSettings = settings;
            }

            if (!gameObject.TryGetComponent<GraphScreenHost>(out var host))
            {
                host = undo ? Undo.AddComponent<GraphScreenHost>(gameObject) : gameObject.AddComponent<GraphScreenHost>();
            }

            if (undo)
            {
                Undo.RecordObject(host, "Add Graph Screen Host");
            }

            host.Graph = graph;
            return host;
        }

        /// <summary>追加する Graph Runner の GameObject の名前。</summary>
        internal static string GetRunnerObjectName(NodeGraphAsset graph) => $"Graph Runner ({(graph != null ? graph.name : "Graph")})";

        /// <summary>
        /// グラフのシーンを Build Settings に入れ、始まるシーンが先頭でなければ先頭へ移すか尋ねる。取り消されたら false。
        /// 「並びはそのまま」を選んだら、このセッションの間はそのグラフについて尋ねない。
        /// </summary>
        private static bool PrepareBuildSettings(NodeGraphAsset graph, string graphPath, SceneReference start, string hostPath)
        {
            BuildSettingsSync.Apply(graph);
            if (start == null || BuildSettingsSync.IsFirst(EditorBuildSettings.scenes, start.Guid))
            {
                return true;
            }

            var keepOrderKey = KeepOrderKeyPrefix + AssetDatabase.AssetPathToGUID(graphPath);
            if (SessionState.GetBool(keepOrderKey, false))
            {
                return true;
            }

            switch (EditorUtility.DisplayDialogComplex(DialogTitle,
                        $"Builds start from the first scene in Build Settings, but '{start.Name}' (where this graph starts) is not first.\n\n" +
                        "Move it to the top? Play in the editor works either way.",
                        "Move to Top", "Cancel", "Keep Order"))
            {
                case 0:
                    EditorBuildSettings.scenes = BuildSettingsSync.MoveToFront(EditorBuildSettings.scenes, start.Guid, hostPath);
                    return true;
                case 2:
                    SessionState.SetBool(keepOrderKey, true);
                    return true;
                default:
                    return false;
            }
        }
    }
}
