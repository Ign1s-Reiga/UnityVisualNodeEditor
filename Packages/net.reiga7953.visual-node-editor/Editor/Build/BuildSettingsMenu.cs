using UnityEditor;

namespace Reiga.VisualNodeEditor.Editor.Build
{
    /// <summary>Project ビューで選んだグラフのシーンを Build Settings に追加するメニュー。</summary>
    internal static class BuildSettingsMenu
    {
        private const string MenuPath = "Assets/Visual Node Editor/Add Graph Scenes to Build Settings";

        [MenuItem(MenuPath)]
        private static void AddSelectedGraphScenes()
        {
            foreach (var graph in Selection.GetFiltered<NodeGraphAsset>(SelectionMode.Assets))
            {
                BuildSettingsSync.Apply(graph);
            }
        }

        [MenuItem(MenuPath, true)]
        private static bool CanAddSelectedGraphScenes() =>
            Selection.GetFiltered<NodeGraphAsset>(SelectionMode.Assets).Length > 0;
    }
}
