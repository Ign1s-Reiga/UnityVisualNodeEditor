using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Reiga.VisualNodeEditor.Editor.Scenes
{
    /// <summary>
    /// シーンを選ぶ・作るための共通処理（Scene の欄の Pick… / Create Scene…、問題一覧の Pick Scene…）。
    /// </summary>
    public static class ScenePicker
    {
        private const string CreateSceneLabel = "Create Scene…";
        private const string DefaultSceneName = "New Scene";

        /// <summary>選べるシーン（プロジェクトの Assets 以下。パスの順）。</summary>
        public static List<string> FindScenePaths() =>
            AssetDatabase.FindAssets("t:SceneAsset", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !string.IsNullOrEmpty(path))
                .Distinct()
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();

        /// <summary>メニューに出す名前（"Assets/" と拡張子を除いたパス。例: "Scenes/Title"）。</summary>
        public static string GetMenuLabel(string scenePath)
        {
            var path = scenePath ?? string.Empty;
            if (path.StartsWith("Assets/", StringComparison.Ordinal))
            {
                path = path.Substring("Assets/".Length);
            }

            return path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) ? path.Substring(0, path.Length - ".unity".Length) : path;
        }

        /// <summary>
        /// シーンの一覧と、最後に Create Scene… のメニューを <paramref name="anchor"/> の下に出す。選ぶか作ったら <paramref name="picked"/> を呼ぶ。
        /// </summary>
        /// <param name="suggestedName">Create Scene… の保存ダイアログに最初に入れる名前（空なら "New Scene"）。</param>
        public static void ShowMenu(Rect anchor, Action<SceneAsset> picked, string suggestedName = null)
        {
            var menu = new GenericMenu();
            var paths = FindScenePaths();
            if (paths.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("No scenes in this project yet"));
            }

            foreach (var path in paths)
            {
                menu.AddItem(new GUIContent(GetMenuLabel(path)), false, () => picked?.Invoke(AssetDatabase.LoadAssetAtPath<SceneAsset>(path)));
            }

            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent(CreateSceneLabel), false, () =>
            {
                var created = CreateScene(suggestedName);
                if (created != null)
                {
                    picked?.Invoke(created);
                }
            });
            menu.DropDown(anchor);
        }

        /// <summary>
        /// 保存先を尋ねて、空のシーン（Camera と Light）を作る。今開いているシーンは切り替えない。
        /// 作れないとき・取り消したときは null（作れない理由はダイアログで伝える）。
        /// </summary>
        public static SceneAsset CreateScene(string suggestedName = null)
        {
            var problem = GetCreateProblem(GetLoadedScenePaths(), EditorApplication.isPlayingOrWillChangePlaymode);
            if (problem != null)
            {
                EditorUtility.DisplayDialog(CreateSceneLabel, problem, "OK");
                return null;
            }

            var name = string.IsNullOrWhiteSpace(suggestedName) ? DefaultSceneName : suggestedName.Trim();
            var path = EditorUtility.SaveFilePanelInProject(CreateSceneLabel, name, "unity", "Save the new scene");
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Additive);
            try
            {
                if (!EditorSceneManager.SaveScene(scene, path))
                {
                    return null;
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }

            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
        }

        /// <summary>
        /// 新しいシーンを作れない理由。作れるなら null。
        /// Unity は、未保存の無題のシーンが開いていると追加でシーンを作れない。Play 中も作らない。
        /// </summary>
        public static string GetCreateProblem(IEnumerable<string> loadedScenePaths, bool isPlaying)
        {
            if (isPlaying)
            {
                return "Stop Play first, then create the scene.";
            }

            return (loadedScenePaths ?? Enumerable.Empty<string>()).Any(string.IsNullOrEmpty)
                ? "Save the open untitled scene first. Unity cannot create another scene while it is open."
                : null;
        }

        private static IEnumerable<string> GetLoadedScenePaths()
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                yield return SceneManager.GetSceneAt(i).path;
            }
        }

        /// <summary>シーンの名前として使える文字だけにする（Create Scene… の最初の名前）。</summary>
        public static string ToFileName(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return DefaultSceneName;
            }

            var invalid = Path.GetInvalidFileNameChars();
            var cleaned = new string(title.Trim().Select(c => invalid.Contains(c) ? '_' : c).ToArray());
            return cleaned.Length > 0 ? cleaned : DefaultSceneName;
        }
    }
}
