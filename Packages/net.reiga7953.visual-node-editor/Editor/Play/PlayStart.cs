using System.Collections.Generic;

namespace Reiga.VisualNodeEditor.Editor.Play
{
    /// <summary>ワンクリック Play で、Graph Runner を置いて開くシーン（グラフが始まるシーン）を決める。</summary>
    public static class PlayStart
    {
        /// <summary>
        /// グラフが始まるシーン。Runner と同じように Entry から最初の待機ノードまでたどり、それが Scene ノードならそのシーン。
        /// そうでなければ（State から始まるなど）、Entry から辿れる最初の Scene ノードのシーン。シーンが無ければ null。
        /// </summary>
        public static SceneReference FindStartScene(NodeGraphAsset graph)
        {
            if (graph == null)
            {
                return null;
            }

            var query = new GraphQuery(graph);
            var entry = query.Entry;
            if (entry == null)
            {
                return null;
            }

            return FollowStartPath(query, entry) ?? FindFirstReachableScene(query, entry);
        }

        // GraphRunner.Start と同じたどり方（通過ノードは最初の Event 以外の先へ、コンテナは中の Entry へ、Exit は外のポートの先へ）
        private static SceneReference FollowStartPath(GraphQuery query, NodeData entry)
        {
            var visited = new HashSet<string>();
            for (var node = entry; node != null && visited.Add(node.Id);)
            {
                switch (node)
                {
                    case SceneNode scene:
                        return scene.Scene.IsEmpty ? null : scene.Scene;
                    case ContainerNode container:
                        node = query.GetContainerEntry(container);
                        break;
                    case ContainerExitNode exit:
                        node = query.GetExitTarget(exit);
                        break;
                    case EntryNode _:
                    case ContainerEntryNode _:
                        node = query.GetFirstNonEventNext(node.Id);
                        break;
                    default:
                        return null;
                }
            }

            return null;
        }

        // 幅優先で、Entry から辿れる最初のシーン付きの Scene ノード
        private static SceneReference FindFirstReachableScene(GraphQuery query, NodeData entry)
        {
            var visited = new HashSet<string>();
            var queue = new Queue<NodeData>();
            queue.Enqueue(entry);
            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                if (node == null || !visited.Add(node.Id))
                {
                    continue;
                }

                if (node is SceneNode scene && !scene.Scene.IsEmpty)
                {
                    return scene.Scene;
                }

                switch (node)
                {
                    case ContainerNode container:
                        queue.Enqueue(query.GetContainerEntry(container));
                        break;
                    case ContainerExitNode exit:
                        queue.Enqueue(query.GetExitTarget(exit));
                        break;
                }

                foreach (var next in query.GetNext(node.Id))
                {
                    queue.Enqueue(next);
                }
            }

            return null;
        }
    }
}
