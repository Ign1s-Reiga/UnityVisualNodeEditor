using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor.Views;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Tests
{
    public sealed class NodeAlignmentTests
    {
        // 幅・高さの違うノード 3 つ
        private static readonly Rect[] Rects =
        {
            new Rect(100f, 50f, 100f, 40f),
            new Rect(10f, 200f, 60f, 80f),
            new Rect(300f, 120f, 80f, 20f),
        };

        [TestCase(12f, 18f, 20f, 20f)]
        [TestCase(9.9f, -9.9f, 0f, 0f)]
        [TestCase(30f, 50f, 40f, 60f)]
        [TestCase(-31f, 29f, -40f, 20f)]
        public void Snap_RoundsToNearestGridPoint(float x, float y, float expectedX, float expectedY)
        {
            Assert.That(GridSnap.Snap(new Vector2(x, y), 20f), Is.EqualTo(new Vector2(expectedX, expectedY)));
        }

        [Test]
        public void Snap_WithoutSpacing_KeepsPosition()
        {
            Assert.That(GridSnap.Snap(new Vector2(3f, 7f), 0f), Is.EqualTo(new Vector2(3f, 7f)));
        }

        [Test]
        public void GridSpacing_MatchesUss()
        {
            Assert.That(NodeGraphView.GridSpacing, Is.EqualTo(20f), "keep in sync with --spacing in NodeGraphView.uss");
        }

        [Test]
        public void Align_LeftRightTopBottom()
        {
            Assert.That(NodeAlignment.Align(Rects, AlignMode.Left),
                Is.EqualTo(new[] { new Vector2(10f, 50f), new Vector2(10f, 200f), new Vector2(10f, 120f) }));
            Assert.That(NodeAlignment.Align(Rects, AlignMode.Right),
                Is.EqualTo(new[] { new Vector2(280f, 50f), new Vector2(320f, 200f), new Vector2(300f, 120f) }));
            Assert.That(NodeAlignment.Align(Rects, AlignMode.Top),
                Is.EqualTo(new[] { new Vector2(100f, 50f), new Vector2(10f, 50f), new Vector2(300f, 50f) }));
            Assert.That(NodeAlignment.Align(Rects, AlignMode.Bottom),
                Is.EqualTo(new[] { new Vector2(100f, 240f), new Vector2(10f, 200f), new Vector2(300f, 260f) }));
        }

        [Test]
        public void Align_Centers()
        {
            // 全体: x 10..380（中心 195）、y 50..280（中心 165）
            Assert.That(NodeAlignment.Align(Rects, AlignMode.CenterHorizontally),
                Is.EqualTo(new[] { new Vector2(145f, 50f), new Vector2(165f, 200f), new Vector2(155f, 120f) }));
            Assert.That(NodeAlignment.Align(Rects, AlignMode.CenterVertically),
                Is.EqualTo(new[] { new Vector2(100f, 145f), new Vector2(10f, 125f), new Vector2(300f, 155f) }));
        }

        [Test]
        public void Distribute_Horizontally_KeepsEndsAndEqualizesGaps()
        {
            // 左から: [10..70] [100..200] [300..380]、幅の合計 240、全体 370 → 間隔 65
            var positions = NodeAlignment.Distribute(Rects, DistributeAxis.Horizontal);

            Assert.That(positions[1], Is.EqualTo(new Vector2(10f, 200f)), "leftmost stays");
            Assert.That(positions[2], Is.EqualTo(new Vector2(300f, 120f)), "rightmost stays");
            Assert.That(positions[0], Is.EqualTo(new Vector2(135f, 50f)), "middle node: 70 + 65");
        }

        [Test]
        public void Distribute_Vertically_KeepsEndsAndEqualizesGaps()
        {
            // 上から: [50..90] [120..140] [200..280]、高さの合計 140、全体 230 → 間隔 45
            var positions = NodeAlignment.Distribute(Rects, DistributeAxis.Vertical);

            Assert.That(positions[0], Is.EqualTo(new Vector2(100f, 50f)));
            Assert.That(positions[1], Is.EqualTo(new Vector2(10f, 200f)));
            Assert.That(positions[2], Is.EqualTo(new Vector2(300f, 135f)), "middle node: 90 + 45");
        }

        [Test]
        public void Distribute_FewerThanThree_DoesNothing()
        {
            var two = new[] { Rects[0], Rects[1] };

            Assert.That(NodeAlignment.Distribute(two, DistributeAxis.Horizontal), Is.EqualTo(new[] { two[0].position, two[1].position }));
            Assert.That(NodeAlignment.Align(new Rect[0], AlignMode.Left), Is.Empty);
        }
    }
}
