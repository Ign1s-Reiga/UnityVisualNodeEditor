using System;
using System.Collections.Generic;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>テスト専用の、出口の無い状態で作られるコンテナ。[NodeMenu] を付けないので Create Node メニューには出ない。</summary>
    [Serializable]
    internal sealed class NoExitTestContainer : ContainerNode
    {
        protected override IEnumerable<string> DefaultExitNames => Array.Empty<string>();
    }
}
