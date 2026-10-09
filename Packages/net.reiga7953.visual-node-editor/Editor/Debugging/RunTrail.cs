using System.Collections.Generic;

namespace Reiga.VisualNodeEditor.Editor.Debugging
{
    /// <summary>
    /// Play 中に直近で通ったノード（今いるノードは含まない。新しい順）。グラフで薄く強調して、どこから来たかを見せる。
    /// </summary>
    public sealed class RunTrail
    {
        /// <summary>覚えておくノードの数の既定値。</summary>
        public const int DefaultCapacity = 4;

        private readonly List<string> _nodeIds = new();
        private readonly int _capacity;
        private string _current;

        public RunTrail(int capacity = DefaultCapacity)
        {
            _capacity = capacity < 1 ? 1 : capacity;
        }

        /// <summary>直近で通ったノードの ID（新しい順。今いるノードは含まない）。</summary>
        public IReadOnlyList<string> NodeIds => _nodeIds;

        /// <summary>
        /// <paramref name="nodeId"/> に入った（null なら止まった）。それまでいたノードを軌跡の先頭に積み、古いものから捨てる。
        /// </summary>
        public void Visit(string nodeId)
        {
            if (_current == nodeId)
            {
                return;
            }

            if (_current != null)
            {
                _nodeIds.Remove(_current);
                _nodeIds.Insert(0, _current);
            }

            if (nodeId != null)
            {
                _nodeIds.Remove(nodeId);
            }

            if (_nodeIds.Count > _capacity)
            {
                _nodeIds.RemoveRange(_capacity, _nodeIds.Count - _capacity);
            }

            _current = nodeId;
        }

        /// <summary>軌跡を消す（新しく実行を見始めたとき・Play を終えたとき）。</summary>
        public void Clear()
        {
            _nodeIds.Clear();
            _current = null;
        }
    }
}
