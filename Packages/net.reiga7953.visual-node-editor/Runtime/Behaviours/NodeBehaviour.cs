using System;

namespace Reiga.VisualNodeEditor
{
    /// <summary>
    /// 待機ノード（State・Scene など <see cref="IBehaviourHost"/> を実装したノード）に付ける処理。Animator の StateMachineBehaviour に相当する。
    /// Runner がそのノードにいる間、<see cref="OnEnter"/> → 毎フレームの <see cref="OnUpdate"/> / <see cref="OnFixedUpdate"/> → <see cref="OnExit"/> の順に呼ばれる。
    /// <para>
    /// サブクラスにも <c>[Serializable]</c> を付けること（属性は継承されない）。public / <c>[SerializeField]</c> のフィールドは
    /// ノードのインスペクタで編集でき、グラフアセットに保存される。グラフはアセットなので、シーン上のオブジェクトはフィールドで参照できない。
    /// シーンのものは <see cref="Host"/> や <c>FindFirstObjectByType</c> から実行時に引く。
    /// </para>
    /// <para>
    /// インスタンスは Runner ごとに、<see cref="GraphRunner.Start"/> のたびにアセットの値から複製される。
    /// 実行中に書き換えたフィールドはアセットに戻らず、次の Start() でアセットの値に戻る。
    /// </para>
    /// </summary>
    [Serializable]
    public abstract class NodeBehaviour
    {
        /// <summary>この振る舞いを動かしている Runner。<see cref="OnEnter"/> より前は null。</summary>
        public GraphRunner Runner { get; private set; }

        /// <summary>この振る舞いが付いているノード。</summary>
        public NodeData Node { get; private set; }

        /// <summary>Runner を動かしているコンポーネント。<see cref="GraphRunner"/> を直接使っている場合は null。</summary>
        public GraphRunnerBehaviour Host => Runner?.Host;

        /// <summary>Runner がこのノードに入ったとき（<see cref="GraphRunner.NodeEntered"/> の前）。</summary>
        public virtual void OnEnter()
        {
        }

        /// <summary>Runner がこのノードにいる間、<see cref="GraphRunner.Update"/> のたびに呼ばれる。</summary>
        /// <param name="deltaTime">前のフレームからの経過時間（<c>Time.deltaTime</c>）。</param>
        public virtual void OnUpdate(float deltaTime)
        {
        }

        /// <summary>Runner がこのノードにいる間、<see cref="GraphRunner.FixedUpdate"/> のたびに呼ばれる。</summary>
        /// <param name="fixedDeltaTime">物理の更新間隔（<c>Time.fixedDeltaTime</c>）。</param>
        public virtual void OnFixedUpdate(float fixedDeltaTime)
        {
        }

        /// <summary>Runner がこのノードから出るとき・止まるとき（<see cref="GraphRunner.NodeExited"/> の前）。</summary>
        public virtual void OnExit()
        {
        }

        internal void Bind(GraphRunner runner, NodeData node)
        {
            Runner = runner;
            Node = node;
        }
    }
}
