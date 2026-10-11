using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Editor.Inspector
{
    /// <summary>
    /// Event ノードのインスペクタに出す条件の一覧。行ごとに、パラメータ（Blackboard から選ぶ）・比較（型で使えるものだけ）・値（型に合った欄）・削除ボタン。
    /// パラメータ・比較・追加・削除はすぐアセットへ書き（Undo 可）、<see cref="Changed"/> で知らせる（インスペクタとノードを作り直してもらう）。
    /// 値の欄はインスペクタ全体の Bind で結び付く（入力のたびに作り直さない）。見た目は NodeGraphEditor.uss（.vne-condition-list）。
    /// </summary>
    public sealed class ConditionListView : VisualElement
    {
        /// <summary><see cref="EventNode"/> の条件のフィールド名（EditMode テストで存在を検証している）。</summary>
        internal const string ConditionsPropertyName = "_conditions";

        internal const string MissingParameterLabel = "(missing)";

        private readonly NodeGraphAsset _asset;
        private readonly EventNode _eventNode;
        private readonly SerializedProperty _property;
        private readonly List<VisualElement> _rows = new();

        /// <param name="asset">ノードのあるグラフ（パラメータの一覧を取る）。</param>
        /// <param name="eventNode">編集する Event ノード。</param>
        /// <param name="conditionsProperty">そのノードの <c>_conditions</c>（値の欄を結び付ける）。</param>
        public ConditionListView(NodeGraphAsset asset, EventNode eventNode, SerializedProperty conditionsProperty)
        {
            _asset = asset;
            _eventNode = eventNode;
            _property = conditionsProperty;
            AddToClassList("vne-condition-list");

            var header = new Label("Conditions");
            header.AddToClassList("vne-condition-list__header");
            Add(header);

            var hint = new Label(eventNode.Conditions.Count == 0
                ? "None: this Event always passes."
                : "All must hold. Events with the same name are tried in edge order.");
            hint.AddToClassList("vne-condition-list__hint");
            Add(hint);

            for (var i = 0; i < eventNode.Conditions.Count; i++)
            {
                var row = CreateRow(i);
                _rows.Add(row);
                Add(row);
            }

            var hasParameters = asset.Parameters.Any(p => p != null);
            var addButton = new Button(() => AddCondition())
            {
                text = "Add Condition",
                tooltip = hasParameters
                    ? "Add a condition on a Blackboard parameter"
                    : "Add a parameter in the Blackboard first: conditions compare parameters",
            };
            addButton.AddToClassList("vne-condition-list__add");
            addButton.SetEnabled(hasParameters);
            Add(addButton);
        }

        /// <summary>条件が追加・削除された、またはパラメータ・比較が変わったとき。</summary>
        public event Action Changed;

        /// <summary>表示している行（テスト用）。</summary>
        internal IReadOnlyList<VisualElement> Rows => _rows;

        /// <summary>条件を足す（Blackboard の最初のパラメータ）。足したら true。</summary>
        internal bool AddCondition()
        {
            if (ConditionEditing.Add(_asset, _eventNode) == null)
            {
                return false;
            }

            Changed?.Invoke();
            return true;
        }

        private VisualElement CreateRow(int index)
        {
            var condition = _eventNode.Conditions[index];
            var parameters = _asset.Parameters.Where(p => p != null).ToList();
            var parameter = condition != null ? _asset.FindParameter(condition.ParameterId) : null;

            var row = new VisualElement();
            row.AddToClassList("vne-condition-list__row");

            // パラメータ: Blackboard から選ぶ（名前は打たない）。削除されたものを指していれば "(missing)" を先頭に出す
            var names = parameters.Select(p => string.IsNullOrEmpty(p.Name) ? "(unnamed)" : p.Name).ToList();
            var offset = parameter == null ? 1 : 0;
            if (parameter == null)
            {
                names.Insert(0, MissingParameterLabel);
            }

            var parameterField = new DropdownField(names, parameter == null ? 0 : parameters.IndexOf(parameter))
            {
                tooltip = "The Blackboard parameter to compare",
            };
            parameterField.AddToClassList("vne-condition-list__parameter");
            parameterField.RegisterValueChangedCallback(_ =>
            {
                var chosen = parameterField.index - offset;
                if (chosen >= 0 && chosen < parameters.Count && ConditionEditing.SetParameter(_asset, _eventNode, index, parameters[chosen].Id))
                {
                    Changed?.Invoke();
                }
            });
            row.Add(parameterField);

            if (parameter != null && condition != null)
            {
                row.Add(CreateOperatorField(index, condition, parameter.Type));
                var value = CreateValueField(index, parameter.Type);
                if (value != null)
                {
                    row.Add(value);
                }
            }

            var remove = new Button(() =>
            {
                if (ConditionEditing.Remove(_asset, _eventNode, index))
                {
                    Changed?.Invoke();
                }
            })
            {
                text = "✕",
                tooltip = "Remove this condition",
            };
            remove.AddToClassList("vne-condition-list__remove");
            row.Add(remove);
            return row;
        }

        // 比較: パラメータの型で使えるものだけ。今の比較が型に合わなければ（検証で Error）"?" を先頭に出す
        private VisualElement CreateOperatorField(int index, TransitionCondition condition, GraphParameterType type)
        {
            var operators = TransitionCondition.GetOperators(type).ToList();
            var symbols = operators.Select(TransitionCondition.GetSymbol).ToList();
            var current = operators.IndexOf(condition.Operator);
            var offset = current < 0 ? 1 : 0;
            if (current < 0)
            {
                symbols.Insert(0, "?");
            }

            var field = new DropdownField(symbols, current < 0 ? 0 : current) { tooltip = "How to compare" };
            field.AddToClassList("vne-condition-list__operator");
            field.RegisterValueChangedCallback(_ =>
            {
                var chosen = field.index - offset;
                if (chosen >= 0 && chosen < operators.Count && ConditionEditing.SetOperator(_asset, _eventNode, index, operators[chosen]))
                {
                    Changed?.Invoke();
                }
            });
            return field;
        }

        // 値: 型に合った欄（Bool は比較だけで表すので無し）
        private VisualElement CreateValueField(int index, GraphParameterType type)
        {
            var element = _property?.GetArrayElementAtIndex(index);
            if (element == null)
            {
                return null;
            }

            BindableElement field = type switch
            {
                GraphParameterType.Int => new IntegerField { bindingPath = element.FindPropertyRelative("_intValue").propertyPath },
                GraphParameterType.Float => new FloatField { bindingPath = element.FindPropertyRelative("_floatValue").propertyPath },
                GraphParameterType.String => new TextField { bindingPath = element.FindPropertyRelative("_stringValue").propertyPath },
                _ => null,
            };
            if (field != null)
            {
                field.tooltip = "The value to compare with";
                field.AddToClassList("vne-condition-list__value");
            }

            return field;
        }
    }
}
