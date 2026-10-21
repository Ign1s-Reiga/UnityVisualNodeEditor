namespace Reiga.VisualNodeEditor
{
    /// <summary>エッジが選ばれることの無い理由（<see cref="TransitionOrder"/>）。</summary>
    internal enum HiddenTransitionReason
    {
        /// <summary>前に、条件の無い自動の Event がある（自動の Event はエッジの順に調べ、満たす最初のものに進む）。</summary>
        AutomaticAfterUnconditioned,

        /// <summary>前に、同じ名前で条件の無い Raise の Event がある（Raise は名前の合う、条件を満たす最初の Event に進む）。</summary>
        RaiseAfterUnconditioned,

        /// <summary>前に、Event 以外のノードへのエッジがある（Advance と Entry は最初のものにしか進まない）。</summary>
        NotFirstNext,

        /// <summary>Event から出ている 2 本目以降のエッジ（Event は最初のエッジの先にしか進まない）。</summary>
        EventFollowsFirstEdge,

        /// <summary>条件が無く先へ進む自動の Event があるので、ノードに入るとすぐに出ていく（Raise・Advance は選ばれない）。</summary>
        LeavesRightAway,

        /// <summary>Entry は Event 以外の行き先があれば、待たずにそこへ進む（Entry から出ている Event は起こせない）。</summary>
        EntryGoesOn,
    }
}
