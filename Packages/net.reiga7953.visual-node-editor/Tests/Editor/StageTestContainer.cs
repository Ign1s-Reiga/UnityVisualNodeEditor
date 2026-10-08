using System;
using System.Collections.Generic;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>
    /// テスト専用のコンテナ（初期の出口を上書きするサブクラスの例）。[NodeMenu] を付けないので Create Node メニューには出ない。
    /// </summary>
    [Serializable]
    internal sealed class StageTestContainer : ContainerNode
    {
        protected override IEnumerable<string> DefaultExitNames => new[] { "Clear", "GameOver" };
    }
}
