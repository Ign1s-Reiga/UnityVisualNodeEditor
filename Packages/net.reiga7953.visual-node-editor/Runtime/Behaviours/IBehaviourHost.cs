using System.Collections.Generic;

namespace Reiga.VisualNodeEditor
{
    /// <summary>
    /// <see cref="NodeBehaviour"/> を持てるノード（<see cref="StateNode"/>・<see cref="SceneNode"/>）。
    /// 独自の待機ノードも、振る舞いのリストを <c>[SerializeReference] private List&lt;NodeBehaviour&gt; _behaviours</c> として持ち
    /// これを実装すれば、振る舞いを付けられる（エディタはこのフィールド名で編集欄を作る。<c>[HideInInspector]</c> は付けない）。
    /// </summary>
    public interface IBehaviourHost
    {
        /// <summary>付いている振る舞い（呼ばれる順）。型が削除・改名されて読めなかったものは null。</summary>
        IReadOnlyList<NodeBehaviour> Behaviours { get; }

        /// <summary>振る舞いを末尾に追加する。</summary>
        void AddBehaviour(NodeBehaviour behaviour);

        /// <summary><paramref name="index"/> 番目の振る舞いを外す（読めなかった null も外せる）。範囲外なら false。</summary>
        bool RemoveBehaviourAt(int index);

        /// <summary><paramref name="index"/> 番目の振る舞いを <paramref name="newIndex"/>（移動後の位置）へ移す。範囲外なら false。</summary>
        bool MoveBehaviour(int index, int newIndex);
    }
}
