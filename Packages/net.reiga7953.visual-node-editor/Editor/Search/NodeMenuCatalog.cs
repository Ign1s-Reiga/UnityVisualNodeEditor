using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;

namespace Reiga.VisualNodeEditor.Editor.Search
{
    /// <summary><see cref="NodeMenuAttribute"/> が付いた生成可能なノード型を列挙する。</summary>
    public static class NodeMenuCatalog
    {
        /// <summary>メニューに出すべき全項目をパス順（序数比較）で返す。</summary>
        public static List<NodeMenuItem> GetItems()
        {
            var items = new List<NodeMenuItem>();
            foreach (var type in TypeCache.GetTypesWithAttribute<NodeMenuAttribute>())
            {
                if (!IsCreatable(type))
                {
                    continue;
                }

                var attribute = type.GetCustomAttribute<NodeMenuAttribute>();
                var path = attribute.Path?.Trim('/');
                // Hidden はカテゴリの色分けだけに使うノード（コンテナの Entry などエディタが自動で作るもの）
                if (!attribute.Hidden && !string.IsNullOrEmpty(path))
                {
                    items.Add(new NodeMenuItem(path, type));
                }
            }

            items.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));
            return items;
        }

        /// <summary>
        /// 表示中の階層（<paramref name="insideContainer"/> = コンテナの中か）で作ってよいノード型か。
        /// ルートの <see cref="EntryNode"/> はルートだけ、コンテナの Entry / Exit はコンテナの中だけ（検証の条件 3）。
        /// </summary>
        public static bool IsAvailableAt(Type type, bool insideContainer)
        {
            if (typeof(EntryNode).IsAssignableFrom(type))
            {
                return !insideContainer;
            }

            if (typeof(ContainerEntryNode).IsAssignableFrom(type) || typeof(ContainerExitNode).IsAssignableFrom(type))
            {
                return insideContainer;
            }

            return true;
        }

        /// <summary>表示中の階層で作れる項目だけを、<see cref="GetItems()"/> と同じ順で返す。</summary>
        public static List<NodeMenuItem> GetItems(bool insideContainer) =>
            GetItems().FindAll(item => IsAvailableAt(item.NodeType, insideContainer));

        /// <summary>検索メニューから生成できるノード型かどうか。</summary>
        public static bool IsCreatable(Type type) =>
            typeof(NodeData).IsAssignableFrom(type)
            && !type.IsAbstract
            && !type.ContainsGenericParameters
            && type.GetConstructor(Type.EmptyTypes) != null;
    }
}
