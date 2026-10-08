namespace Reiga.VisualNodeEditor.Editor.Views
{
    /// <summary>
    /// シーンノード（<see cref="SceneNode"/>）の View。入力・出力を 1 つずつ持つ。
    /// タイトルを付けていなければシーン名をタイトルにし（同じ「Scene」が並ばないように）、タイトルを付けたらシーン名はサマリーに出す。
    /// </summary>
    [CustomNodeView(typeof(SceneNode))]
    public sealed class SceneNodeView : NodeView
    {
        protected override void CreatePorts()
        {
            AddInputPort(InputPortName);
            AddOutputPort(OutputPortName);
        }

        protected override string GetDisplayTitle()
        {
            var sceneName = (Data as SceneNode)?.SceneName;
            return Data.HasCustomTitle || string.IsNullOrEmpty(sceneName) ? base.GetDisplayTitle() : sceneName;
        }

        protected override string GetSummary() => Data.HasCustomTitle ? (Data as SceneNode)?.SceneName : string.Empty;
    }
}
