using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Editor.Inspector
{
    /// <summary>
    /// <see cref="ParameterChange"/>（Set Parameter の振る舞いの 1 行）の欄。パラメータは Blackboard から選ぶドロップダウン（名前は打たない）、
    /// 操作は型で使えるもの（<c>=</c>、Int・Float なら <c>+=</c> も）、値は型に合った欄。パラメータか操作を変えたら行を作り直す。
    /// グラフのアセットの中の値でなければ（パラメータの一覧が無いので）ふつうの欄。見た目は NodeGraphEditor.uss（.vne-parameter-change）。
    /// </summary>
    [CustomPropertyDrawer(typeof(ParameterChange))]
    public sealed class ParameterChangeDrawer : PropertyDrawer
    {
        // ParameterChange の private フィールド名（EditMode テストで存在を検証している）
        internal const string ParameterIdPropertyName = "_parameterId";
        internal const string OperationPropertyName = "_operation";
        internal const string BoolValuePropertyName = "_boolValue";
        internal const string IntValuePropertyName = "_intValue";
        internal const string FloatValuePropertyName = "_floatValue";
        internal const string StringValuePropertyName = "_stringValue";

        private const string MissingLabel = "(missing)";
        private const string ChooseLabel = "(choose)";

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var serializedObject = property.serializedObject;
            if (!(serializedObject.targetObject is NodeGraphAsset))
            {
                return new PropertyField(property);
            }

            var row = new VisualElement();
            row.AddToClassList("vne-parameter-change");
            var path = property.propertyPath;

            void Rebuild()
            {
                row.Clear();
                serializedObject.Update();
                var current = serializedObject.FindProperty(path);
                if (current != null)
                {
                    Build(row, current, Rebuild);
                }
            }

            Rebuild();

            // Undo などで外から変わったときも、型に合った欄に作り直す
            row.TrackPropertyValue(property.FindPropertyRelative(ParameterIdPropertyName), _ => Rebuild());
            row.TrackPropertyValue(property.FindPropertyRelative(OperationPropertyName), _ => Rebuild());
            return row;
        }

        private static void Build(VisualElement row, SerializedProperty property, System.Action rebuild)
        {
            var graph = (NodeGraphAsset)property.serializedObject.targetObject;
            var idProperty = property.FindPropertyRelative(ParameterIdPropertyName);
            var parameters = graph.Parameters.Where(p => p != null).ToList();
            var parameter = graph.FindParameter(idProperty.stringValue);

            // パラメータ: 削除されたもの・まだ選んでいないものを指していれば、それを先頭に出す
            var names = parameters.Select(p => string.IsNullOrEmpty(p.Name) ? "(unnamed)" : p.Name).ToList();
            var offset = parameter == null ? 1 : 0;
            if (parameter == null)
            {
                names.Insert(0, string.IsNullOrEmpty(idProperty.stringValue) ? ChooseLabel : MissingLabel);
            }

            // 名前の無い・同じ名前のパラメータが並んでも、別の項目として選べるように番号を付ける
            var parameterField = new DropdownField(DropdownLabels.MakeUnique(names), parameter == null ? 0 : parameters.IndexOf(parameter))
            {
                tooltip = parameters.Count == 0 ? "Add a parameter in the Blackboard first" : "The Blackboard parameter to change",
            };
            parameterField.AddToClassList("vne-parameter-change__parameter");
            parameterField.RegisterValueChangedCallback(_ =>
            {
                var chosen = parameterField.index - offset;
                if (chosen < 0 || chosen >= parameters.Count)
                {
                    return;
                }

                property.serializedObject.Update();
                property.FindPropertyRelative(ParameterIdPropertyName).stringValue = parameters[chosen].Id;
                // 足せない型（Bool・String）に変えたら、操作を「入れる」に戻す
                var operation = property.FindPropertyRelative(OperationPropertyName);
                if (!ParameterChange.IsAllowed((ParameterChangeOperation)operation.enumValueIndex, parameters[chosen].Type))
                {
                    operation.enumValueIndex = (int)ParameterChangeOperation.Set;
                }

                property.serializedObject.ApplyModifiedProperties();
                rebuild();
            });
            row.Add(parameterField);

            if (parameter == null)
            {
                return;
            }

            row.Add(CreateOperationField(property, parameter.Type, rebuild));
            var value = CreateValueField(property, parameter.Type);
            value.AddToClassList("vne-parameter-change__value");
            row.Add(value);
        }

        // 操作: Set は "="、Add は "+="（Int・Float だけ）。今の操作が型に合わなければ（検証で Error）"?" を先頭に出す
        private static VisualElement CreateOperationField(SerializedProperty property, GraphParameterType type, System.Action rebuild)
        {
            var operations = new List<ParameterChangeOperation> { ParameterChangeOperation.Set };
            if (ParameterChange.IsAllowed(ParameterChangeOperation.Add, type))
            {
                operations.Add(ParameterChangeOperation.Add);
            }

            var symbols = operations.Select(operation => operation == ParameterChangeOperation.Add ? "+=" : "=").ToList();
            var current = operations.IndexOf((ParameterChangeOperation)property.FindPropertyRelative(OperationPropertyName).enumValueIndex);
            var offset = current < 0 ? 1 : 0;
            if (current < 0)
            {
                symbols.Insert(0, "?");
            }

            var field = new DropdownField(symbols, current < 0 ? 0 : current) { tooltip = "= sets the value. += adds to it (use a negative number to subtract)" };
            field.AddToClassList("vne-parameter-change__operation");
            field.SetEnabled(operations.Count > 1 || current < 0);
            field.RegisterValueChangedCallback(_ =>
            {
                var chosen = field.index - offset;
                if (chosen < 0 || chosen >= operations.Count)
                {
                    return;
                }

                property.serializedObject.Update();
                property.FindPropertyRelative(OperationPropertyName).enumValueIndex = (int)operations[chosen];
                property.serializedObject.ApplyModifiedProperties();
                rebuild();
            });
            return field;
        }

        // 値: 型に合った欄（作り直すので、インスペクタ全体の Bind ではなくここで結び付ける）
        private static BindableElement CreateValueField(SerializedProperty property, GraphParameterType type)
        {
            switch (type)
            {
                case GraphParameterType.Bool:
                {
                    var toggle = new Toggle { tooltip = "The value to set" };
                    toggle.BindProperty(property.FindPropertyRelative(BoolValuePropertyName));
                    return toggle;
                }
                case GraphParameterType.Float:
                {
                    var field = new FloatField { tooltip = "The value to set or add" };
                    field.BindProperty(property.FindPropertyRelative(FloatValuePropertyName));
                    return field;
                }
                case GraphParameterType.String:
                {
                    var field = new TextField { tooltip = "The value to set" };
                    field.BindProperty(property.FindPropertyRelative(StringValuePropertyName));
                    return field;
                }
                default:
                {
                    var field = new IntegerField { tooltip = "The value to set or add" };
                    field.BindProperty(property.FindPropertyRelative(IntValuePropertyName));
                    return field;
                }
            }
        }
    }
}
