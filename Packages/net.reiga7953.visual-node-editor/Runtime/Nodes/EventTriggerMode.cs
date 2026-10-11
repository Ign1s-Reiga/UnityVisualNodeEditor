namespace Reiga.VisualNodeEditor
{
    /// <summary><see cref="EventNode"/> がどうやって起こされるか。</summary>
    public enum EventTriggerMode
    {
        /// <summary>名前で起こされたとき（<see cref="GraphRunner.Raise"/>）に、条件を満たしていれば進む。</summary>
        Raise,

        /// <summary>
        /// 待機ノードにいる間に条件を満たしたら、自分で進む。調べるのは待機ノードに入り終えたときと、パラメータの値が変わったとき。
        /// <see cref="GraphRunner.Raise"/> では選ばれない。
        /// </summary>
        Automatic,
    }
}
