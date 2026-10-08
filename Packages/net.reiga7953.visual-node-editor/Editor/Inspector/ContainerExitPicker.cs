using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Editor.Inspector
{
    /// <summary>
    /// Exit ノードのインスペクタに出す、出口の選択欄。親のコンテナの出口から選ぶドロップダウンと、選んだ出口の名前の欄。
    /// ドロップダウンの最後の「+ New exit…」で、親のコンテナに出口を追加して選ぶ。
    /// 変更はすぐアセットへ書き（Undo 可）、<see cref="Changed"/> で知らせる。見た目は NodeGraphEditor.uss（.vne-exit-picker）で決める。
    /// </summary>
    public sealed class ContainerExitPicker : VisualElement
    {
        /// <summary>ドロップダウンの最後の項目。親のコンテナに出口を追加して選ぶ。</summary>
        public const string NewExitChoice = "+ New exit…";

        /// <summary>指している出口が親のコンテナに無いときにドロップダウンに出す文字列。</summary>
        public const string MissingExitChoice = "(missing exit)";

        private readonly NodeGraphAsset _asset;
        private readonly ContainerExitNode _exitNode;
        private readonly ContainerNode _parent;
        private readonly DropdownField _dropdown;
        private readonly TextField _nameField;
        private readonly Label _message;

        public ContainerExitPicker(NodeGraphAsset asset, ContainerExitNode exitNode)
        {
            _asset = asset;
            _exitNode = exitNode;
            _parent = asset != null ? asset.FindNode(exitNode.ParentId) as ContainerNode : null;
            AddToClassList("vne-exit-picker");

            var exits = Exits;
            var choices = exits.Select(e => e.Name).ToList();
            if (_parent != null)
            {
                choices.Add(NewExitChoice);
            }

            var current = exits.FindIndex(e => e.Id == exitNode.ExitId);
            _dropdown = new DropdownField("Exit", choices, current);
            _dropdown.AddToClassList("vne-exit-picker__dropdown");
            if (current < 0)
            {
                _dropdown.SetValueWithoutNotify(MissingExitChoice);
            }

            _dropdown.RegisterValueChangedCallback(_ => Pick(_dropdown.index));
            Add(_dropdown);

            // 選んでいる出口の名前。改名すると、同じ出口を指す Exit ノードとコンテナのポートにも反映される
            _nameField = new TextField("Exit Name") { isDelayed = true, tooltip = "Renames the exit of the container" };
            _nameField.AddToClassList("vne-exit-picker__name");
            _nameField.SetEnabled(current >= 0);
            _nameField.SetValueWithoutNotify(current >= 0 ? exits[current].Name : string.Empty);
            _nameField.RegisterValueChangedCallback(evt =>
            {
                if (!RenameSelectedExit(evt.newValue))
                {
                    _nameField.SetValueWithoutNotify(_parent?.FindExit(_exitNode.ExitId)?.Name ?? evt.previousValue);
                }
            });
            Add(_nameField);

            _message = new Label();
            _message.AddToClassList("vne-exit-picker__message");
            Add(_message);
        }

        /// <summary>Exit ノードの出口が変わった・親のコンテナの出口が追加・改名されたとき。</summary>
        public event Action Changed;

        /// <summary>ドロップダウンの項目（親のコンテナの出口の名前、最後に <see cref="NewExitChoice"/>）。</summary>
        public IReadOnlyList<string> Choices => _dropdown.choices;

        /// <summary>直前の編集を受け付けなかった理由。無ければ空文字。</summary>
        public string Message => _message.text;

        private List<ContainerExit> Exits => _parent?.Exits.Where(e => e != null).ToList() ?? new List<ContainerExit>();

        /// <summary>
        /// ドロップダウンの <paramref name="index"/> 番目を選んだときの処理。出口の数と同じ（最後の項目）なら新しい出口を作って選ぶ。
        /// 変わったら true。
        /// </summary>
        internal bool Pick(int index)
        {
            var exits = Exits;
            var changed = index == exits.Count
                ? ContainerExitEditing.AddExitForExitNode(_asset, _exitNode) != null
                : index >= 0 && index < exits.Count && exits[index].Id != _exitNode.ExitId
                    && ContainerExitEditing.SetExitNodeTarget(_asset, _exitNode, exits[index].Id);
            if (changed)
            {
                Changed?.Invoke();
            }

            return changed;
        }

        /// <summary>選んでいる出口を改名する。名前が使えなければ理由を表示して false。</summary>
        internal bool RenameSelectedExit(string name)
        {
            var problem = ContainerExitEditing.ValidateName(_parent, _exitNode.ExitId, name);
            if (problem != null)
            {
                ShowMessage(problem);
                return false;
            }

            var previous = _parent?.FindExit(_exitNode.ExitId)?.Name;
            if (!ContainerExitEditing.Rename(_asset, _parent, _exitNode.ExitId, name))
            {
                return false;
            }

            ShowMessage(string.Empty);
            if (previous != _parent.FindExit(_exitNode.ExitId)?.Name)
            {
                Changed?.Invoke();
            }

            return true;
        }

        private void ShowMessage(string text)
        {
            _message.text = text;
            _message.EnableInClassList("vne-exit-picker__message--visible", text.Length > 0);
        }
    }
}
