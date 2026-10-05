using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Editor.Views
{
    /// <summary><see cref="GroupData"/> 1 件に対応する GraphView のグループ枠。</summary>
    public sealed class GroupView : Group
    {
        public GroupView(GroupData data)
        {
            title = data.Title;
            viewDataKey = data.Id;
            AddToClassList("vne-group");
            SetPosition(new Rect(data.Position, Vector2.zero));
        }

        /// <summary>表示しているグループの ID。</summary>
        public string GroupId => viewDataKey;
    }
}
