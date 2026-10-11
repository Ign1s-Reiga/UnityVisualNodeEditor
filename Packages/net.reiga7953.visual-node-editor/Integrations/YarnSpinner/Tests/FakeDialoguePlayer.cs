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

        /// <summary>会話を始めると true になる（テストで、知らせの無いまま終わったことにするには false にする）。</summary>
        public bool IsRunning { get; set; }

        public bool Start(string yarnNode)
        {
            if (Missing.Contains(yarnNode))
            {
                return false;
            }

            Started.Add(yarnNode);
            IsRunning = true;
            return true;
        }

        public void Stop() => StopCount++;
    }
}
