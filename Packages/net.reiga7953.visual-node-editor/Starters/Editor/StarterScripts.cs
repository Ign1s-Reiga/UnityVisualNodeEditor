using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Reiga.VisualNodeEditor.Editor.Behaviours;
using UnityEditor;

namespace Reiga.VisualNodeEditor.Starters.Editor
{
    /// <summary>
    /// ゲームの Starter が書き出すスクリプト。ひな形（<c>Starters/Templates</c> の C#。そのままコンパイルできる形）のクラス名だけを置き換える。
    /// 書き出したスクリプトはユーザーのもの（名前空間無し。自由に書き換えてよい）。
    /// </summary>
    internal static class StarterScripts
    {
        /// <summary>ひな形のフォルダ（パッケージの中）。</summary>
        internal const string TemplateFolder = "Packages/net.reiga7953.visual-node-editor/Starters/Templates";

        /// <summary>
        /// ひな形の C# の中身。<paramref name="templateClass"/> のファイル（<c>SnakeGameTemplate.cs</c> など）を読む。見つからなければ例外。
        /// </summary>
        internal static string LoadTemplate(string templateClass)
        {
            var path = $"{TemplateFolder}/{templateClass}.cs";
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            var text = script != null ? script.text : File.Exists(path) ? File.ReadAllText(path) : null;
            if (string.IsNullOrEmpty(text))
            {
                throw new InvalidOperationException($"The starter template '{path}' was not found. Reinstall the Visual Node Editor package.");
            }

            return text;
        }

        /// <summary><paramref name="template"/> の中の <paramref name="templateClass"/>（語として）を <paramref name="className"/> に置き換える。</summary>
        internal static string Render(string template, string templateClass, string className) =>
            Regex.Replace(template, @"\b" + Regex.Escape(templateClass) + @"\b", className);

        /// <summary>
        /// グラフ名から作るクラス名（例: "Snake" → "SnakeGame"、"Snake 1" → "Snake1Game"、"Sliding Puzzle" → "SlidingPuzzleGame"）。
        /// <paramref name="isTaken"/> なら番号を足す（"SnakeGame2" …）。既定では、コンパイル済みの同じ名前のクラス、プロジェクトのどこかにある同じ名前のスクリプト
        /// （<see cref="IsScriptNameTaken"/>。まだコンパイルされていないものも）、<paramref name="folder"/> の同じ名前のファイルがあれば使わない。
        /// </summary>
        internal static string ChooseClassName(string graphName, string folder, Func<string, bool> isTaken = null)
        {
            isTaken ??= name => NodeBehaviourScriptCreator.IsClassNameTaken(name) || IsScriptNameTaken(name) || File.Exists($"{folder}/{name}.cs");
            var stem = NodeBehaviourScriptTemplate.ToClassName(graphName) + "Game";
            if (!isTaken(stem))
            {
                return stem;
            }

            for (var number = 2; ; number++)
            {
                var candidate = stem + number;
                if (!isTaken(candidate))
                {
                    return candidate;
                }
            }
        }

        /// <summary>
        /// プロジェクト（パッケージを含む）に、ファイル名が <paramref name="name"/> のスクリプトがあるか（大文字小文字は区別しない）。
        /// コンパイルが終わる前の、別のフォルダで作った Starter のスクリプトも見つける（同じクラス名にすると、コンパイルできなくなる）。
        /// </summary>
        internal static bool IsScriptNameTaken(string name) =>
            AssetDatabase.FindAssets($"{name} t:MonoScript")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Any(path => string.Equals(Path.GetFileNameWithoutExtension(path), name, StringComparison.OrdinalIgnoreCase));

        /// <summary>ひな形 <paramref name="templateClass"/> から、クラス名を <paramref name="className"/> にしたスクリプトの中身を作る。ひな形が無ければ例外。</summary>
        internal static string Generate(string templateClass, string className) => Render(LoadTemplate(templateClass), templateClass, className);

        /// <summary><paramref name="folder"/> に <paramref name="className"/>.cs として <paramref name="text"/> を書き出し、パスを返す（読み込みは呼ぶ側で行う）。</summary>
        internal static string Write(string folder, string className, string text)
        {
            var path = $"{folder}/{className}.cs";
            File.WriteAllText(path, text);
            return path;
        }
    }
}
