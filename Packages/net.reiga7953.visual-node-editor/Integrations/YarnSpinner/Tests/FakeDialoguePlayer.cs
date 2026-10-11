using System.Collections.Generic;

namespace Reiga.VisualNodeEditor.YarnSpinner.Tests
{
    /// <summary>テスト用: 始めた会話と止めた回数を覚えるだけの <see cref="IDialoguePlayer"/>。<see cref="Missing"/> のノードは始められない。</summary>
    internal sealed class FakeDialoguePlayer : IDialoguePlayer
    {
        public List<string> Started { get; } = new();

        /// <summary>Yarn に無いことにするノード。</summary>
        public HashSet<string> Missing { get; } = new();

        public int StopCount { get; private set; }

        public bool Start(string yarnNode)
        {
            if (Missing.Contains(yarnNode))
            {
                return false;
            }

            Started.Add(yarnNode);
            return true;
        }

        public void Stop() => StopCount++;
    }
}
