using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Editor.Inspector
{
    /// <summary>
    /// コンテナのインスペクタに出す出口の一覧。名前の編集、ドラッグでの並べ替え、追加、削除ができる。
    /// 変更はすぐアセットへ書き（Undo 可）、<see cref="Changed"/> で知らせる（出力ポートを作り直してもらう）。
    /// 見た目は NodeGraphEditor.uss（.vne-exit-list）で決める。
    /// </summary>
    public sealed class ContainerExitListView : VisualElement
    {
        private const string RemoveDialogTitle = "Remove Exit";

        private readonly NodeGraphAsset _asset;
        private readonly ContainerNode _container;
        private readonly List<ContainerExit> _items = new();
        private readonly ListView _list;
        private readonly Label _message;

        public ContainerExitListView(NodeGraphAsset asset, ContainerNode container)
        {
            _asset = asset;
            _container = container;
            AddToClassList("vne-exit-list");

            var header = new Label("Exits");
            header.AddToClassList("vne-exit-list__header");
            Add(header);

            _list = new ListView(_items, -1f, MakeItem, BindItem)
            {
                reorderable = true,
                reorderMode = ListViewReorderMode.Animated,
                selectionType = SelectionType.Single,
                showBorder = true,
                virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight,
            };
            _list.AddToClassList("vne-exit-list__list");
            _list.itemIndexChanged += OnItemIndexChanged;
            Add(_list);

            _message = new Label();
            _message.AddToClassList("vne-exit-list__message");
            Add(_message);

            var addButton = new Button(() => AddExit()) { text = "Add Exit", tooltip = "Add an exit (an output port of the container)" };
            addButton.AddToClassList("vne-exit-list__add");
            Add(addButton);

            Reload();
        }

        /// <summary>出口が追加・改名・並べ替え・削除されたとき。</summary>
        public event Action Changed;

        /// <summary>
        /// 影響のある出口を削除する前の確認。影響の説明を受け取り、削除してよければ true を返す。
        /// 既定は確認ダイアログ。テストでは差し替える。
        /// </summary>
        public Func<string, bool> Confirm { get; set; } =
            message => EditorUtility.DisplayDialog(RemoveDialogTitle, message, "Remove", "Cancel");

        /// <summary>直前の編集を受け付けなかった理由（名前が空・重複など）。無ければ空文字。</summary>
        public string Message => _message.text;

        /// <summary>出口を追加する（重複しない名前を付ける）。</summary>
        internal ContainerExit AddExit()
        {
            var exit = ContainerExitEditing.Add(_asset, _container);
            if (exit != null)
            {
                ShowMessage(string.Empty);
                OnChanged();
            }

            return exit;
        }

        /// <summary>出口を改名する。名前が使えなければ理由を表示して false。</summary>
        internal bool RenameExit(string exitId, string name)
        {
            var problem = ContainerExitEditing.ValidateName(_container, exitId, name);
            if (problem != null)
            {
                ShowMessage(problem);
                return false;
            }

            var previous = _container.FindExit(exitId)?.Name;
            if (!ContainerExitEditing.Rename(_asset, _container, exitId, name))
            {
                return false;
            }

            ShowMessage(string.Empty);
            if (previous != _container.FindExit(exitId)?.Name)
            {
                OnChanged();
            }

            return true;
        }

        /// <summary>出口を削除する。影響があれば <see cref="Confirm"/> で確かめる。削除したら true。</summary>
        internal bool RemoveExit(string exitId)
        {
            if (!ContainerExitEditing.Remove(_asset, _container, exitId, Confirm))
            {
                return false;
            }

            ShowMessage(string.Empty);
            OnChanged();
            return true;
        }

        /// <summary>一覧を <paramref name="index"/>（移動後の位置）へ並べ替えたとき（ListView のドラッグから呼ばれる）。</summary>
        internal void MoveExit(string exitId, int index)
        {
            if (ContainerExitEditing.Move(_asset, _container, exitId, index))
            {
                OnChanged();
            }
        }

        private void OnItemIndexChanged(int oldIndex, int newIndex)
        {
            // ListView は itemsSource を並べ替え済み。動かした出口は newIndex にある
            if (newIndex >= 0 && newIndex < _items.Count)
            {
                MoveExit(_items[newIndex].Id, newIndex);
            }
        }

        private void Reload()
        {
            _items.Clear();
            foreach (var exit in _container.Exits)
            {
                if (exit != null)
                {
                    _items.Add(exit);
                }
            }

            _list.RefreshItems();
        }

        private void OnChanged()
        {
            Reload();
            Changed?.Invoke();
        }

        private void ShowMessage(string text)
        {
            _message.text = text;
            _message.EnableInClassList("vne-exit-list__message--visible", text.Length > 0);
        }

        private VisualElement MakeItem()
        {
            var row = new VisualElement();
            row.AddToClassList("vne-exit-list__row");

            // 確定（Enter・フォーカスを外す）したときだけ改名する
            var nameField = new TextField { isDelayed = true };
            nameField.AddToClassList("vne-exit-list__name");
            nameField.RegisterValueChangedCallback(evt =>
            {
                if (nameField.userData is string exitId && !RenameExit(exitId, evt.newValue))
                {
                    nameField.SetValueWithoutNotify(_container.FindExit(exitId)?.Name ?? evt.previousValue);
                }
            });

            var removeButton = new Button { text = "−", tooltip = "Remove this exit and its connections" };
            removeButton.AddToClassList("vne-exit-list__remove");
            removeButton.clicked += () =>
            {
                if (removeButton.userData is string exitId)
                {
                    RemoveExit(exitId);
                }
            };

            row.Add(nameField);
            row.Add(removeButton);
            return row;
        }

        private void BindItem(VisualElement element, int index)
        {
            var exit = _items[index];
            var nameField = element.Q<TextField>();
            nameField.userData = exit.Id;
            nameField.SetValueWithoutNotify(exit.Name);
            element.Q<Button>().userData = exit.Id;
        }
    }
}
