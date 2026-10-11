namespace Reiga.VisualNodeEditor.YarnSpinner
{
    /// <summary>
    /// 会話を流すもの（<see cref="DialogueFlow"/> から見た Yarn の DialogueRunner）。テストでは偽物に差し替える。
    /// 始めた 1 回の会話に対して、終わったときも <see cref="Stop"/> で止めたときも、終わりの知らせがちょうど 1 回来る前提。
    /// </summary>
    internal interface IDialoguePlayer
    {
        /// <summary>
        /// <paramref name="yarnNode"/> から会話を始める。始められない（Yarn プロジェクトが無い・そのノードが無い）なら何もせず false
        /// （その場合は終わりの知らせが来ない）。
        /// </summary>
        bool Start(string yarnNode);

        /// <summary>流している会話を止める。</summary>
        void Stop();

        /// <summary>
        /// 始めた会話の終わりの知らせがまだ来ていない（流している・止めている途中）か。
        /// 知らせを出し終えた、またはもう出せない（DialogueRunner が壊された）なら false。
        /// </summary>
        bool IsRunning { get; }
    }
}
