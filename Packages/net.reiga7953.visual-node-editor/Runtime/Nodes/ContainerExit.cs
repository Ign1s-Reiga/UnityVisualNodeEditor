using System;
using UnityEngine;

namespace Reiga.VisualNodeEditor
{
    /// <summary>
    /// コンテナ（<see cref="ContainerNode"/>）の出口 1 つ。
    /// <see cref="Id"/> はコンテナの出力ポートの ID としてエッジに保存されるので、作成後は変えない。
    /// <see cref="Name"/> はポートの表示名で、自由に変えてよい（エッジは壊れない）。
    /// </summary>
    [Serializable]
    public sealed class ContainerExit
    {
        [SerializeField] private string _id = Guid.NewGuid().ToString("N");
        [SerializeField] private string _name;

        public ContainerExit()
        {
        }

        public ContainerExit(string name)
        {
            _name = name;
        }

        /// <summary>出口の ID（出力ポートの ID）。作成後は変わらない。コンテナの中でだけ意味を持つ。</summary>
        public string Id => _id;

        /// <summary>表示名（出力ポートのラベル、Exit ノードのタイトル）。</summary>
        public string Name
        {
            get => _name;
            set => _name = value;
        }
    }
}
