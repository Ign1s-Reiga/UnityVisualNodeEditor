namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>テスト専用の、[Serializable] を付け忘れた振る舞い（グラフに保存できないので追加させない）。</summary>
    internal sealed class NonSerializableTestBehaviour : NodeBehaviour
    {
    }
}
