using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Editor.Views
{
    /// <summary>
    /// <see cref="NodeData"/> から対応する <see cref="NodeView"/> を生成する。
    /// 型 → View の対応は <see cref="CustomNodeViewAttribute"/> から自動登録される。
    /// </summary>
    public static class NodeViewFactory
    {
        private static Dictionary<Type, Type> _viewTypes;

        private static Dictionary<Type, Type> ViewTypes => _viewTypes ??= CollectViewTypes();

        /// <summary><paramref name="data"/> を表示する View を生成し、ポートまで構築して返す。</summary>
        public static NodeView Create(NodeData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var view = (NodeView)Activator.CreateInstance(ResolveViewType(data.GetType()));
            view.Initialize(data);
            return view;
        }

        /// <summary>
        /// ノード型に対応する View 型を返す。直接の登録が無ければ基底型をたどり、
        /// 見つからなければ <see cref="NodeView"/> を返す。
        /// </summary>
        public static Type ResolveViewType(Type nodeType)
        {
            for (var type = nodeType; type != null && type != typeof(object); type = type.BaseType)
            {
                if (ViewTypes.TryGetValue(type, out var viewType))
                {
                    return viewType;
                }
            }

            return typeof(NodeView);
        }

        private static Dictionary<Type, Type> CollectViewTypes()
        {
            var map = new Dictionary<Type, Type>();
            foreach (var viewType in TypeCache.GetTypesWithAttribute<CustomNodeViewAttribute>())
            {
                var nodeType = viewType.GetCustomAttribute<CustomNodeViewAttribute>().NodeType;
                if (viewType.IsAbstract || !typeof(NodeView).IsAssignableFrom(viewType)
                    || viewType.GetConstructor(Type.EmptyTypes) == null)
                {
                    Debug.LogWarning($"[VisualNodeEditor] {viewType} must be a non-abstract NodeView with a public parameterless constructor.");
                    continue;
                }

                if (nodeType == null || !typeof(NodeData).IsAssignableFrom(nodeType))
                {
                    Debug.LogWarning($"[VisualNodeEditor] {viewType} targets {nodeType}, which is not a NodeData type.");
                    continue;
                }

                if (map.TryGetValue(nodeType, out var existing))
                {
                    Debug.LogWarning($"[VisualNodeEditor] Both {existing} and {viewType} are registered for {nodeType}. Using {existing}.");
                    continue;
                }

                map.Add(nodeType, viewType);
            }

            return map;
        }
    }
}
