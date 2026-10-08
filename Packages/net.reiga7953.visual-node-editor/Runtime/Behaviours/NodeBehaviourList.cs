using System;
using System.Collections.Generic;
using UnityEngine;

namespace Reiga.VisualNodeEditor
{
    /// <summary><see cref="IBehaviourHost"/> の実装で共通の、振る舞いのリストの操作。</summary>
    internal static class NodeBehaviourList
    {
        public static void Add(List<NodeBehaviour> list, NodeBehaviour behaviour)
        {
            if (behaviour == null)
            {
                throw new ArgumentNullException(nameof(behaviour));
            }

            list.Add(behaviour);
        }

        public static bool RemoveAt(List<NodeBehaviour> list, int index)
        {
            if (index < 0 || index >= list.Count)
            {
                return false;
            }

            list.RemoveAt(index);
            return true;
        }

        public static bool Move(List<NodeBehaviour> list, int index, int newIndex)
        {
            if (index < 0 || index >= list.Count)
            {
                return false;
            }

            var behaviour = list[index];
            list.RemoveAt(index);
            list.Insert(Mathf.Clamp(newIndex, 0, list.Count), behaviour);
            return true;
        }
    }
}
