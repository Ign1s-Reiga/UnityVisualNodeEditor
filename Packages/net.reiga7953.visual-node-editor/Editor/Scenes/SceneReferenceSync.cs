using System;
using Reiga.VisualNodeEditor.Editor.Inspector;
using UnityEditor;

namespace Reiga.VisualNodeEditor.Editor.Scenes
{
    /// <summary>
    /// グラフ内のすべての <see cref="SceneReference"/> の Path を、GUID から引き直して最新にする。
    /// Runtime は Path（とそこから導く Name）でシーンを読むため、シーンの移動・改名後に古い Path が残らないようにする。
    /// </summary>
    public static class SceneReferenceSync
    {
        /// <summary>
        /// <paramref name="graph"/> 内の参照を更新する。GUID から Path が引けない（シーンが削除された）参照は変更しない。
        /// Undo には積まない。変更があれば true を返す（呼び出し側で SetDirty / 保存すること）。
        /// </summary>
        /// <param name="guidToPath">GUID からプロジェクト相対パスを返す関数。通常は <see cref="AssetDatabase.GUIDToAssetPath(string)"/>。</param>
        public static bool Resync(NodeGraphAsset graph, Func<string, string> guidToPath)
        {
            if (graph == null)
            {
                return false;
            }

            using var serializedObject = new SerializedObject(graph);
            var changed = false;
            var property = serializedObject.GetIterator();
            while (property.Next(true))
            {
                if (property.type != nameof(SceneReference))
                {
                    continue;
                }

                var guid = property.FindPropertyRelative(SceneReferenceDrawer.GuidPropertyName)?.stringValue;
                var path = property.FindPropertyRelative(SceneReferenceDrawer.PathPropertyName);
                if (string.IsNullOrEmpty(guid) || path == null)
                {
                    continue;
                }

                var currentPath = guidToPath(guid);
                if (!string.IsNullOrEmpty(currentPath) && currentPath != path.stringValue)
                {
                    path.stringValue = currentPath;
                    changed = true;
                }
            }

            if (changed)
            {
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }

            return changed;
        }
    }
}
