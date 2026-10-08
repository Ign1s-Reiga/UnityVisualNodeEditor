using System;
using System.Collections.Generic;
using Reiga.VisualNodeEditor.Editor.Views;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Editor.Search
{
    /// <summary>
    /// ノード追加用の検索ウィンドウ。<see cref="NodeMenuCatalog"/> の項目をパスで階層化して表示する。
    /// </summary>
    public sealed class NodeSearchWindow : ScriptableObject, ISearchWindowProvider
    {
        private EditorWindow _window;
        private NodeGraphView _graphView;
        private Texture2D _indentIcon;

        /// <summary>ノードの追加先と、マウス座標の変換に使うウィンドウを設定する。</summary>
        public void Initialize(EditorWindow window, NodeGraphView graphView)
        {
            _window = window;
            _graphView = graphView;
        }

        /// <inheritdoc />
        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            // 表示中の階層で置けるノードだけを出す（ルートの Entry はルートだけ、コンテナの Exit はコンテナの中だけ）
            var insideContainer = _graphView != null && _graphView.CurrentContainerId.Length > 0;
            return BuildSearchTree(NodeMenuCatalog.GetItems(insideContainer), _indentIcon);
        }

        /// <inheritdoc />
        public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
        {
            if (!(entry.userData is Type nodeType) || _window == null || _graphView == null)
            {
                return false;
            }

            var root = _window.rootVisualElement;
            var windowMouse = root.ChangeCoordinatesTo(root.parent,
                context.screenMousePosition - _window.position.position);
            var graphMouse = _graphView.contentViewContainer.WorldToLocal(windowMouse);
            return _graphView.CreateNode(nodeType, graphMouse) != null;
        }

        /// <summary>
        /// パス順に並んだ項目から検索ツリーを組み立てる。"A/B/C" は グループ A → グループ B → 項目 C になる。
        /// </summary>
        public static List<SearchTreeEntry> BuildSearchTree(IEnumerable<NodeMenuItem> items, Texture icon)
        {
            var tree = new List<SearchTreeEntry> { new SearchTreeGroupEntry(new GUIContent("Create Node"), 0) };
            var openGroups = new List<string>();

            foreach (var item in items)
            {
                var segments = item.Path.Split('/');
                var groupCount = segments.Length - 1;

                var shared = 0;
                while (shared < openGroups.Count && shared < groupCount && openGroups[shared] == segments[shared])
                {
                    shared++;
                }

                openGroups.RemoveRange(shared, openGroups.Count - shared);
                for (var i = shared; i < groupCount; i++)
                {
                    tree.Add(new SearchTreeGroupEntry(new GUIContent(segments[i]), i + 1));
                    openGroups.Add(segments[i]);
                }

                tree.Add(new SearchTreeEntry(new GUIContent(segments[groupCount], icon))
                {
                    level = groupCount + 1,
                    userData = item.NodeType,
                });
            }

            return tree;
        }

        private void OnEnable()
        {
            // 項目名をグループ名と揃えるための透明アイコン（SearchWindow はアイコン無しだと字下げがずれる）
            _indentIcon = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            _indentIcon.SetPixel(0, 0, Color.clear);
            _indentIcon.Apply();
        }

        private void OnDisable()
        {
            if (_indentIcon != null)
            {
                DestroyImmediate(_indentIcon);
            }
        }
    }
}
