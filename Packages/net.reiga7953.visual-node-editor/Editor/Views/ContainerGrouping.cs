using System.Collections.Generic;
using System.Linq;
using Reiga.VisualNodeEditor.Editor.Inspector;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Editor.Views
{
    /// <summary>
    /// 選択したノードを新しいコンテナにまとめる（右クリック → Group into Container）。
    /// 外から入ってくるエッジはコンテナの入力へ付け替え、中では Entry をその行き先へ繋ぐ。
    /// 外へ出ていくエッジは、出元のポートごとに出口を作り、中ではその出口の Exit ノードへ、外ではその出口のポートから元の行き先へ繋ぐ。
    /// Event ノードは、そこへ繋がっているノードと同じ側に置く（<see cref="GraphRunner.Raise"/> は直接繋がった Event だけを探すため）。
    /// アセットのデータだけを変える（Undo の記録と表示の作り直しは呼び出し側で行う）。
    /// </summary>
    public static class ContainerGrouping
    {
        // 中に置く Entry / Exit ノードを、まとめたノードの左右にどれだけ離して置くか
        private const float SideMargin = 300f;
        private const float ExitSpacing = 100f;

        private const string NothingToGroupMessage =
            "Nothing to group. Entry nodes stay where they are, and events stay with the nodes they come from.";

        /// <summary>まとめられるノードか（ルートの Entry と、コンテナの Entry / Exit は動かせない）。</summary>
        public static bool CanGroup(NodeData node) =>
            node != null && !(node is EntryNode) && !(node is ContainerEntryNode) && !(node is ContainerExitNode);

        /// <summary>
        /// <paramref name="levelId"/> の階層で <paramref name="selectedIds"/> をまとめるとき、コンテナに入れるノード（アセット内の順）。
        /// Event ノードは繋がっている元と同じ側へ寄せる（元がすべて中なら選んでいなくても入れ、すべて外なら選んでいても外に残す）。
        /// まとめられなければ null を返し、<paramref name="problem"/> に理由を入れる。
        /// </summary>
        public static List<NodeData> Plan(NodeGraphAsset asset, IEnumerable<string> selectedIds, string levelId, out string problem)
        {
            problem = NothingToGroupMessage;
            if (asset == null)
            {
                return null;
            }

            var level = levelId ?? string.Empty;
            var levelNodes = asset.Nodes.Where(n => n != null && NodeGraphAsset.IsSameLevel(n.ParentId, level)).ToList();
            var levelIds = new HashSet<string>(levelNodes.Select(n => n.Id));
            var moved = new HashSet<string>((selectedIds ?? Enumerable.Empty<string>())
                .Where(id => id != null && levelIds.Contains(id) && CanGroup(asset.FindNode(id))));

            // Event ノードごとの、そこへ繋がっている元（同じ階層のノード）
            var sourcesByEvent = levelNodes.OfType<EventNode>().ToDictionary(
                e => e.Id,
                e => asset.Edges.Where(edge => edge.ToNodeId == e.Id && levelIds.Contains(edge.FromNodeId))
                    .Select(edge => edge.FromNodeId).Distinct().ToList());

            // Event → Event と連なることもあるので、寄せても変わらなくなるまで繰り返す（輪で行き来しないよう回数を区切る）
            for (var pass = 0; pass <= sourcesByEvent.Count; pass++)
            {
                var changed = false;
                foreach (var pair in sourcesByEvent.Where(p => p.Value.Count > 0))
                {
                    var inside = pair.Value.Count(moved.Contains);
                    if (inside == pair.Value.Count)
                    {
                        changed |= moved.Add(pair.Key);
                    }
                    else if (inside == 0)
                    {
                        changed |= moved.Remove(pair.Key);
                    }
                }

                if (!changed)
                {
                    break;
                }
            }

            var split = sourcesByEvent.FirstOrDefault(p => p.Value.Any(moved.Contains) && !p.Value.All(moved.Contains));
            if (split.Key != null)
            {
                problem = $"Cannot group: event '{NodeDisplay.GetNodeLabel(asset.FindNode(split.Key))}' comes from nodes both inside and outside the selection. " +
                          "Select all of them, or none.";
                return null;
            }

            if (moved.Count == 0)
            {
                return null;
            }

            // コンテナの入口は 1 つなので、外から別々のノードへ入っていると、まとめる前の入り方を保てない
            var entryPoints = asset.Edges
                .Where(e => moved.Contains(e.ToNodeId) && levelIds.Contains(e.FromNodeId) && !moved.Contains(e.FromNodeId))
                .Select(e => e.ToNodeId)
                .Distinct()
                .ToList();
            if (entryPoints.Count > 1)
            {
                var names = string.Join(", ", entryPoints.Select(id => $"'{NodeDisplay.GetNodeLabel(asset.FindNode(id))}'"));
                problem = $"Cannot group: the selection is entered from outside at {names}. A container has one way in, so group nodes that are entered at one place.";
                return null;
            }

            problem = null;
            return levelNodes.Where(n => moved.Contains(n.Id)).ToList();
        }

        /// <summary>
        /// <paramref name="levelId"/> の階層で <paramref name="selectedIds"/> を（<see cref="Plan"/> のとおりに）新しいコンテナに入れ、
        /// そのコンテナを返す。まとめられなければ何も変えずに null を返し、<paramref name="problem"/> に理由を入れる。
        /// </summary>
        public static ContainerNode Apply(NodeGraphAsset asset, IEnumerable<string> selectedIds, string levelId, out string problem)
        {
            var moved = Plan(asset, selectedIds, levelId, out problem);
            if (moved == null)
            {
                return null;
            }

            var level = levelId ?? string.Empty;
            var movedIds = new HashSet<string>(moved.Select(n => n.Id));
            bool IsOutside(string id) => !movedIds.Contains(id) && asset.FindNode(id) is NodeData n && NodeGraphAsset.IsSameLevel(n.ParentId, level);
            var incoming = asset.Edges.Where(e => IsOutside(e.FromNodeId) && movedIds.Contains(e.ToNodeId)).ToList();
            var outgoing = asset.Edges.Where(e => movedIds.Contains(e.FromNodeId) && IsOutside(e.ToNodeId)).ToList();

            var minX = moved.Min(n => n.Position.x);
            var maxX = moved.Max(n => n.Position.x);
            var midY = moved.Average(n => n.Position.y);

            // まとめたノードがあった所にコンテナを置く
            var container = new ContainerNode { ParentId = level, Position = new Vector2(minX, midY) };
            asset.AddNode(container);
            foreach (var node in moved)
            {
                node.ParentId = container.Id;
                foreach (var group in asset.Groups)
                {
                    // グループは置かれた階層のものなので、中へ移したノードは外のグループから外す
                    group.RemoveNode(node.Id);
                }
            }

            // 入口: 外からのエッジはコンテナの入力へ。中では Entry をその行き先（Plan で 1 つに決まっている）へ。
            // 入ってくるエッジが無ければ、一番左の State・Scene・コンテナへ（Note はポートが無く、Event は Entry から通らない）
            var entry = new ContainerEntryNode { ParentId = container.Id, Position = new Vector2(minX - SideMargin, midY) };
            asset.AddNode(entry);
            var firstIncoming = incoming.FirstOrDefault();
            var startNodeId = firstIncoming?.ToNodeId ?? moved
                .Where(n => ConnectionCandidates.IsWaitNode(n) || n is ContainerNode)
                .OrderBy(n => n.Position.x)
                .FirstOrDefault()?.Id;
            if (startNodeId != null)
            {
                asset.AddEdge(new EdgeData(entry.Id, NodeView.OutputPortName, startNodeId, firstIncoming?.ToPort ?? NodeView.InputPortName));
            }

            // 付け替えは同じ位置で置き換える（エッジの順が Advance などの行き先を決めるため）
            foreach (var edge in incoming)
            {
                var replacement = new EdgeData(edge.FromNodeId, edge.FromPort, container.Id, ContainerNode.InputPortId);
                if (asset.FindEdge(replacement.FromNodeId, replacement.FromPort, replacement.ToNodeId, replacement.ToPort) == null)
                {
                    asset.ReplaceEdge(edge, replacement);
                }
                else
                {
                    asset.RemoveEdge(edge);
                }
            }

            // 出口: 外へのエッジを出元のポートごとにまとめて出口にする。外へ出ていなければ既定の出口（Next）のまま
            var sources = outgoing.Select(e => (e.FromNodeId, e.FromPort)).Distinct().ToList();
            if (sources.Count == 0)
            {
                for (var i = 0; i < container.Exits.Count; i++)
                {
                    AddExitNode(asset, container, container.Exits[i], GetExitPosition(maxX, midY, i));
                }

                return container;
            }

            foreach (var defaultExit in container.Exits.ToList())
            {
                container.RemoveExit(defaultExit.Id);
            }

            for (var i = 0; i < sources.Count; i++)
            {
                var (fromNodeId, fromPort) = sources[i];
                var exit = container.AddExit(ContainerExitEditing.GetUniqueName(container, GetExitName(asset, fromNodeId, outgoing)));
                var exitNode = AddExitNode(asset, container, exit, GetExitPosition(maxX, midY, i));

                // 中: 出元のポートの最初の外へのエッジを、その位置のまま Exit ノードへのエッジにする（残りは消す）。
                // 外: コンテナの出口から元の行き先へ、元の順で繋ぐ
                var leaving = outgoing.Where(e => e.FromNodeId == fromNodeId && e.FromPort == fromPort).ToList();
                asset.ReplaceEdge(leaving[0], new EdgeData(fromNodeId, fromPort, exitNode.Id, NodeView.InputPortName));
                foreach (var edge in leaving)
                {
                    asset.RemoveEdge(edge);
                    asset.AddEdge(new EdgeData(container.Id, exit.Id, edge.ToNodeId, edge.ToPort));
                }
            }

            return container;
        }

        // 出口の名前: 出元が名前のある Event ならそのイベント名。そうでなければ（最初の）行き先の名前
        // （行き先が Event のエッジは境界をまたがない。Event は繋がっている元と同じ側に置くため）
        private static string GetExitName(NodeGraphAsset asset, string fromNodeId, IEnumerable<EdgeData> outgoing)
        {
            if (asset.FindNode(fromNodeId) is EventNode source && !string.IsNullOrEmpty(source.EventName))
            {
                return source.EventName;
            }

            return NodeDisplay.GetNodeLabel(asset.FindNode(outgoing.First(e => e.FromNodeId == fromNodeId).ToNodeId));
        }

        private static Vector2 GetExitPosition(float maxX, float midY, int index) =>
            new Vector2(maxX + SideMargin, midY + ExitSpacing * index);

        private static ContainerExitNode AddExitNode(NodeGraphAsset asset, ContainerNode container, ContainerExit exit, Vector2 position)
        {
            var exitNode = new ContainerExitNode { ParentId = container.Id, ExitId = exit.Id, Position = position };
            asset.AddNode(exitNode);
            return exitNode;
        }
    }
}
