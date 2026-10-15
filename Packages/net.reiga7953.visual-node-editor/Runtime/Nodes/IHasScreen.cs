using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor
{
    /// <summary>
    /// 画面（UI Toolkit の UXML）を持てるノード（<see cref="StateNode"/>・<see cref="SceneNode"/>・<see cref="ContainerNode"/>）。
    /// <see cref="GraphScreenHost"/> が、グラフの今の場所に沿ってその画面を重ねて出す（コンテナの画面の上に、今のノードの画面）。
    /// 画面は Unity のシーンとは別のもの（タイトル・設定・ポーズの画面は State ノードに付ければよく、シーンを分けなくてよい）。
    /// </summary>
    public interface IHasScreen
    {
        /// <summary>このノード（コンテナなら、その中のどこか）にいる間に出す画面。無ければ null。</summary>
        VisualTreeAsset Screen { get; set; }
    }
}
