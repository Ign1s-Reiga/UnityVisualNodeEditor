namespace Reiga.VisualNodeEditor.YarnSpinner
{
    /// <summary>
    /// 会話を流すもの（<see cref="DialogueFlow"/> から見た Yarn の DialogueRunner）。テストでは偽物に差し替える。
    /// 1 回の <see cref="Start"/> に対して、会話が終わったときも <see cref="Stop"/> で止めたときも、終わりの知らせがちょうど 1 回来る前提。
    /// </summary>
    internal interface IDialoguePlayer
    {
        /// <summary><paramref name="yarnNode"/> から会話を始める。</summary>
        void Start(string yarnNode);

        /// <summary>流している会話を止める。</summary>
        void Stop();
    }
}
