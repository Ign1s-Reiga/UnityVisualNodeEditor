using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Editor.Scenes
{
    /// <summary>
    /// UI Toolkit の画面に要るアセット（Panel Settings と、その既定のテーマ）と、シーンへの UIDocument の置き方
    /// （Scene ノードの Create Screen… と、ゲームの Starter の UI Toolkit 版が使う）。
    /// </summary>
    public static class ScreenAssets
    {
        /// <summary>Unity の既定のランタイムのテーマ（Panel Settings を Create メニューで作るときに Unity が書くものと同じ中身）。</summary>
        public const string DefaultThemeContent = "@import url(\"unity-theme://default\");\nVisualElement {}\n";

        /// <summary>画面の大きさの基準（uGUI の Starter の Canvas と同じ）。</summary>
        public static readonly Vector2Int ReferenceResolution = new Vector2Int(1920, 1080);

        /// <summary>プロジェクトにある Panel Settings（パスの順で最初のもの）。無ければ null。</summary>
        public static PanelSettings FindPanelSettings() =>
            AssetDatabase.FindAssets("t:PanelSettings")
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, System.StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<PanelSettings>)
                .FirstOrDefault(settings => settings != null);

        /// <summary>プロジェクトにある Panel Settings。無ければ <paramref name="folder"/> に作る（<see cref="CreatePanelSettings"/>）。</summary>
        public static PanelSettings FindOrCreatePanelSettings(string folder) =>
            FindPanelSettings() ?? CreatePanelSettings(AssetDatabase.GenerateUniqueAssetPath($"{folder}/Panel Settings.asset"));

        /// <summary>
        /// <paramref name="path"/> に Panel Settings を作る。1920 × 1080 を基準に拡大縮小し（幅と高さの中間で合わせる）、テーマは同じフォルダの
        /// <c>Runtime Theme.tss</c>（無ければ Unity の既定のテーマで作る）。
        /// </summary>
        public static PanelSettings CreatePanelSettings(string path)
        {
            var folder = Path.GetDirectoryName(path)?.Replace('\\', '/') ?? "Assets";
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = ReferenceResolution;
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            settings.themeStyleSheet = FindOrCreateTheme(folder);
            AssetDatabase.CreateAsset(settings, path);
            AssetDatabase.SaveAssetIfDirty(settings);
            return settings;
        }

        /// <summary><paramref name="folder"/> の <c>Runtime Theme.tss</c>。無ければ Unity の既定のテーマで作る。</summary>
        public static ThemeStyleSheet FindOrCreateTheme(string folder)
        {
            var path = $"{folder}/Runtime Theme.tss";
            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(path);
            if (theme != null)
            {
                return theme;
            }

            File.WriteAllText(path, DefaultThemeContent);
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(path);
        }

        /// <summary><paramref name="scene"/> の一番上に、<paramref name="tree"/> の画面を出す UIDocument（GameObject <paramref name="name"/>）を置く。</summary>
        public static UIDocument AddDocument(Scene scene, PanelSettings settings, VisualTreeAsset tree, string name = "UI")
        {
            var documentObject = new GameObject(name);
            SceneManager.MoveGameObjectToScene(documentObject, scene);
            var document = documentObject.AddComponent<UIDocument>();
            document.panelSettings = settings;
            document.visualTreeAsset = tree;
            return document;
        }
    }
}
