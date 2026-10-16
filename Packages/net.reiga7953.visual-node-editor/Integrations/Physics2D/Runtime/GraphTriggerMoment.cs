namespace Reiga.VisualNodeEditor.Physics2D
{
    /// <summary><see cref="GraphTrigger2D"/> がいつ起こすか。</summary>
    public enum GraphTriggerMoment
    {
        /// <summary>触れたとき（入ったとき・ぶつかったとき）。</summary>
        Enter,

        /// <summary>離れたとき（出たとき）。</summary>
        Exit,
    }
}
