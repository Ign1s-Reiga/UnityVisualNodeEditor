using Reiga.VisualNodeEditor.Editor;

namespace Reiga.VisualNodeEditor.Localization.Tests
{
    /// <summary>
    /// テストのグラフ: Entry → Play。Blackboard: Score（Int、初期値は引数）、Speed（Float、1.5）、Cleared（Bool、false）。
    /// </summary>
    internal static class LocalizationTestGraph
    {
        public static NodeGraphAsset Create(int score = 0)
        {
            var builder = new NodeGraphBuilder();
            builder.AddParameter("Score", GraphParameterType.Int).IntValue = score;
            builder.AddParameter("Speed", GraphParameterType.Float).FloatValue = 1.5f;
            builder.AddParameter("Cleared", GraphParameterType.Bool);
            builder.Connect(builder.Entry, builder.Add(new StateNode { Title = "Play" }, 250, 0));
            return builder.Graph;
        }
    }
}
