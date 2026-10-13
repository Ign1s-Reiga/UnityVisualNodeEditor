using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Editor.Scenes
{
    /// <summary>
    /// Scene ノードの画面（UI Toolkit の UXML）。シーンが使っている UXML を探して UI Builder で開く（Edit Screen）。
    /// 無ければ、シーンの隣に UXML を作り、それを出す UIDocument をシーンに置いて開く（Create Screen…）。画面の配置は UI Builder で行う。
    /// </summary>
    public static class SceneScreens
    {
        /// <summary>
        /// シーンが使っている UXML（パスの順）。保存したシーンの依存のうち <c>.uxml</c> と、シーンが開いていれば、その中の UIDocument の画面
        /// （保存前に足したものも見つける）。シーンが無ければ空。
        /// </summary>
        public static List<string> FindScreens(string scenePath)
        {
            if (string.IsNullOrEmpty(scenePath) || AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
            {
                return new List<string>();
            }

            var screens = AssetDatabase.GetDependencies(scenePath, false)
                .Where(path => path.EndsWith(".uxml", StringComparison.OrdinalIgnoreCase));
            var scene = SceneManager.GetSceneByPath(scenePath);
            if (scene.IsValid() && scene.isLoaded)
            {
                screens = screens.Concat(scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<UIDocument>(true))
                    .Where(document => document.visualTreeAsset != null)
                    .Select(document => AssetDatabase.GetAssetPath(document.visualTreeAsset))
                    .Where(path => !string.IsNullOrEmpty(path)));
            }

            return screens.Distinct().OrderBy(path => path, StringComparer.Ordinal).ToList();
        }

        /// <summary>UXML を開く（Unity の既定では UI Builder で開く）。</summary>
        public static void Open(string uxmlPath)
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
            if (tree != null)
            {
                AssetDatabase.OpenAsset(tree);
            }
        }

        /// <summary>
        /// <paramref name="scenePath"/> のシーンの画面を作る: シーンの隣に <c>&lt;シーン名&gt;.uxml</c>（見出しだけ）を作り、Panel Settings（プロジェクトにあればそれ、無ければシーンの隣に作る）
        /// で出す UIDocument（GameObject <c>UI</c>）をシーンに置く。シーンが開いていればそこに置いて未保存にし、開いていなければ追加で開いて保存して閉じる。
        /// 作った UXML のパスを返す。作れなければ null と理由（<paramref name="problem"/>）。
        /// Play 中・（シーンを追加で開くときに）未保存の無題のシーンが開いているときは、何も作らない。シーンに足せなければ、作った UXML を消して例外にする。
        /// </summary>
        public static string CreateScreen(string scenePath, out string problem)
        {
            problem = null;
            if (string.IsNullOrEmpty(scenePath) || AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
            {
                problem = "the scene is not set or no longer exists";
                return null;
            }

            // Play 中はシーンを書き換えない（開いていれば、足した物は Play を終えると消える）
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                problem = "Unity is in Play mode. Stop playing first";
                return null;
            }

            var scene = SceneManager.GetSceneByPath(scenePath);
            var opened = !scene.IsValid() || !scene.isLoaded;
            if (opened)
            {
                // 未保存の無題のシーンが開いていると、シーンを追加で開けない
                problem = ScenePicker.GetCreateProblem();
                if (problem != null)
                {
                    return null;
                }
            }

            // ファイルは確かめ終えてから書く
            var folder = Path.GetDirectoryName(scenePath)?.Replace('\\', '/') ?? "Assets";
            var sceneName = Path.GetFileNameWithoutExtension(scenePath);
            var uxmlPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{sceneName}.uxml");
            File.WriteAllText(uxmlPath, BuildDefaultScreen(sceneName));
            AssetDatabase.ImportAsset(uxmlPath);
            var placed = false;
            try
            {
                var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
                var settings = ScreenAssets.FindOrCreatePanelSettings(folder);
                if (opened)
                {
                    scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                }

                try
                {
                    ScreenAssets.AddDocument(scene, settings, tree);
                    if (opened)
                    {
                        EditorSceneManager.SaveScene(scene);
                    }
                    else
                    {
                        EditorSceneManager.MarkSceneDirty(scene);
                    }

                    placed = true;
                }
                finally
                {
                    if (opened && scene.IsValid())
                    {
                        EditorSceneManager.CloseScene(scene, true);
                    }
                }
            }
            finally
            {
                // シーンに置けなければ、どこからも使われない UXML を残さない
                if (!placed)
                {
                    AssetDatabase.DeleteAsset(uxmlPath);
                }
            }

            return uxmlPath;
        }

        /// <summary>新しい画面の UXML（中央に見出しだけ）。グラフに繋がるコントロールは UI Builder の Library から置く。</summary>
        public static string BuildDefaultScreen(string heading) =>
            "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\" xmlns:vne=\"Reiga.VisualNodeEditor\" editor-extension-mode=\"False\">\n" +
            "    <ui:VisualElement name=\"screen\" style=\"flex-grow: 1; align-items: center; justify-content: center;\">\n" +
            $"        <ui:Label name=\"heading\" text=\"{System.Security.SecurityElement.Escape(heading)}\" style=\"font-size: 72px; color: rgb(255, 255, 255);\" />\n" +
            "    </ui:VisualElement>\n" +
            "</ui:UXML>\n";
    }
}
