namespace Reiga.VisualNodeEditor
{
    /// <summary>前のエッジがあるために、選ばれることの無いエッジ（<see cref="TransitionOrder.FindHidden"/> の結果）。</summary>
    internal sealed class HiddenTransition
    {
        public HiddenTransition(EdgeData edge, NodeData target, NodeData chosenInstead, HiddenTransitionReason reason)
        {
            Edge = edge;
            Target = target;
            ChosenInstead = chosenInstead;
            Reason = reason;
        }

        /// <summary>選ばれないエッジ。</summary>
        public EdgeData Edge { get; }

        /// <summary>そのエッジの行き先。</summary>
        public NodeData Target { get; }

        /// <summary>代わりにいつも選ばれる、前のエッジの行き先。</summary>
        public NodeData ChosenInstead { get; }

        public HiddenTransitionReason Reason { get; }
    }
}
