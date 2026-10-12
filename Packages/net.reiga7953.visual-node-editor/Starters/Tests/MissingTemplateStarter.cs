using Reiga.VisualNodeEditor.Starters.Editor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Reiga.VisualNodeEditor.Starters.Tests
{
    /// <summary>ひな形のファイルが無い Starter（パッケージの一部が欠けたとき）。グラフとシーンは Snake のものを使う。</summary>
    internal sealed class MissingTemplateStarter : GameStarter
    {
        internal override string StarterName => "Missing Template Starter";

        internal override string BaseName => "Broken";

        internal override string TemplateClass => "NoSuchTemplate";

        internal override string Controls => string.Empty;

        internal override NodeGraphAsset BuildGraph() => SnakeStarter.Instance.BuildGraph();

        internal override RectTransform BuildGame(Scene scene, NodeGraphAsset graph) => SnakeStarter.Instance.BuildGame(scene, graph);
    }
}
