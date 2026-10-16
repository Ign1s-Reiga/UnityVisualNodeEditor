namespace Reiga.VisualNodeEditor
{
    /// <summary><see cref="GraphRunner"/> が Scene ノードに入ったときにシーンを読み込む方法。テストでは偽物に差し替える。</summary>
    public interface ISceneLoader
    {
        /// <summary><paramref name="scene"/> を読み込む。</summary>
        void LoadScene(SceneReference scene);

        /// <summary>
        /// 今いるシーンでも <paramref name="scene"/> を読み込み直す（Reload をオンにした Scene ノードへ、同じシーンにいるまま入り直したとき）。
        /// 既定は <see cref="LoadScene"/> と同じ（前からある読み込み方を壊さない）。
        /// </summary>
        void ReloadScene(SceneReference scene) => LoadScene(scene);
    }
}
