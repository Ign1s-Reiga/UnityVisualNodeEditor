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

        private static bool HasNode(YarnProject project, string yarnNode)
        {
            if (project == null)
            {
                return false;
            }

            try
            {
                return project.Program.Nodes.ContainsKey(yarnNode);
            }
            catch (Exception)
            {
                // スクリプトにエラーがあって、プログラムが無い
                return false;
            }
        }
    }
}
