namespace Reiga.VisualNodeEditor
{
    /// <summary>
    /// ロードすると、ノードの最初からやり直すノードの印（Yarn Spinner の Dialogue ノードなど。会話の途中からは再開できない）。
    /// このノードにいる間に <see cref="GraphRunner.CaptureState"/> すると、ノードに入る直前の値で保存する
    /// （やり直すときに、ノードの中で変えた値が二重にかからないように）。
    /// </summary>
    public interface IRestartOnLoad
    {
    }
}
