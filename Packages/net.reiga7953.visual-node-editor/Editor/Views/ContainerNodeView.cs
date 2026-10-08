using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Editor.Views
{
    /// <summary>
    /// コンテナ（<see cref="ContainerNode"/>）の View。入力 1 つと、出口ごとの出力（ID = 出口の ID、ラベル = 出口の名前）を持つ。
    /// サマリーに中のノードの数を出し、ダブルクリックで中の階層を開く。
    /// <see cref="ContainerNode"/> のサブクラスも、専用の View を登録しなければこの View で表示する。
    /// </summary>
    [CustomNodeView(typeof(ContainerNode))]
    public class ContainerNodeView : NodeView
    {
        public ContainerNodeView()
        {
            RegisterCallback<MouseDownEvent>(OnMouseDown);
        }

        /// <summary>中のノードの数の表示（コンテナの Entry / Exit は数えない）。</summary>
        public static string FormatChildCount(int count) => count switch
        {
            0 => "Empty",
            1 => "1 node",
            _ => $"{count} nodes",
        };

        /// <summary>出口の名前を常に見せる（出口が 1 つでも、どの出口か分かるように）。</summary>
        protected override bool AlwaysShowPortLabels => true;

        protected override void CreatePorts()
        {
            AddInputPort(ContainerNode.InputPortId);
            if (Data is ContainerNode container)
            {
                foreach (var exit in container.Exits.Where(e => e != null))
                {
                    AddOutputPort(exit.Id, exit.Name);
                }
            }
        }

        protected override string GetSummary()
        {
            if (Graph == null)
            {
                return string.Empty;
            }

            var count = Graph.GetChildren(Data.Id).Count(n => !(n is ContainerEntryNode) && !(n is ContainerExitNode));
            return FormatChildCount(count);
        }

        private void OnMouseDown(MouseDownEvent evt)
        {
            // ポートのダブルクリック（エッジの操作）では開かない
            if (evt.clickCount != 2 || evt.button != (int)MouseButton.LeftMouse
                || (evt.target as VisualElement)?.GetFirstOfType<Port>() != null)
            {
                return;
            }

            var graphView = GetFirstAncestorOfType<NodeGraphView>();
            if (graphView == null)
            {
                return;
            }

            // クリックの処理中に自分ごと作り直さないよう、イベントの後で開く
            var containerId = NodeId;
            graphView.schedule.Execute(() => graphView.EnterLevel(containerId));
            evt.StopPropagation();
        }
    }
}
