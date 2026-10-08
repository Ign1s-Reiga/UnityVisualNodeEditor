using System;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Editor.Behaviours
{
    /// <summary>
    /// Create Script… の後、コンパイルが終わったら追加する振る舞い（どのアセットの、どのノードへ、どのクラスを）。
    /// ドメインリロードを越えて残すため、JSON にして <c>SessionState</c> に置く。
    /// </summary>
    [Serializable]
    internal sealed class PendingBehaviourScript
    {
        [SerializeField] private string _assetGuid;
        [SerializeField] private string _nodeId;
        [SerializeField] private string _className;

        /// <summary>JsonUtility が読み戻すときに使う。</summary>
        public PendingBehaviourScript()
        {
        }

        public PendingBehaviourScript(string assetGuid, string nodeId, string className)
        {
            _assetGuid = assetGuid;
            _nodeId = nodeId;
            _className = className;
        }

        public string AssetGuid => _assetGuid;

        public string NodeId => _nodeId;

        public string ClassName => _className;
    }
}
