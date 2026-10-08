using System;
using Reiga.VisualNodeEditor.Editor.Behaviours;
using Reiga.VisualNodeEditor.Editor.Views;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Editor.Inspector
{
    /// <summary>
    /// 振る舞いを持てるノード（<see cref="IBehaviourHost"/>）のインスペクタに出す「Behaviours」の一覧。
    /// 振る舞いごとにフィールドを <see cref="PropertyField"/> で編集でき（バインディング経由なので Undo 対応）、
    /// 並べ替え・削除・スクリプトを開く操作と、Add Behaviour / Create Script… を持つ。
    /// 一覧の形が変わる操作の後は <see cref="Changed"/> で知らせる（インスペクタを作り直してもらう）。見た目は NodeGraphEditor.uss（.vne-behaviour-list）。
    /// </summary>
    public sealed class NodeBehaviourListView : VisualElement
    {
        /// <summary><see cref="StateNode"/> / <see cref="SceneNode"/> の振る舞いのリストのフィールド名（EditMode テストで存在を検証している）。</summary>
        internal const string BehavioursPropertyName = "_behaviours";

        /// <summary>型が削除・改名されて読めなかった振る舞いの見出し。</summary>
        public const string MissingBehaviourLabel = "Missing behaviour";

        private readonly NodeGraphAsset _asset;
        private readonly string _nodeId;

        /// <param name="asset">ノードを持つグラフ。</param>
        /// <param name="node">振る舞いを持てるノード。</param>
        /// <param name="behavioursProperty">ノードの振る舞いのリストのプロパティ（フィールドの編集に使う。null ならフィールドを出さない）。</param>
        public NodeBehaviourListView(NodeGraphAsset asset, NodeData node, SerializedProperty behavioursProperty)
        {
            _asset = asset;
            _nodeId = node.Id;
            AddToClassList("vne-behaviour-list");

            var header = new Label("Behaviours");
            header.AddToClassList("vne-behaviour-list__header");
            Add(header);

            var behaviours = ((IBehaviourHost)node).Behaviours;
            if (behaviours.Count == 0)
            {
                var empty = new Label("Add a behaviour to run C# code (OnUpdate, OnFixedUpdate, …) while the runner stays on this node.");
                empty.AddToClassList("vne-behaviour-list__empty");
                Add(empty);
            }

            for (var i = 0; i < behaviours.Count; i++)
            {
                var property = behavioursProperty != null && i < behavioursProperty.arraySize
                    ? behavioursProperty.GetArrayElementAtIndex(i)
                    : null;
                Add(CreateItem(behaviours[i], i, behaviours.Count, property));
            }

            var footer = new VisualElement();
            footer.AddToClassList("vne-behaviour-list__footer");
            var addButton = new Button { text = "Add Behaviour", tooltip = "Add an existing NodeBehaviour class" };
            addButton.clicked += () => ShowAddMenu(addButton);
            var createButton = new Button(() => NodeBehaviourScriptCreator.CreateScript(_asset, _nodeId))
            {
                text = "Create Script…",
                tooltip = "Write a new NodeBehaviour script, open it, and add it here once it compiles",
            };
            footer.Add(addButton);
            footer.Add(createButton);
            Add(footer);
        }

        /// <summary>振る舞いが追加・削除・並べ替えられたとき。</summary>
        public event Action Changed;

        /// <summary>振る舞いを末尾に追加する（Add Behaviour メニューから呼ばれる）。追加したら true。</summary>
        internal bool AddBehaviour(Type type) => Notify(NodeBehaviourEditing.Add(_asset, _nodeId, type) != null);

        /// <summary><paramref name="index"/> 番目の振る舞いを外す。外したら true。</summary>
        internal bool RemoveBehaviour(int index) => Notify(NodeBehaviourEditing.Remove(_asset, _nodeId, index));

        /// <summary><paramref name="index"/> 番目の振る舞いを <paramref name="newIndex"/> へ移す。動いたら true。</summary>
        internal bool MoveBehaviour(int index, int newIndex) => Notify(NodeBehaviourEditing.Move(_asset, _nodeId, index, newIndex));

        private bool Notify(bool changed)
        {
            if (changed)
            {
                Changed?.Invoke();
            }

            return changed;
        }

        private VisualElement CreateItem(NodeBehaviour behaviour, int index, int count, SerializedProperty property)
        {
            var item = new VisualElement();
            item.AddToClassList("vne-behaviour-list__item");

            var row = new VisualElement();
            row.AddToClassList("vne-behaviour-list__item-header");
            var type = behaviour?.GetType();
            var title = new Label(type != null ? NodeDisplay.GetBehaviourName(type) : MissingBehaviourLabel)
            {
                tooltip = type != null
                    ? type.FullName
                    : "Its script was renamed or deleted, so it is skipped at runtime. Remove it, or restore the script.",
            };
            title.AddToClassList("vne-behaviour-list__item-title");
            title.EnableInClassList("vne-behaviour-list__item-title--missing", type == null);
            row.Add(title);

            if (type != null)
            {
                row.Add(CreateButton("Edit", "Open the script", true, () => OpenScript(type)));
            }

            row.Add(CreateButton("↑", "Move up (runs earlier)", index > 0, () => MoveBehaviour(index, index - 1)));
            row.Add(CreateButton("↓", "Move down (runs later)", index < count - 1, () => MoveBehaviour(index, index + 1)));
            row.Add(CreateButton("−", "Remove this behaviour", true, () => RemoveBehaviour(index)));
            item.Add(row);

            // 振る舞いのフィールド（バインディングはインスペクタ全体の Bind で行われる）
            if (property != null && behaviour != null)
            {
                var child = property.Copy();
                var end = property.GetEndProperty();
                var hasChild = child.NextVisible(true);
                while (hasChild && !SerializedProperty.EqualContents(child, end))
                {
                    var field = new PropertyField(child.Copy(), NodeDisplay.GetFieldLabel(child.name));
                    field.AddToClassList("vne-behaviour-list__field");
                    item.Add(field);
                    hasChild = child.NextVisible(false);
                }
            }

            return item;
        }

        private static Button CreateButton(string text, string tooltip, bool enabled, Action clicked)
        {
            var button = new Button(clicked) { text = text, tooltip = tooltip };
            button.AddToClassList("vne-behaviour-list__button");
            button.SetEnabled(enabled);
            return button;
        }

        private void ShowAddMenu(VisualElement anchor)
        {
            var menu = new GenericMenu();
            var types = NodeBehaviourCatalog.GetTypes();
            if (types.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("No NodeBehaviour classes yet (use Create Script…)"));
            }

            foreach (var type in types)
            {
                var path = NodeBehaviourCatalog.GetMenuPath(type);
                if (NodeBehaviourCatalog.IsSaveable(type))
                {
                    menu.AddItem(new GUIContent(path), false, () => AddBehaviour(type));
                }
                else
                {
                    // [Serializable] が無いとグラフに保存できないので、付けるよう促す
                    menu.AddDisabledItem(new GUIContent(path + " (needs [Serializable])"));
                }
            }

            menu.DropDown(anchor.worldBound);
        }

        private static void OpenScript(Type type)
        {
            var script = NodeBehaviourCatalog.FindScript(type);
            if (script != null)
            {
                AssetDatabase.OpenAsset(script);
            }
            else
            {
                Debug.LogWarning($"[VisualNodeEditor] The script for '{type.FullName}' was not found. Its file name must match the class name.");
            }
        }
    }
}
