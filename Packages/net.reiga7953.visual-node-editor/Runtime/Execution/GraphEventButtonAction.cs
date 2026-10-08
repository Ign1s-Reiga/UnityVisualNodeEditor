namespace Reiga.VisualNodeEditor
{
    /// <summary><see cref="GraphEventButton"/> が押されたときに Runner へ送る操作。</summary>
    public enum GraphEventButtonAction
    {
        /// <summary>イベント名で遷移する（<see cref="GraphRunner.Raise"/>）。</summary>
        Raise,

        /// <summary>イベントを介さずに次のノードへ進む（<see cref="GraphRunner.Advance"/>）。</summary>
        Advance,
    }
}
