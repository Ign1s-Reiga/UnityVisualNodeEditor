using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Yarn.Unity;

namespace Reiga.VisualNodeEditor.YarnSpinner.Editor
{
    /// <summary>
    /// プロジェクトの Yarn プロジェクトのノード名と変数の初期値（Dialogue ノードの選択肢と検証に使う）。
    /// 集めた結果は覚えておき、プロジェクトのアセットが変わったら捨てる（編集のたびの検証で読み直さない）。
    /// </summary>
    internal static class YarnProjects
    {
        private static List<YarnProjectInfo> _all;

        static YarnProjects()
        {
            EditorApplication.projectChanged += () => _all = null;
        }

        /// <summary>プロジェクトのすべての Yarn プロジェクト（パスの順）。コンパイルに失敗しているものは、ノード名・変数が空。</summary>
        public static IReadOnlyList<YarnProjectInfo> All => _all ??= Load();

        /// <summary>覚えている結果を捨てる（アセットを作った直後など、projectChanged を待たずに読み直したいとき）。</summary>
        public static void Invalidate() => _all = null;

        /// <summary>どれかの Yarn プロジェクトにそのノードがあるか。</summary>
        public static bool HasNode(string nodeName) => All.Any(p => p.NodeNames.Contains(nodeName));

        /// <summary>ユーザーが書いたノードか（Yarn が内部で作るノードを選択肢に出さない）。</summary>
        public static bool IsUserNode(string nodeName) =>
            !string.IsNullOrEmpty(nodeName) && !nodeName.StartsWith("$", StringComparison.Ordinal)
                                            && nodeName.IndexOf("Yarn.Internal", StringComparison.Ordinal) < 0;

        private static List<YarnProjectInfo> Load()
        {
            var projects = new List<YarnProjectInfo>();
            foreach (var path in AssetDatabase.FindAssets("t:YarnProject").Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p, StringComparer.Ordinal))
            {
                var project = AssetDatabase.LoadAssetAtPath<YarnProject>(path);
                if (project == null)
                {
                    continue;
                }

                IReadOnlyList<string> nodes = Array.Empty<string>();
                IReadOnlyDictionary<string, IConvertible> variables = new Dictionary<string, IConvertible>();
                try
                {
                    nodes = project.NodeNames.Where(IsUserNode).OrderBy(n => n, StringComparer.Ordinal).ToList();
                    variables = project.InitialValues;
                }
                catch (Exception exception)
                {
                    // スクリプトにエラーがあって、まだプログラムが無い
                    Debug.Log($"[VisualNodeEditor] Could not read Yarn project '{path}': {exception.Message}");
                }

                projects.Add(new YarnProjectInfo(path, project.name, nodes, variables));
            }

            return projects;
        }
    }
}
