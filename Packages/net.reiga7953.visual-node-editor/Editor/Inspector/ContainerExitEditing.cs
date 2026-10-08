using System;
using System.Linq;
using UnityEditor;

namespace Reiga.VisualNodeEditor.Editor.Inspector
{
    /// <summary>
    /// コンテナの出口の追加・改名・並べ替え・削除。どれも 1 回の Undo で戻せる。
    /// インスペクタの UI（<see cref="ContainerExitListView"/>・<see cref="ContainerExitPicker"/>）から使う。
    /// 名前は前後の空白を除き、空や、同じコンテナの中での重複は受け付けない。
    /// </summary>
    public static class ContainerExitEditing
    {
        /// <summary>名前を指定せずに出口を追加したときの名前の元（"Exit", "Exit 2", …）。</summary>
        public const string DefaultExitName = "Exit";

        /// <summary>コンテナの中で重複しない出口の名前。<paramref name="baseName"/> が空いていればそのまま、無ければ番号を付ける。</summary>
        public static string GetUniqueName(ContainerNode container, string baseName = DefaultExitName)
        {
            baseName = string.IsNullOrWhiteSpace(baseName) ? DefaultExitName : baseName.Trim();
            if (container == null || !container.TryGetExit(baseName, out _))
            {
                return baseName;
            }

            for (var i = 2; ; i++)
            {
                var candidate = $"{baseName} {i}";
                if (!container.TryGetExit(candidate, out _))
                {
                    return candidate;
                }
            }
        }

        /// <summary>
        /// 出口 <paramref name="exitId"/> の名前を <paramref name="name"/> にしてよいか。よければ null、だめなら理由。
        /// 新しい出口なら <paramref name="exitId"/> に null を渡す。
        /// </summary>
        public static string ValidateName(ContainerNode container, string exitId, string name)
        {
            var trimmed = name?.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                return "Exit names cannot be empty.";
            }

            if (container != null && container.TryGetExit(trimmed, out var existing) && existing.Id != exitId)
            {
                return $"This container already has an exit named '{trimmed}'.";
            }

            return null;
        }

        /// <summary>出口を末尾に追加して返す。名前が無い・使えないときは重複しない名前を付ける。</summary>
        public static ContainerExit Add(NodeGraphAsset asset, ContainerNode container, string name = null)
        {
            if (asset == null || container == null)
            {
                return null;
            }

            var exitName = ValidateName(container, null, name) == null ? name.Trim() : GetUniqueName(container, name);
            Undo.RecordObject(asset, "Add Container Exit");
            var exit = container.AddExit(exitName);
            EditorUtility.SetDirty(asset);
            return exit;
        }

        /// <summary>出口を改名する。名前が使えない・出口が無ければ何もせず false。同じ名前なら何もせず true。</summary>
        public static bool Rename(NodeGraphAsset asset, ContainerNode container, string exitId, string name)
        {
            var exit = container?.FindExit(exitId);
            if (asset == null || exit == null || ValidateName(container, exitId, name) != null)
            {
                return false;
            }

            var trimmed = name.Trim();
            if (exit.Name == trimmed)
            {
                return true;
            }

            Undo.RecordObject(asset, "Rename Container Exit");
            container.RenameExit(exitId, trimmed);
            EditorUtility.SetDirty(asset);
            return true;
        }

        /// <summary>出口を <paramref name="index"/>（移動後の位置）へ移す。出力ポートの並びも変わる。出口が無ければ false。</summary>
        public static bool Move(NodeGraphAsset asset, ContainerNode container, string exitId, int index)
        {
            var exit = container?.FindExit(exitId);
            if (asset == null || exit == null)
            {
                return false;
            }

            if (container.Exits.ToList().IndexOf(exit) == index)
            {
                return true;
            }

            Undo.RecordObject(asset, "Reorder Container Exits");
            container.MoveExit(exitId, index);
            EditorUtility.SetDirty(asset);
            return true;
        }

        /// <summary>
        /// 出口を削除し、その出力ポートから出るエッジも削除する（指していた Exit ノードは残る）。
        /// エッジか Exit ノードに影響があるときは、<paramref name="confirm"/> に影響の一覧を渡し、false が返れば何もしない。
        /// 削除したら true。
        /// </summary>
        public static bool Remove(NodeGraphAsset asset, ContainerNode container, string exitId, Func<string, bool> confirm)
        {
            var removal = ContainerExitRemoval.Inspect(asset, container, exitId);
            if (removal == null)
            {
                return false;
            }

            if (!removal.IsEmpty && confirm != null && !confirm(removal.Describe(asset)))
            {
                return false;
            }

            Undo.RecordObject(asset, "Remove Container Exit");
            asset.RemoveContainerExit(container, exitId);
            EditorUtility.SetDirty(asset);
            return true;
        }

        /// <summary>Exit ノードが指す出口を変える。親のコンテナに無い出口なら何もせず false。</summary>
        public static bool SetExitNodeTarget(NodeGraphAsset asset, ContainerExitNode exitNode, string exitId)
        {
            if (asset == null || exitNode == null || !(asset.FindNode(exitNode.ParentId) is ContainerNode parent)
                || parent.FindExit(exitId) == null)
            {
                return false;
            }

            if (exitNode.ExitId == exitId)
            {
                return true;
            }

            Undo.RecordObject(asset, "Change Exit");
            exitNode.ExitId = exitId;
            EditorUtility.SetDirty(asset);
            return true;
        }

        /// <summary>
        /// Exit ノードの親のコンテナに新しい出口を追加し、Exit ノードがそれを指すようにする（1 回の Undo で戻せる）。
        /// 親がコンテナでなければ null。
        /// </summary>
        public static ContainerExit AddExitForExitNode(NodeGraphAsset asset, ContainerExitNode exitNode)
        {
            if (asset == null || exitNode == null || !(asset.FindNode(exitNode.ParentId) is ContainerNode parent))
            {
                return null;
            }

            Undo.RecordObject(asset, "Add Container Exit");
            var exit = parent.AddExit(GetUniqueName(parent));
            exitNode.ExitId = exit.Id;
            EditorUtility.SetDirty(asset);
            return exit;
        }
    }
}
