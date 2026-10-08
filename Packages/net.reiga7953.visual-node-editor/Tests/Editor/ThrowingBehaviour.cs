using System;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>テスト専用の振る舞い。OnUpdate で必ず例外を出す。</summary>
    [Serializable]
    internal sealed class ThrowingBehaviour : NodeBehaviour
    {
        public override void OnUpdate(float deltaTime) => throw new InvalidOperationException("broken behaviour");
    }
}
