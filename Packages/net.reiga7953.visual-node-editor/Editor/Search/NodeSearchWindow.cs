using System;
using System.Collections.Generic;
using System.Linq;
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
        private PendingConnection _pending;

        /// <summary>ノードの追加先と、マウス座標の変換に使うウィンドウを設定する。</summary>
        public void Initialize(EditorWindow window, NodeGraphView graphView)
        {
            _window = window;
            _graphView = graphView;
        }

        /// <summary>
        /// 次に選ぶノードを、<paramref name="pending"/> のポートと繋いで作る（空き地へエッジを落としたとき）。null なら繋がずに作る。
        /// </summary>
        public void SetPendingConnection(PendingConnection pending) => _pending = pending;

        /// <inheritdoc />
        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            // 表示中の階層で置けるノードだけを出す（ルートの Entry はルートだけ、コンテナの Exit はコンテナの中だけ）。
            // エッジから作るときは、そのポートと繋げるノードだけ
            var insideContainer = _graphView != null && _graphView.CurrentContainerId.Length > 0;
            var items = NodeMenuCatalog.GetItems(insideContainer);
            if (_pending != null)
            {
                items = items.FindAll(item => ConnectionCandidates.CanConnect(item.NodeType, _pending.Direction));
            }

            return BuildSearchTree(items, _indentIcon, ConnectionCandidates.GetFeaturedType(_pending));
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
            var pending = _pending;
            _pending = null;
            return pending != null
                ? _graphView.CreateConnectedNode(nodeType, graphMouse, pending) != null
                : _graphView.CreateNode(nodeType, graphMouse) != null;
        }

        /// <summary>
        /// パス順に並んだ項目から検索ツリーを組み立てる。"A/B/C" は グループ A → グループ B → 項目 C になる。
        /// <paramref name="featuredType"/> が項目にあれば、その項目を最上位の先頭にも出す（よく使うものをすぐ選べるように）。
        /// </summary>
        public static List<SearchTreeEntry> BuildSearchTree(IEnumerable<NodeMenuItem> items, Texture icon, Type featuredType = null)
        {
            var tree = new List<SearchTreeEntry> { new SearchTreeGroupEntry(new GUIContent("Create Node"), 0) };
            var openGroups = new List<string>();
            var itemList = items.ToList();
            var featured = itemList.FirstOrDefault(item => featuredType != null && item.NodeType == featuredType);
            if (featured.NodeType != null)
            {
                tree.Add(new SearchTreeEntry(new GUIContent(featured.Path.Split('/').Last(), icon))
                {
                    level = 1,
                    userData = featured.NodeType,
                });
            }

            foreach (var item in itemList)
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
