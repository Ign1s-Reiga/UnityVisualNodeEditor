using System;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>テスト用: ロードすると最初からやり直す待機ノード（Yarn の Dialogue ノードと同じ扱い）。</summary>
    [Serializable]
    public sealed class RestartTestNode : NodeData, IRestartOnLoad
    {
        protected override string DefaultTitle => "Restart";
    }
}
