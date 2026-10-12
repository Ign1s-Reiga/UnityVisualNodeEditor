namespace Reiga.VisualNodeEditor
{
    /// <summary><see cref="ParameterChange"/> の操作。</summary>
    public enum ParameterChangeOperation
    {
        /// <summary>値を入れる（どの型でも）。</summary>
        Set,

        /// <summary>今の値に足す（Int・Float だけ）。引くときは負の数を足す。</summary>
        Add,
    }
}
