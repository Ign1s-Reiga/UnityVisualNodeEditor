using System;
using System.Linq;
using Reiga.VisualNodeEditor.Editor.Build;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Reiga.VisualNodeEditor.Editor.Play
{
    /// <summary>
    /// ワンクリック Play。グラフのシーンを Build Settings に入れ、グラフが始まるシーンを開き、そこにこのグラフの Graph Runner が無ければ
    /// （確認してから）追加して保存し、Play に入る。シーンや Build Settings の並びを変える前には必ず確認する。
    /// </summary>
    public static class PlaySetup
    {
        /// <summary><see cref="GraphRunnerBehaviour"/> のグラフのフィールド名（EditMode テストで存在を検証している）。</summary>
        internal const string RunnerGraphPropertyName = "_graph";

        private const string DialogTitle = "Play";
        private const string KeepOrderKeyPrefix = "Reiga.VisualNodeEditor.KeepBuildOrder.";

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

            var graphPath = graph != null ? AssetDatabase.GetAssetPath(graph) : null;
            if (string.IsNullOrEmpty(graphPath))
            {
                notify?.Invoke("Open a saved Node Graph asset first.");
                return;
            }

            // Runner を置いて開くシーン。グラフにシーンが無ければ、今開いているシーンを使う
            var start = PlayStart.FindStartScene(graph);
            var hostPath = start != null ? AssetDatabase.GUIDToAssetPath(start.Guid) : SceneManager.GetActiveScene().path;
            if (string.IsNullOrEmpty(hostPath))
            {
                notify?.Invoke(start != null
                    ? $"The scene '{start.Name}' was not found. Pick the scene again on its Scene node."
                    : "Save the open scene first, or add a Scene node to the graph.");
                return;
            }

            if (!PrepareBuildSettings(graph, graphPath, start, hostPath))
            {
                return;
            }

            if (SceneManager.GetActiveScene().path != hostPath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    return;
                }

                EditorSceneManager.OpenScene(hostPath, OpenSceneMode.Single);
            }

            var scene = SceneManager.GetActiveScene();
            if (FindRunner(scene, graph) == null)
            {
                if (!EditorUtility.DisplayDialog(DialogTitle,
                        $"'{scene.name}' has no Graph Runner for '{graph.name}', so nothing would run.\n\n" +
                        $"Add one (a new GameObject named '{GetRunnerObjectName(graph)}') and save the scene?",
                        "Add and Play", "Cancel"))
                {
                    return;
                }

                AddRunner(scene, graph);
                EditorSceneManager.SaveScene(scene);
            }

            EditorApplication.EnterPlaymode();
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
            var gameObject = new GameObject(GetRunnerObjectName(graph));
            SceneManager.MoveGameObjectToScene(gameObject, scene);
            Undo.RegisterCreatedObjectUndo(gameObject, "Add Graph Runner");

            var runner = gameObject.AddComponent<GraphRunnerBehaviour>();
            var serializedRunner = new SerializedObject(runner);
            serializedRunner.FindProperty(RunnerGraphPropertyName).objectReferenceValue = graph;
            serializedRunner.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            return runner;
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
