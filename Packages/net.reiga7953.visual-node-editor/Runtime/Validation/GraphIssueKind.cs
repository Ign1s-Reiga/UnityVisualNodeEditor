namespace Reiga.VisualNodeEditor
{
    /// <summary>
    /// 問題の種類。エディタが直し方を決めるのに使う（メッセージの文字列では見分けない）。直し方の無い問題は <see cref="Other"/>。
    /// </summary>
    public enum GraphIssueKind
    {
        /// <summary>決まった直し方の無い問題。</summary>
        Other,

        /// <summary>ルート階層に Entry ノードが無い。</summary>
        MissingEntry,

        /// <summary>Scene ノードにシーンが指定されていない。</summary>
        SceneNotSet,

        /// <summary>Scene ノードが指しているシーンが削除された。</summary>
        SceneMissing,

        /// <summary>Scene ノードのシーンが Build Settings で有効になっていない。</summary>
        SceneNotInBuildSettings,

        /// <summary>Build Settings のどのシーンにも、このグラフの Graph Runner が無い。</summary>
        NoGraphRunner,
    }
}
