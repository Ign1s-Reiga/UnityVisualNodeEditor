using UnityEngine.Events;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>
    /// テスト専用の、uGUI の Button の代わり（public な onClick（UnityEvent）プロパティを持つ）。
    /// エディタ用のテストアセンブリの MonoBehaviour は GameObject に付けられないため、普通のクラスにしている。
    /// </summary>
    internal sealed class ClickSourceTestComponent
    {
        public UnityEvent onClick { get; } = new UnityEvent();
    }
}
