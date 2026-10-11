namespace Reiga.VisualNodeEditor
{
    /// <summary><see cref="TransitionCondition"/> の比較。使えるものはパラメータの型で決まる（<see cref="TransitionCondition.GetOperators"/>）。</summary>
    public enum ConditionOperator
    {
        /// <summary>Bool が true。</summary>
        IsTrue,

        /// <summary>Bool が false。</summary>
        IsFalse,

        /// <summary>等しい（Int・Float・String。String は大文字小文字を区別する）。</summary>
        Equal,

        /// <summary>等しくない（Int・Float・String）。</summary>
        NotEqual,

        /// <summary>より小さい（Int・Float）。</summary>
        Less,

        /// <summary>以下（Int・Float）。</summary>
        LessOrEqual,

        /// <summary>より大きい（Int・Float）。</summary>
        Greater,

        /// <summary>以上（Int・Float）。</summary>
        GreaterOrEqual,
    }
}
