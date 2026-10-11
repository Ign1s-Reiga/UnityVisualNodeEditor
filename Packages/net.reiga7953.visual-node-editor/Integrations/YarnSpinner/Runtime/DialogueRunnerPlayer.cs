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

        public void Start(string yarnNode) => _runner.StartDialogue(yarnNode).Forget();

        public void Stop() => _runner.Stop().Forget();
    }
}
