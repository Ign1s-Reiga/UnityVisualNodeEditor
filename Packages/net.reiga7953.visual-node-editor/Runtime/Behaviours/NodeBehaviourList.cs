using System;
using System.Collections.Generic;
using UnityEngine;

namespace Reiga.VisualNodeEditor
{
    /// <summary>
    /// <see cref="IBehaviourHost"/> の実装で共通の、振る舞いのリストの操作。独自の待機ノード（他のアセンブリのものも）が使う。
    /// </summary>
    public static class NodeBehaviourList
    {
        /// <summary>振る舞いを末尾に足す。</summary>
        /// <exception cref="ArgumentNullException"><paramref name="behaviour"/> が null。</exception>
        public static void Add(List<NodeBehaviour> list, NodeBehaviour behaviour)
        {
            if (behaviour == null)
            {
                throw new ArgumentNullException(nameof(behaviour));
            }

            list.Add(behaviour);
        }

        /// <summary>振る舞いを外す。範囲外なら false。</summary>
        public static bool RemoveAt(List<NodeBehaviour> list, int index)
        {
            if (index < 0 || index >= list.Count)
            {
                return false;
            }

            list.RemoveAt(index);
            return true;
        }

        /// <summary>振る舞いを <paramref name="newIndex"/> へ動かす（範囲外なら端へ）。元の位置が範囲外なら false。</summary>
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
