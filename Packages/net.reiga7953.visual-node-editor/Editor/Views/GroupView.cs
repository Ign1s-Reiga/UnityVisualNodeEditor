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

        /// <summary>
        /// 付箋はグループに入れない。<see cref="GroupData"/> はノードしか保存しないため、入れられると
        /// 一時的にはグループと一緒に動くが、再読み込み後に外へ出てしまい表示と保存内容が食い違う。
        /// </summary>
        public override bool AcceptsElement(GraphElement element, ref string reasonWhyNotAccepted)
        {
            if (element is StickyNote)
            {
                reasonWhyNotAccepted = "Sticky notes cannot be added to a group.";
                return false;
            }

            return base.AcceptsElement(element, ref reasonWhyNotAccepted);
        }
    }
}
