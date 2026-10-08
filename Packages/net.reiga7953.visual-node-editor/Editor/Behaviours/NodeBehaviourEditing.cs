using System;
using UnityEditor;

namespace Reiga.VisualNodeEditor.Editor.Behaviours
{
    /// <summary>ノードの振る舞いの追加・削除・並べ替え。どれも 1 回の Undo で戻せる。</summary>
    public static class NodeBehaviourEditing
    {
        /// <summary>
        /// <paramref name="nodeId"/> のノードに <paramref name="type"/> の振る舞いを末尾に追加して返す。
        /// ノードが振る舞いを持てない・型が付けられない・<c>[Serializable]</c> が無く保存できないなら何もせず null。
        /// </summary>
        public static NodeBehaviour Add(NodeGraphAsset asset, string nodeId, Type type)
        {
            if (asset == null || !(asset.FindNode(nodeId) is IBehaviourHost host)
                || !NodeBehaviourCatalog.IsCreatable(type) || !NodeBehaviourCatalog.IsSaveable(type))
            {
                return null;
            }

            var behaviour = (NodeBehaviour)Activator.CreateInstance(type);
            Undo.RecordObject(asset, "Add Behaviour");
            host.AddBehaviour(behaviour);
            EditorUtility.SetDirty(asset);
            return behaviour;
        }

        /// <summary><paramref name="index"/> 番目の振る舞いを外す（読めなかったものも外せる）。外したら true。</summary>
        public static bool Remove(NodeGraphAsset asset, string nodeId, int index)
        {
            if (asset == null || !(asset.FindNode(nodeId) is IBehaviourHost host) || index < 0 || index >= host.Behaviours.Count)
            {
                return false;
            }

            Undo.RecordObject(asset, "Remove Behaviour");
            host.RemoveBehaviourAt(index);
            EditorUtility.SetDirty(asset);
            return true;
        }

        /// <summary><paramref name="index"/> 番目の振る舞いを <paramref name="newIndex"/>（移動後の位置）へ移す。動いたら true。</summary>
        public static bool Move(NodeGraphAsset asset, string nodeId, int index, int newIndex)
        {
            if (asset == null || !(asset.FindNode(nodeId) is IBehaviourHost host)
                || index < 0 || index >= host.Behaviours.Count || newIndex < 0 || newIndex >= host.Behaviours.Count
                || index == newIndex)
            {
                return false;
            }

            Undo.RecordObject(asset, "Reorder Behaviours");
            host.MoveBehaviour(index, newIndex);
            EditorUtility.SetDirty(asset);
            return true;
        }
    }
}
