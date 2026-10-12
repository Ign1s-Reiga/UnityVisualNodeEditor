using System.Collections.Generic;
using Reiga.VisualNodeEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Sliding puzzle (the 15 puzzle), made by the Sliding Puzzle Starter of Visual Node Editor. Change it freely: it is your game's code now.
/// <para>
/// The graph decides the flow (Shuffle → Playing ⇄ Paused → Solved → next level, or All Clear after the last one),
/// keeps the Level and Moves parameters, shows the panels, and turns keys into events. This script only plays the game:
/// when the graph enters the Shuffle node it builds a shuffled board for the Level parameter
/// (level 1 is 3 × 3, each level adds a row and a column), slides tiles while the graph is on the Playing node,
/// adds to Moves, and raises Solved when the tiles are back in order.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class SlidingPuzzleGameTemplate : MonoBehaviour
{
    [Tooltip("The graph this game plays in. Empty: any running graph.")]
    [SerializeField] private NodeGraphAsset _graph;

    [Header("Names in the graph")]
    [Tooltip("Entering this node builds a new shuffled board for the current level.")]
    [SerializeField] private string _shuffleNode = "Shuffle";

    [Tooltip("Tiles slide only while the graph is on this node.")]
    [SerializeField] private string _playingNode = "Playing";

    [Tooltip("Raised when the tiles are back in order.")]
    [SerializeField] private string _solvedEvent = "Solved";

    [Tooltip("Int parameter: the level (1 = the smallest board).")]
    [SerializeField] private string _levelParameter = "Level";

    [Tooltip("Int parameter: slides made on this board.")]
    [SerializeField] private string _movesParameter = "Moves";

    [Header("Board")]
    [Tooltip("Rows and columns at level 1. Each level adds one.")]
    [SerializeField] [Min(2)] private int _firstSize = 3;

    [Tooltip("Random slides used to shuffle (starting from the solved board, so every board can be solved).")]
    [SerializeField] [Min(1)] private int _shuffleMoves = 200;

    [SerializeField] private Color _tileColor = new Color(0.95f, 0.85f, 0.6f);
    [SerializeField] private Color _numberColor = new Color(0.25f, 0.18f, 0.1f);

    // Tiles by number (index 0 is tile 1). Cells are numbered row by row from the top left; a cell holds a tile number, 0 for the gap
    private readonly List<Button> _tiles = new List<Button>();
    private readonly System.Random _random = new System.Random();
    private int[] _cells = new int[0];
    private int _size;
    private GraphRunner _runner;
    private bool _warnedNoRunner;

    private void Start()
    {
        ConnectRunner();
        NewBoard();
    }

    private void OnDestroy()
    {
        if (_runner != null)
        {
            _runner.NodeEntered -= OnNodeEntered;
        }
    }

    private void Update()
    {
        if (_runner == null || !_runner.IsRunning)
        {
            ConnectRunner();
        }

        if (!IsPlaying())
        {
            return;
        }

        // The arrow keys slide a tile next to the gap into it
        var direction = Vector2Int.zero;
        if (GraphInput.GetKeyDown(KeyCode.UpArrow) || GraphInput.GetKeyDown(KeyCode.W))
        {
            direction = Vector2Int.up;
        }
        else if (GraphInput.GetKeyDown(KeyCode.DownArrow) || GraphInput.GetKeyDown(KeyCode.S))
        {
            direction = Vector2Int.down;
        }
        else if (GraphInput.GetKeyDown(KeyCode.LeftArrow) || GraphInput.GetKeyDown(KeyCode.A))
        {
            direction = Vector2Int.left;
        }
        else if (GraphInput.GetKeyDown(KeyCode.RightArrow) || GraphInput.GetKeyDown(KeyCode.D))
        {
            direction = Vector2Int.right;
        }

        if (direction != Vector2Int.zero)
        {
            TrySlideCell(FindCellToSlide(_cells, _size, direction));
        }
    }

    /// <summary>Builds a shuffled board for the current level. Called when the graph enters the Shuffle node.</summary>
    public void NewBoard()
    {
        var level = _runner != null && _runner.HasParameter(_levelParameter, GraphParameterType.Int) ? Mathf.Max(1, _runner.GetInt(_levelParameter)) : 1;
        _size = _firstSize + level - 1;
        _cells = CreateSolved(_size);
        Shuffle(_cells, _size, _shuffleMoves, _random);
        BuildTiles();
        Redraw();
    }

    private void TrySlideCell(int cell)
    {
        if (cell < 0 || !Slide(_cells, _size, cell))
        {
            return;
        }

        if (_runner.HasParameter(_movesParameter, GraphParameterType.Int))
        {
            _runner.SetInt(_movesParameter, _runner.GetInt(_movesParameter) + 1);
        }

        Redraw();
        if (IsSolved(_cells))
        {
            _runner.Raise(_solvedEvent);
        }
    }

    private void OnTileClicked(int number)
    {
        if (IsPlaying())
        {
            TrySlideCell(System.Array.IndexOf(_cells, number));
        }
    }

    /// <summary>The solved board: 1, 2, 3, … row by row, and the gap (0) in the last cell.</summary>
    public static int[] CreateSolved(int size)
    {
        var cells = new int[size * size];
        for (var i = 0; i < cells.Length - 1; i++)
        {
            cells[i] = i + 1;
        }

        return cells;
    }

    /// <summary>Whether the tiles are in order with the gap in the last cell.</summary>
    public static bool IsSolved(int[] cells)
    {
        for (var i = 0; i < cells.Length - 1; i++)
        {
            if (cells[i] != i + 1)
            {
                return false;
            }
        }

        return cells.Length > 0 && cells[cells.Length - 1] == 0;
    }

    /// <summary>Slides the tile in <paramref name="cell"/> into the gap if they are side by side. Returns whether it moved.</summary>
    public static bool Slide(int[] cells, int size, int cell)
    {
        var gap = System.Array.IndexOf(cells, 0);
        if (cell < 0 || cell >= cells.Length || gap < 0 || !AreNeighbours(cell, gap, size))
        {
            return false;
        }

        cells[gap] = cells[cell];
        cells[cell] = 0;
        return true;
    }

    /// <summary>
    /// The cell whose tile would move into the gap when it slides in <paramref name="direction"/> (up is toward the top row),
    /// or -1 if no tile is on that side of the gap.
    /// </summary>
    public static int FindCellToSlide(int[] cells, int size, Vector2Int direction)
    {
        var gap = System.Array.IndexOf(cells, 0);
        if (gap < 0)
        {
            return -1;
        }

        // Row 0 is the top row. Moving a tile up fills the gap from below (the next row), moving one right fills it from the left
        var row = gap / size + direction.y;
        var column = gap % size - direction.x;
        return row >= 0 && row < size && column >= 0 && column < size ? row * size + column : -1;
    }

    /// <summary>
    /// Shuffles by sliding random tiles into the gap <paramref name="moves"/> times (never straight back), so the board can always be solved.
    /// Keeps going if it happens to end solved.
    /// </summary>
    public static void Shuffle(int[] cells, int size, int moves, System.Random random)
    {
        var previous = -1;
        var neighbours = new List<int>(4);
        for (var i = 0; i < moves || IsSolved(cells); i++)
        {
            var gap = System.Array.IndexOf(cells, 0);
            neighbours.Clear();
            for (var cell = 0; cell < cells.Length; cell++)
            {
                if (cell != previous && AreNeighbours(cell, gap, size))
                {
                    neighbours.Add(cell);
                }
            }

            var chosen = neighbours[random.Next(neighbours.Count)];
            Slide(cells, size, chosen);
            previous = gap;
        }
    }

    private static bool AreNeighbours(int a, int b, int size)
    {
        int rowA = a / size, columnA = a % size, rowB = b / size, columnB = b % size;
        return Mathf.Abs(rowA - rowB) + Mathf.Abs(columnA - columnB) == 1;
    }

    private void BuildTiles()
    {
        foreach (var tile in _tiles)
        {
            if (tile != null)
            {
                Destroy(tile.gameObject);
            }
        }

        _tiles.Clear();
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        for (var number = 1; number < _cells.Length; number++)
        {
            var tileObject = new GameObject("Tile " + number, typeof(RectTransform), typeof(Image), typeof(Button));
            tileObject.transform.SetParent(transform, false);
            tileObject.GetComponent<Image>().color = _tileColor;
            var button = tileObject.GetComponent<Button>();
            var tileNumber = number;
            button.onClick.AddListener(() => OnTileClicked(tileNumber));

            var label = new GameObject("Number", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(tileObject.transform, false);
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var text = label.GetComponent<Text>();
            text.font = font;
            text.text = number.ToString();
            text.alignment = TextAnchor.MiddleCenter;
            text.color = _numberColor;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 10;
            text.resizeTextMaxSize = 120;
            text.raycastTarget = false;
            _tiles.Add(button);
        }
    }

    // Puts each tile over its cell (row 0 is the top row)
    private void Redraw()
    {
        for (var cell = 0; cell < _cells.Length; cell++)
        {
            var number = _cells[cell];
            if (number == 0)
            {
                continue;
            }

            var rect = (RectTransform)_tiles[number - 1].transform;
            var row = cell / _size;
            var column = cell % _size;
            rect.anchorMin = new Vector2((float)column / _size, (float)(_size - 1 - row) / _size);
            rect.anchorMax = new Vector2((float)(column + 1) / _size, (float)(_size - row) / _size);
            rect.offsetMin = new Vector2(4f, 4f);
            rect.offsetMax = new Vector2(-4f, -4f);
        }
    }

    private bool IsPlaying() => _runner != null && _runner.Current != null && _runner.Current.Title == _playingNode;

    private void OnNodeEntered(NodeData node)
    {
        if (node.Title == _shuffleNode)
        {
            NewBoard();
        }
    }

    // The Graph Runner lives in the Title scene and stays when this scene loads. Find it, and follow the graph's nodes
    private void ConnectRunner()
    {
        var runner = GraphRunner.FindRunning(_graph);
        if (runner != _runner)
        {
            if (_runner != null)
            {
                _runner.NodeEntered -= OnNodeEntered;
            }

            _runner = runner;
            if (_runner != null)
            {
                _runner.NodeEntered += OnNodeEntered;
            }
        }

        if (_runner == null && !_warnedNoRunner)
        {
            _warnedNoRunner = true;
            Debug.LogWarning($"[{GetType().Name}] No Graph Runner is running this game's graph. Start from the Title scene, " +
                             "or press Play in the graph's Visual Node Editor window.", this);
        }
    }
}
