using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor
{
    /// <summary>Unity シーン（またはステージ）を表すノード。<see cref="NodeBehaviour"/> を付けると、このシーンにいる間の処理を書ける。</summary>
    [Serializable]
    [NodeMenu("Flow/Scene")]
    public sealed class SceneNode : NodeData, IBehaviourHost, IHasScreen
    {
        [SerializeField] private SceneReference _scene = new SceneReference();

        [Tooltip("Coming back to this node while the graph is still in this scene loads the scene again (retries, respawns), so its objects start over. " +
                 "Off: the scene is not loaded again and its objects keep their state.")]
        [SerializeField] private bool _reload;

        // インスペクタでは専用の欄（Create Screen… / Edit Screen 付き）で編集する（NodeInspectorView が名前で除外する）
        [Tooltip("The UI Toolkit screen (UXML) shown while the graph is on this node.")]
        [SerializeField] private VisualTreeAsset _screen;

        // インスペクタでは専用の一覧で編集する（NodeInspectorView が名前で除外する）。
        // [HideInInspector] を付けると、振る舞いの中のフィールドまでインスペクタから見えなくなるので付けない
        [SerializeReference] private List<NodeBehaviour> _behaviours = new();

        /// <summary>このノードが表すシーン。</summary>
        public SceneReference Scene
        {
            get => _scene;
            set => _scene = value ?? new SceneReference();
        }

        /// <summary>シーン名。未設定なら空文字。</summary>
        public string SceneName => _scene.Name;

        /// <summary>
        /// 同じシーンにいるまま（Runner が最後に読み込んだシーンがこのシーンのとき）このノードに入り直したら、シーンを読み込み直すか（やり直し・ミスからの再開）。
        /// 既定は false（読み込み直さず、シーンの中の物は前の状態のまま）。
        /// </summary>
        public bool Reload
        {
            get => _reload;
            set => _reload = value;
        }

        /// <inheritdoc />
        public VisualTreeAsset Screen
        {
            get => _screen;
            set => _screen = value;
        }

        /// <inheritdoc />
        public IReadOnlyList<NodeBehaviour> Behaviours => _behaviours;

        /// <inheritdoc />
        public void AddBehaviour(NodeBehaviour behaviour) => NodeBehaviourList.Add(_behaviours, behaviour);

        /// <inheritdoc />
        public bool RemoveBehaviourAt(int index) => NodeBehaviourList.RemoveAt(_behaviours, index);

        /// <inheritdoc />
        public bool MoveBehaviour(int index, int newIndex) => NodeBehaviourList.Move(_behaviours, index, newIndex);

        protected override string DefaultTitle => "Scene";
    }
}
