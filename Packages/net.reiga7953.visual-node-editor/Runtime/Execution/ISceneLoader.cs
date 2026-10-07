namespace Reiga.VisualNodeEditor
{
    /// <summary><see cref="GraphRunner"/> が Scene ノードに入ったときにシーンを読み込む方法。テストでは偽物に差し替える。</summary>
    public interface ISceneLoader
    {
        /// <summary><paramref name="scene"/> を読み込む。</summary>
        void LoadScene(SceneReference scene);
    }
}
