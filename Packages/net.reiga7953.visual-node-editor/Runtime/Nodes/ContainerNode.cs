using System;
using System.Collections.Generic;
using UnityEngine;

namespace Reiga.VisualNodeEditor
{
    /// <summary>
    /// 他のノードを含むノード（サブグラフ）。子は子の <see cref="NodeData.ParentId"/> で表し、このノードは子の一覧を持たない。
    /// 中にはちょうど 1 つの <see cref="ContainerEntryNode"/> と、出口へ向かう <see cref="ContainerExitNode"/> を置く。
    /// ポートは入力 1 つと、<see cref="Exits"/> の並び順に出口ごとの出力 1 つ（ポート ID = 出口の ID）。
    /// 初期の出口を変えたいときはサブクラスで <see cref="DefaultExitNames"/> を上書きする。
    /// </summary>
    [Serializable]
    [NodeMenu("Flow/Container")]
    public class ContainerNode : NodeData
    {
        /// <summary>入力ポートの ID。</summary>
        public const string InputPortId = "in";

        // 出口の一覧はエディタの専用 UI で編集する（生のリストの「+」は出口の ID ごと複製してしまうため隠す）
        [SerializeField, HideInInspector] private List<ContainerExit> _exits = new();

        public ContainerNode()
        {
            // 既定の出口は作成時にだけ使う。以後の出口はインスタンスごとのデータ
            foreach (var name in DefaultExitNames ?? Array.Empty<string>())
            {
                _exits.Add(new ContainerExit(name));
            }
        }

        /// <summary>出口（出力ポートの並び順）。</summary>
        public IReadOnlyList<ContainerExit> Exits => _exits;

        /// <summary>作成時に作る出口の名前。既定は <c>{ "Next" }</c>。サブクラスで上書きできる。</summary>
        protected virtual IEnumerable<string> DefaultExitNames => new[] { "Next" };

        protected override string DefaultTitle => "Container";

        /// <summary>ID が一致する出口。無ければ null。</summary>
        public ContainerExit FindExit(string exitId) => _exits.Find(e => e != null && e.Id == exitId);

        /// <summary>名前が一致する出口を探す（大文字小文字を区別する）。テンプレートやゲームのコードから出口を引くのに使う。</summary>
        public bool TryGetExit(string name, out ContainerExit exit)
        {
            exit = name == null ? null : _exits.Find(e => e != null && string.Equals(e.Name, name, StringComparison.Ordinal));
            return exit != null;
        }

        /// <summary>出口を末尾に追加して返す。</summary>
        public ContainerExit AddExit(string name)
        {
            var exit = new ContainerExit(name);
            _exits.Add(exit);
            return exit;
        }

        /// <summary>出口を改名する（ID は変わらないのでエッジは壊れない）。見つからなければ false。</summary>
        public bool RenameExit(string exitId, string name)
        {
            var exit = FindExit(exitId);
            if (exit == null)
            {
                return false;
            }

            exit.Name = name;
            return true;
        }

        /// <summary>出口を <paramref name="index"/>（移動後の一覧での位置。範囲外は端に寄せる）へ移す。見つからなければ false。</summary>
        public bool MoveExit(string exitId, int index)
        {
            var exit = FindExit(exitId);
            if (exit == null)
            {
                return false;
            }

            _exits.Remove(exit);
            _exits.Insert(Mathf.Clamp(index, 0, _exits.Count), exit);
            return true;
        }

        /// <summary>
        /// 出口を一覧から外す。出力ポートのエッジも消すには <see cref="NodeGraphAsset.RemoveContainerExit"/> を使う。
        /// 見つからなければ false。
        /// </summary>
        public bool RemoveExit(string exitId)
        {
            var exit = FindExit(exitId);
            return exit != null && _exits.Remove(exit);
        }
    }
}
