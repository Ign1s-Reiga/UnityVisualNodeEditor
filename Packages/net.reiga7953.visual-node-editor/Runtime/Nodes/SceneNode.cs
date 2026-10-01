using System;
using UnityEngine;

namespace Reiga.VisualNodeEditor
{
    /// <summary>Unity シーン（またはステージ）を表すノード。</summary>
    [Serializable]
    [NodeMenu("Flow/Scene")]
    public sealed class SceneNode : NodeData
    {
        [SerializeField] private string _sceneName;

        public string SceneName
        {
            get => _sceneName;
            set => _sceneName = value;
        }

        protected override string DefaultTitle => "Scene";
    }
}
