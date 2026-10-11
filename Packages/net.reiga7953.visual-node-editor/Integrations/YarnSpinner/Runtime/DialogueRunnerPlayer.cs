using System;
using Yarn.Unity;

namespace Reiga.VisualNodeEditor.YarnSpinner
{
    /// <summary><see cref="IDialoguePlayer"/> を Yarn Spinner の <see cref="DialogueRunner"/> で行う。</summary>
    internal sealed class DialogueRunnerPlayer : IDialoguePlayer
    {
        private readonly DialogueRunner _runner;

        public DialogueRunnerPlayer(DialogueRunner runner)
        {
            _runner = runner;
        }

        // 無いノードを渡すと、Yarn は始めるのに失敗して終わりの知らせを出さないので、先に確かめる
        public bool Start(string yarnNode)
        {
            if (!HasNode(_runner.YarnProject, yarnNode))
            {
                return false;
            }

            _runner.StartDialogue(yarnNode).Forget();
            return true;
        }

        public void Stop() => _runner.Stop().Forget();

        // IsDialogueRunning は止めた時点で false になるが、終わりの知らせはその後の後片付けが済んでから来る。
        // DialogueTask はその知らせの直前に終わるので、両方を見る（壊された DialogueRunner はもう知らせを出さない）
        public bool IsRunning => _runner != null && (_runner.IsDialogueRunning || !_runner.DialogueTask.IsCompleted());

        private static bool HasNode(YarnProject project, string yarnNode)
        {
            if (project == null)
            {
                return false;
            }

            try
            {
                return Array.IndexOf(project.NodeNames, yarnNode) >= 0;
            }
            catch (Exception)
            {
                // スクリプトにエラーがあって、プログラムが無い
                return false;
            }
        }
    }
}
