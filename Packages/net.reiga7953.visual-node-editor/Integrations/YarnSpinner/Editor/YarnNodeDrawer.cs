using System.Linq;
using Reiga.VisualNodeEditor.Editor.Views;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.YarnSpinner.Editor
{
    /// <summary>
    /// <see cref="YarnNodeAttribute"/> の付いた文字列フィールドを、入力欄と「▾」のメニュー（プロジェクトの Yarn プロジェクトのノード名。
    /// プロジェクトごとにまとめる）にする。一覧に無い名前も入力できる。見た目はイベント名の欄と同じ（.vne-event-name-field）。
    /// </summary>
    [CustomPropertyDrawer(typeof(YarnNodeAttribute))]
    public sealed class YarnNodeDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                return new PropertyField(property);
            }

            var root = new VisualElement();
            root.AddToClassList("vne-event-name-field");

            var text = new TextField(NodeDisplay.GetFieldLabel(property.name)) { bindingPath = property.propertyPath };
            text.AddToClassList("vne-event-name-field__text");
            text.AddToClassList(BaseField<string>.alignedFieldUssClassName);
            root.Add(text);

            var pick = new Button { text = "▾", tooltip = "Pick a node from the Yarn projects in this project" };
            pick.AddToClassList("vne-event-name-field__pick");
            pick.clicked += () => ShowMenu(pick, property.serializedObject, property.propertyPath);
            root.Add(pick);
            return root;
        }

        private static void ShowMenu(VisualElement anchor, SerializedObject serializedObject, string propertyPath)
        {
            var menu = new GenericMenu();
            var projects = YarnProjects.All.Where(p => p.NodeNames.Count > 0).ToList();
            if (projects.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("No Yarn nodes yet (create a Yarn Project and a .yarn script)"));
            }

            foreach (var project in projects)
            {
                foreach (var node in project.NodeNames)
                {
                    // プロジェクトが 1 つなら階層にしない
                    var label = projects.Count == 1 ? node : $"{project.Name}/{node}";
                    menu.AddItem(new GUIContent(label), false, () =>
                    {
                        serializedObject.Update();
                        serializedObject.FindProperty(propertyPath).stringValue = node;
                        serializedObject.ApplyModifiedProperties();
                    });
                }
            }

            menu.DropDown(anchor.worldBound);
        }
    }
}
