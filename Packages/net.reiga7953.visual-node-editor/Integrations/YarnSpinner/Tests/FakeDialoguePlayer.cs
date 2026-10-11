using System.Collections.Generic;

namespace Reiga.VisualNodeEditor.YarnSpinner.Tests
{
    /// <summary>テスト用: 始めた会話と止めた回数を覚えるだけの <see cref="IDialoguePlayer"/>。</summary>
    internal sealed class FakeDialoguePlayer : IDialoguePlayer
    {
        public List<string> Started { get; } = new();

        public int StopCount { get; private set; }

        public void Start(string yarnNode) => Started.Add(yarnNode);

        public void Stop() => StopCount++;
    }
}
