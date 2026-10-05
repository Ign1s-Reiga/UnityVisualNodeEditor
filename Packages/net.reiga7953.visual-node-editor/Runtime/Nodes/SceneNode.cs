using System;
using UnityEngine;

namespace Reiga.VisualNodeEditor
{
    /// <summary>Unity シーン（またはステージ）を表すノード。</summary>
    [Serializable]
    [NodeMenu("Flow/Scene")]
    public sealed class SceneNode : NodeData
    {
        [SerializeField] private SceneReference _scene = new SceneReference();

        /// <summary>このノードが表すシーン。</summary>
        public SceneReference Scene
        {
            get => _scene;
            set => _scene = value ?? new SceneReference();
        }

        /// <summary>シーン名。未設定なら空文字。</summary>
        public string SceneName => _scene.Name;

        protected override string DefaultTitle => "Scene";
    }
}
