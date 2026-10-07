using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Editor.Views
{
    /// <summary>
    /// グラフのパラメータ（<see cref="GraphParameter"/>）を編集する Blackboard。
    /// 追加（+）・改名（名前のダブルクリック）・削除（右クリック → Delete）・並べ替え（ドラッグ）・既定値の編集はすべて Undo 対応。
    /// 位置・大きさは NodeGraphView.uss（#vne-blackboard）で決める。
    /// </summary>
    public sealed class ParameterBlackboard : Blackboard
    {
        private readonly BlackboardSection _section = new BlackboardSection { title = string.Empty };
        private NodeGraphAsset _asset;

        public ParameterBlackboard(GraphView graphView) : base(graphView)
        {
            name = "vne-blackboard";
            title = "Parameters";
            scrollable = true;
            Add(_section);

            addItemRequested = _ => ShowAddMenu();
            editTextRequested = (_, element, newName) => RenameParameter(GetParameterId(element), newName);
            moveItemRequested = (_, index, element) => MoveParameter(GetParameterId(element), index);
        }

        /// <summary>パラメータが追加・変更・削除されたとき（検証や未保存表示の更新に使う）。</summary>
        public event Action Changed;

        /// <summary>アセットのパラメータで一覧を作り直す。</summary>
        public void Rebuild(NodeGraphAsset asset)
        {
            _asset = asset;
            subTitle = asset != null ? asset.name : string.Empty;
            _section.Clear();
            if (asset == null)
            {
                return;
            }

            foreach (var parameter in asset.Parameters.Where(p => p != null))
            {
                _section.Add(CreateRow(parameter));
            }
        }

        /// <summary>型を指定してパラメータを追加する。名前は型名から重複しないように付ける。追加したパラメータを返す。</summary>
        internal GraphParameter AddParameter(GraphParameterType type)
        {
            if (_asset == null)
            {
                return null;
            }

            var parameter = new GraphParameter(ParameterNames.MakeUnique(_asset.Parameters.Select(p => p?.Name), type.ToString()), type);
            Undo.RecordObject(_asset, "Add Parameter");
            _asset.AddParameter(parameter);
            CommitStructuralChange();
            return parameter;
        }

        /// <summary>改名する。空・重複する名前なら何もせず false を返す（表示は元の名前に戻す）。</summary>
        internal bool RenameParameter(string id, string newName)
        {
            var parameter = _asset != null ? _asset.FindParameter(id) : null;
            if (parameter == null)
            {
                return false;
            }

            var others = _asset.Parameters.Where(p => p != null && p != parameter).Select(p => p.Name);
            if (!ParameterNames.IsValidName(others, newName))
            {
                Rebuild(_asset);
                return false;
            }

            Undo.RecordObject(_asset, "Rename Parameter");
            parameter.Name = newName.Trim();
            CommitStructuralChange();
            return true;
        }

        /// <summary>削除する。</summary>
        internal bool RemoveParameter(string id)
        {
            var parameter = _asset != null ? _asset.FindParameter(id) : null;
            if (parameter == null)
            {
                return false;
            }

            Undo.RecordObject(_asset, "Delete Parameter");
            _asset.RemoveParameter(parameter);
            CommitStructuralChange();
            return true;
        }

        /// <summary>並び順を変える。</summary>
        internal bool MoveParameter(string id, int index)
        {
            var parameter = _asset != null ? _asset.FindParameter(id) : null;
            if (parameter == null)
            {
                return false;
            }

            Undo.RecordObject(_asset, "Move Parameter");
            _asset.MoveParameter(parameter, index);
            CommitStructuralChange();
            return true;
        }

        private void ShowAddMenu()
        {
            var menu = new GenericMenu();
            foreach (GraphParameterType type in Enum.GetValues(typeof(GraphParameterType)))
            {
                menu.AddItem(new GUIContent(type.ToString()), false, () => AddParameter(type));
            }

            menu.ShowAsContext();
        }

        private VisualElement CreateRow(GraphParameter parameter)
        {
            var field = new BlackboardField(null, parameter.Name, parameter.Type.ToString()) { userData = parameter.Id };
            field.AddToClassList("vne-blackboard__field");

            // Delete キーで GraphView 側から消されないようにし、削除は右クリックメニューだけで行う
            field.capabilities &= ~Capabilities.Deletable;
            field.AddManipulator(new ContextualMenuManipulator(evt =>
                evt.menu.AppendAction("Delete", _ => RemoveParameter(parameter.Id))));

            var row = new BlackboardRow(field, CreateValueField(parameter));
            row.AddToClassList("vne-blackboard__row");
            return row;
        }

        private VisualElement CreateValueField(GraphParameter parameter)
        {
            const string label = "Default";
            VisualElement field = parameter.Type switch
            {
                GraphParameterType.Bool => Bind(new Toggle(label) { value = parameter.BoolValue }, parameter, (p, v) => p.BoolValue = v),
                GraphParameterType.Int => Bind(new IntegerField(label) { value = parameter.IntValue }, parameter, (p, v) => p.IntValue = v),
                GraphParameterType.Float => Bind(new FloatField(label) { value = parameter.FloatValue }, parameter, (p, v) => p.FloatValue = v),
                _ => Bind(new TextField(label) { value = parameter.StringValue }, parameter, (p, v) => p.StringValue = v),
            };
            field.AddToClassList("vne-blackboard__value");
            return field;
        }

        // 既定値の編集では一覧を作り直さない（入力中のフォーカスを失わないように）
        private BaseField<TValue> Bind<TValue>(BaseField<TValue> field, GraphParameter parameter, Action<GraphParameter, TValue> apply)
        {
            field.RegisterValueChangedCallback(evt =>
            {
                if (_asset == null || !(_asset.FindParameter(parameter.Id) is GraphParameter current))
                {
                    return;
                }

                Undo.RecordObject(_asset, "Edit Parameter");
                apply(current, evt.newValue);
                EditorUtility.SetDirty(_asset);
                Changed?.Invoke();
            });
            return field;
        }

        private void CommitStructuralChange()
        {
            EditorUtility.SetDirty(_asset);
            Rebuild(_asset);
            Changed?.Invoke();
        }

        private static string GetParameterId(VisualElement element) =>
            ((element as BlackboardField) ?? element?.Q<BlackboardField>())?.userData as string;
    }
}
