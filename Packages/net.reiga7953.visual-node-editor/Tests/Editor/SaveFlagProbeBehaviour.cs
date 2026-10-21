using System;
using System.Collections.Generic;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>テスト専用の振る舞い。OnEnter で <see cref="GraphRunner.EnteredFromSave"/> を記録する（Runner は振る舞いを複製するので、記録は static）。</summary>
    [Serializable]
    internal sealed class SaveFlagProbeBehaviour : NodeBehaviour
    {
        public static List<bool> Seen { get; } = new();

        public override void OnEnter() => Seen.Add(Runner.EnteredFromSave);
    }
}
