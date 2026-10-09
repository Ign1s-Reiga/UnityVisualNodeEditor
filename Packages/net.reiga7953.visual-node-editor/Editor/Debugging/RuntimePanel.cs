using System.Collections.Generic;
using System.Linq;
using Reiga.VisualNodeEditor.Editor.Views;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Editor.Debugging
{
    /// <summary>
    /// Play 中、インスペクタの上に出す「Now running」パネル。今いるノード（入っているコンテナも含めて）と、
    /// そこから起こせる操作（イベントの Raise / Advance）のボタンを出す。シーンにボタンを置かなくても流れを確かめられる。
    /// 見た目は NodeGraphEditor.uss（.vne-runtime-panel）。
    /// </summary>
    public sealed class RuntimePanel : VisualElement
    {
        private const string HiddenClassName = "vne-runtime-panel--hidden";

        private readonly Label _location;
        private readonly VisualElement _actions;
        private readonly Label _noActions;
        private GraphRunner _runner;

        public RuntimePanel()
        {
            AddToClassList("vne-runtime-panel");
            AddToClassList(HiddenClassName);

            var header = new Label("Now running");
            header.AddToClassList("vne-runtime-panel__header");
            Add(header);

            _location = new Label();
            _location.AddToClassList("vne-runtime-panel__location");
            Add(_location);

            _actions = new VisualElement();
            _actions.AddToClassList("vne-runtime-panel__actions");
            Add(_actions);

            _noActions = new Label("Nothing leads on from here (no Event or next node).");
            _noActions.AddToClassList("vne-runtime-panel__empty");
            Add(_noActions);
        }

        /// <summary>表示中の Runner。表示していなければ null。</summary>
        public GraphRunner Runner => _runner;

        /// <summary>今出しているボタン（テスト用）。</summary>
        internal IReadOnlyList<Button> ActionButtons => _actions.Query<Button>().ToList();

        /// <summary>今いる場所の表示（例: "Stage › Inner › Boss"）。</summary>
        public string LocationText => _location.text;

        /// <summary><paramref name="runner"/> の状態を出す。null なら隠す。</summary>
        public void Show(GraphRunner runner)
        {
            _runner = runner;
            Refresh();
        }

        /// <summary>今の Runner の状態で表示し直す（ノードを移ったときなど）。止まっていれば隠す。</summary>
        public void Refresh()
        {
            var running = _runner != null && _runner.IsRunning && _runner.Current != null;
            EnableInClassList(HiddenClassName, !running);
            _actions.Clear();
            if (!running)
            {
                _location.text = string.Empty;
                return;
            }

            _location.text = GetLocation(_runner);
            var actions = RuntimeActions.For(_runner.Query, _runner.Current);
            foreach (var action in actions)
            {
                var button = new Button(() =>
                {
                    action.Run(_runner);
                    Refresh();
                })
                {
                    text = action.Label,
                    tooltip = action.Kind == RuntimeActionKind.Raise
                        ? $"Raise(\"{action.EventName}\") on the running graph"
                        : "Advance() on the running graph",
                };
                button.AddToClassList("vne-runtime-panel__action");
                _actions.Add(button);
            }

            _noActions.EnableInClassList("vne-runtime-panel__empty--hidden", actions.Count > 0);
        }

        /// <summary>今いる場所（入っているコンテナを外側から並べ、最後に今のノード）。</summary>
        public static string GetLocation(GraphRunner runner)
        {
            if (runner?.Current == null)
            {
                return string.Empty;
            }

            var names = runner.ContainerPath
                .Select(NodeDisplay.GetNodeLabel)
                .Append(NodeDisplay.GetNodeLabel(runner.Current));
            return string.Join(" › ", names);
        }
    }
}
