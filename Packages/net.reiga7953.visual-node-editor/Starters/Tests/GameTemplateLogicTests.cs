using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Starters.Tests
{
    /// <summary>ゲームのスクリプトのひな形の遊びの規則（スネークのぶつかり方・えさ、スライドパズルの動かし方・混ぜ方・揃い方）。</summary>
    public sealed class GameTemplateLogicTests
    {
        [Test]
        public void Snake_StaysOnTheBoard()
        {
            Assert.That(SnakeGameTemplate.IsInside(new Vector2Int(0, 0), 20, 20), Is.True);
            Assert.That(SnakeGameTemplate.IsInside(new Vector2Int(19, 19), 20, 20), Is.True);
            Assert.That(SnakeGameTemplate.IsInside(new Vector2Int(-1, 5), 20, 20), Is.False);
            Assert.That(SnakeGameTemplate.IsInside(new Vector2Int(5, 20), 20, 20), Is.False);
        }

        [Test]
        public void Snake_CannotTurnStraightBack()
        {
            Assert.That(SnakeGameTemplate.IsReverse(Vector2Int.left, Vector2Int.right), Is.True);
            Assert.That(SnakeGameTemplate.IsReverse(Vector2Int.up, Vector2Int.right), Is.False);
        }

        [Test]
        public void Snake_HitsItsBody_ButNotTheTailThatMovesAway()
        {
            var body = new List<Vector2Int> { new Vector2Int(2, 2), new Vector2Int(2, 1), new Vector2Int(1, 1), new Vector2Int(1, 2) };

            Assert.That(SnakeGameTemplate.HitsBody(body, new Vector2Int(2, 1), false), Is.True);
            Assert.That(SnakeGameTemplate.HitsBody(body, new Vector2Int(1, 2), false), Is.False, "the tail leaves its cell in the same step");
            Assert.That(SnakeGameTemplate.HitsBody(body, new Vector2Int(1, 2), true), Is.True, "unless the snake grows");
            Assert.That(SnakeGameTemplate.HitsBody(body, new Vector2Int(3, 2), false), Is.False);
        }

        [Test]
        public void Snake_FoodIsPlacedOnAFreeCell_OrNowhereWhenTheBoardIsFull()
        {
            var body = new List<Vector2Int> { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(0, 1) };
            var random = new System.Random(1);
            for (var i = 0; i < 20; i++)
            {
                Assert.That(SnakeGameTemplate.PlaceFood(body, 2, 2, random), Is.EqualTo(new Vector2Int(1, 1)));
            }

            body.Add(new Vector2Int(1, 1));
            Assert.That(SnakeGameTemplate.PlaceFood(body, 2, 2, random), Is.EqualTo(new Vector2Int(-1, -1)));
        }

        [Test]
        public void Puzzle_SolvedBoard_HasTheGapLast()
        {
            var cells = SlidingPuzzleGameTemplate.CreateSolved(3);

            Assert.That(cells, Is.EqualTo(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 0 }));
            Assert.That(SlidingPuzzleGameTemplate.IsSolved(cells), Is.True);
        }

        [Test]
        public void Puzzle_OnlyTilesNextToTheGapSlide()
        {
            var cells = SlidingPuzzleGameTemplate.CreateSolved(3);

            Assert.That(SlidingPuzzleGameTemplate.Slide(cells, 3, 0), Is.False, "far from the gap");
            Assert.That(SlidingPuzzleGameTemplate.Slide(cells, 3, 4), Is.False, "diagonal");
            Assert.That(SlidingPuzzleGameTemplate.Slide(cells, 3, 7), Is.True, "left of the gap");
            Assert.That(cells, Is.EqualTo(new[] { 1, 2, 3, 4, 5, 6, 7, 0, 8 }));
            Assert.That(SlidingPuzzleGameTemplate.IsSolved(cells), Is.False);
            Assert.That(SlidingPuzzleGameTemplate.Slide(cells, 3, 8), Is.True, "and back");
            Assert.That(SlidingPuzzleGameTemplate.IsSolved(cells), Is.True);
        }

        [Test]
        public void Puzzle_ArrowKeysPickTheTileThatMovesIntoTheGap()
        {
            // 1 2 3 / 4 _ 5 / 6 7 8: 隙間は真ん中（セル 4）
            var cells = new[] { 1, 2, 3, 4, 0, 5, 6, 7, 8 };

            Assert.That(SlidingPuzzleGameTemplate.FindCellToSlide(cells, 3, Vector2Int.up), Is.EqualTo(7), "up: the tile below moves up");
            Assert.That(SlidingPuzzleGameTemplate.FindCellToSlide(cells, 3, Vector2Int.down), Is.EqualTo(1), "down: the tile above moves down");
            Assert.That(SlidingPuzzleGameTemplate.FindCellToSlide(cells, 3, Vector2Int.left), Is.EqualTo(5), "left: the tile on the right moves left");
            Assert.That(SlidingPuzzleGameTemplate.FindCellToSlide(cells, 3, Vector2Int.right), Is.EqualTo(3), "right: the tile on the left moves right");

            var corner = SlidingPuzzleGameTemplate.CreateSolved(3);
            Assert.That(SlidingPuzzleGameTemplate.FindCellToSlide(corner, 3, Vector2Int.up), Is.EqualTo(-1), "nothing below the bottom row");
        }

        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void Puzzle_ShuffledBoards_AreMixedUp_AndCanBeSolved(int size)
        {
            var cells = SlidingPuzzleGameTemplate.CreateSolved(size);
            SlidingPuzzleGameTemplate.Shuffle(cells, size, 200, new System.Random(size));

            Assert.That(SlidingPuzzleGameTemplate.IsSolved(cells), Is.False);
            Assert.That(cells, Is.EquivalentTo(SlidingPuzzleGameTemplate.CreateSolved(size)), "the same tiles, moved");
            Assert.That(IsSolvable(cells, size), Is.True);
        }

        [Test]
        public void ToolkitTemplates_PlayByTheSameRules()
        {
            // UI Toolkit 版は描き方だけが違い、遊びの規則は uGUI 版と同じ
            var body = new List<Vector2Int> { new Vector2Int(2, 2), new Vector2Int(2, 1), new Vector2Int(1, 1), new Vector2Int(1, 2) };
            Assert.That(SnakeGameToolkitTemplate.IsInside(new Vector2Int(19, 0), 20, 20), Is.True);
            Assert.That(SnakeGameToolkitTemplate.IsReverse(Vector2Int.down, Vector2Int.up), Is.True);
            Assert.That(SnakeGameToolkitTemplate.HitsBody(body, new Vector2Int(1, 2), false), Is.False);
            Assert.That(SnakeGameToolkitTemplate.HitsBody(body, new Vector2Int(1, 2), true), Is.True);
            Assert.That(SnakeGameToolkitTemplate.PlaceFood(body, 2, 2, new System.Random(1)), Is.Not.EqualTo(new Vector2Int(1, 1)).And.Not.EqualTo(new Vector2Int(2, 2)));

            var cells = new[] { 1, 2, 3, 4, 0, 5, 6, 7, 8 };
            Assert.That(SlidingPuzzleGameToolkitTemplate.FindCellToSlide(cells, 3, Vector2Int.up), Is.EqualTo(7));
            Assert.That(SlidingPuzzleGameToolkitTemplate.Slide(cells, 3, 5), Is.True);
            Assert.That(cells, Is.EqualTo(new[] { 1, 2, 3, 4, 5, 0, 6, 7, 8 }));
            var shuffled = SlidingPuzzleGameToolkitTemplate.CreateSolved(4);
            SlidingPuzzleGameToolkitTemplate.Shuffle(shuffled, 4, 200, new System.Random(4));
            Assert.That(SlidingPuzzleGameToolkitTemplate.IsSolved(shuffled), Is.False);
            Assert.That(IsSolvable(shuffled, 4), Is.True);
        }

        // 15 パズルが解ける並びか（転倒数の偶奇と、隙間の行で決まる）
        private static bool IsSolvable(int[] cells, int size)
        {
            var inversions = 0;
            for (var i = 0; i < cells.Length; i++)
            {
                for (var j = i + 1; j < cells.Length; j++)
                {
                    if (cells[i] != 0 && cells[j] != 0 && cells[i] > cells[j])
                    {
                        inversions++;
                    }
                }
            }

            if (size % 2 == 1)
            {
                return inversions % 2 == 0;
            }

            var gapRowFromBottom = size - System.Array.IndexOf(cells, 0) / size;
            return (inversions + gapRowFromBottom) % 2 == 1;
        }
    }
}
