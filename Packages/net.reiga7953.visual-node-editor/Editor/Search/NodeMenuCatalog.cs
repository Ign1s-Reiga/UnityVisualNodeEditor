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

                var path = type.GetCustomAttribute<NodeMenuAttribute>().Path?.Trim('/');
                if (!string.IsNullOrEmpty(path))
                {
                    items.Add(new NodeMenuItem(path, type));
                }
            }

            items.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));
            return items;
        }

        /// <summary>検索メニューから生成できるノード型かどうか。</summary>
        public static bool IsCreatable(Type type) =>
            typeof(NodeData).IsAssignableFrom(type)
            && !type.IsAbstract
            && !type.ContainsGenericParameters
            && type.GetConstructor(Type.EmptyTypes) != null;
    }
}
