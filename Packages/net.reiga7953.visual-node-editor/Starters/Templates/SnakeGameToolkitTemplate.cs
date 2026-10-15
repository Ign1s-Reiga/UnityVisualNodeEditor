using System.Collections.Generic;
using Reiga.VisualNodeEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Snake for UI Toolkit, made by the Snake Starter (UI Toolkit) of Visual Node Editor. Change it freely: it is your game's code now.
/// <para>
/// The graph decides the flow (Title → Ready → Playing ⇄ Paused → Game Over → Retry or Title), resets Score when a round starts,
/// and turns keys into events. The screens (UXML) are on the graph's nodes: the Graph Screen Host on this GameObject shows the Game screen
/// (the score and the board) while the graph is in Round, with the Ready and Paused screens over it.
/// This script only plays the game: it draws the snake inside the "board" element of the Game screen,
/// moves it while the graph is on the Playing node, adds to Score when the snake eats, and raises Crash when it hits a wall or itself.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(UIDocument))]
public class SnakeGameToolkitTemplate : MonoBehaviour
{
    [Tooltip("The graph this game plays in. Empty: any running graph.")]
    [SerializeField] private NodeGraphAsset _graph;

    [Tooltip("The name of the element in the screen (UXML) the snake is drawn in.")]
    [SerializeField] private string _boardElement = "board";

    [Header("Names in the graph")]
    [Tooltip("Entering this node starts a new round (the snake goes back to the start).")]
    [SerializeField] private string _readyNode = "Ready";

    [Tooltip("The snake moves only while the graph is on this node.")]
    [SerializeField] private string _playingNode = "Playing";

    [Tooltip("Raised when the snake hits a wall or itself.")]
    [SerializeField] private string _crashEvent = "Crash";

    [Tooltip("Int parameter: food eaten in this round.")]
    [SerializeField] private string _scoreParameter = "Score";

    [Tooltip("Int parameter: the best Score so far.")]
    [SerializeField] private string _highScoreParameter = "HighScore";

    [Tooltip("Float parameter: cells per second.")]
    [SerializeField] private string _speedParameter = "Speed";

    [Header("Board")]
    [SerializeField] [Min(4)] private int _columns = 20;
    [SerializeField] [Min(4)] private int _rows = 20;
    [SerializeField] [Min(1)] private int _startLength = 3;
    [SerializeField] private Color _headColor = new Color(0.6f, 0.95f, 0.45f);
    [SerializeField] private Color _bodyColor = new Color(0.3f, 0.75f, 0.3f);
    [SerializeField] private Color _foodColor = new Color(0.95f, 0.35f, 0.3f);

    [Tooltip("The thin line between cells (the board's background color in the screen's USS).")]
    [SerializeField] private Color _gapColor = new Color(0.07f, 0.08f, 0.1f);

    private const float DefaultSpeed = 8f;

    // The snake's cells, head first
    private readonly List<Vector2Int> _body = new List<Vector2Int>();
    private readonly List<VisualElement> _segments = new List<VisualElement>();
    private readonly System.Random _random = new System.Random();
    private VisualElement _board;
    private VisualElement _food;
    private Vector2Int _foodCell;
    private Vector2Int _direction = Vector2Int.right;
    private Vector2Int _nextDirection = Vector2Int.right;
    private float _stepTimer;
    private GraphRunner _runner;
    private bool _warnedNoRunner;
    private bool _warnedNoBoard;

    private void Start()
    {
        ConnectRunner();
        ResetBoard();
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

        if (_board == null)
        {
            FindBoard();
        }

        if (!IsPlaying())
        {
            return;
        }

        ReadInput();
        _stepTimer += Time.deltaTime;
        var interval = 1f / Mathf.Max(0.5f, GetSpeed());
        while (_stepTimer >= interval && IsPlaying())
        {
            _stepTimer -= interval;
            Step();
        }
    }

    /// <summary>Puts the snake back at the start and places new food. Called when the graph enters the Ready node.</summary>
    public void ResetBoard()
    {
        _body.Clear();
        var start = new Vector2Int(Mathf.Max(_startLength - 1, _columns / 4), _rows / 2);
        for (var i = 0; i < _startLength; i++)
        {
            _body.Add(start - new Vector2Int(i, 0));
        }

        _direction = Vector2Int.right;
        _nextDirection = Vector2Int.right;
        _stepTimer = 0f;
        _foodCell = PlaceFood(_body, _columns, _rows, _random);
        Redraw();
    }

    // Moves one cell: eats, grows, or crashes
    private void Step()
    {
        _direction = _nextDirection;
        var head = _body[0] + _direction;
        var eats = head == _foodCell;
        if (!IsInside(head, _columns, _rows) || HitsBody(_body, head, eats))
        {
            Crash();
            return;
        }

        _body.Insert(0, head);
        if (eats)
        {
            AddToScore(1);
            _foodCell = PlaceFood(_body, _columns, _rows, _random);
        }
        else
        {
            _body.RemoveAt(_body.Count - 1);
        }

        Redraw();
    }

    private void Crash()
    {
        var score = GetInt(_scoreParameter);
        if (_runner.HasParameter(_highScoreParameter, GraphParameterType.Int) && score > _runner.GetInt(_highScoreParameter))
        {
            _runner.SetInt(_highScoreParameter, score);
        }

        _runner.Raise(_crashEvent);
    }

    private void ReadInput()
    {
        var wanted = _nextDirection;
        if (GraphInput.GetKeyDown(KeyCode.UpArrow) || GraphInput.GetKeyDown(KeyCode.W))
        {
            wanted = Vector2Int.up;
        }
        else if (GraphInput.GetKeyDown(KeyCode.DownArrow) || GraphInput.GetKeyDown(KeyCode.S))
        {
            wanted = Vector2Int.down;
        }
        else if (GraphInput.GetKeyDown(KeyCode.LeftArrow) || GraphInput.GetKeyDown(KeyCode.A))
        {
            wanted = Vector2Int.left;
        }
        else if (GraphInput.GetKeyDown(KeyCode.RightArrow) || GraphInput.GetKeyDown(KeyCode.D))
        {
            wanted = Vector2Int.right;
        }

        // The snake cannot turn back into itself
        if (!IsReverse(wanted, _direction))
        {
            _nextDirection = wanted;
        }
    }

    /// <summary>Whether <paramref name="cell"/> is on the board.</summary>
    public static bool IsInside(Vector2Int cell, int columns, int rows) => cell.x >= 0 && cell.y >= 0 && cell.x < columns && cell.y < rows;

    /// <summary>Whether <paramref name="direction"/> goes straight back the way the snake is moving.</summary>
    public static bool IsReverse(Vector2Int direction, Vector2Int current) => direction + current == Vector2Int.zero;

    /// <summary>
    /// Whether the head moving into <paramref name="cell"/> hits the body. The tail moves away at the same time,
    /// so its cell is free unless the snake is growing (<paramref name="growing"/>).
    /// </summary>
    public static bool HitsBody(IList<Vector2Int> body, Vector2Int cell, bool growing)
    {
        var count = growing ? body.Count : body.Count - 1;
        for (var i = 0; i < count; i++)
        {
            if (body[i] == cell)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>A random free cell for the food. (-1, -1) when the snake fills the board.</summary>
    public static Vector2Int PlaceFood(IList<Vector2Int> body, int columns, int rows, System.Random random)
    {
        var free = new List<Vector2Int>();
        for (var y = 0; y < rows; y++)
        {
            for (var x = 0; x < columns; x++)
            {
                var cell = new Vector2Int(x, y);
                if (!body.Contains(cell))
                {
                    free.Add(cell);
                }
            }
        }

        return free.Count == 0 ? new Vector2Int(-1, -1) : free[random.Next(free.Count)];
    }

    private void Redraw()
    {
        if (_board == null)
        {
            return;
        }

        while (_segments.Count < _body.Count)
        {
            _segments.Add(CreateCell());
        }

        for (var i = 0; i < _segments.Count; i++)
        {
            var visible = i < _body.Count;
            _segments[i].style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (visible)
            {
                _segments[i].style.backgroundColor = i == 0 ? _headColor : _bodyColor;
                PlaceInCell(_segments[i], _body[i]);
            }
        }

        if (_food == null)
        {
            _food = CreateCell();
        }

        _food.style.backgroundColor = _foodColor;
        _food.style.display = _foodCell.x >= 0 ? DisplayStyle.Flex : DisplayStyle.None;
        if (_foodCell.x >= 0)
        {
            PlaceInCell(_food, _foodCell);
        }
    }

    // A cell drawn over the board, with a thin border in the board's color so neighbouring cells stay apart
    private VisualElement CreateCell()
    {
        var cell = new VisualElement { pickingMode = PickingMode.Ignore };
        cell.style.position = Position.Absolute;
        var gap = _gapColor;
        cell.style.borderLeftWidth = 1f;
        cell.style.borderTopWidth = 1f;
        cell.style.borderRightWidth = 1f;
        cell.style.borderBottomWidth = 1f;
        cell.style.borderLeftColor = gap;
        cell.style.borderTopColor = gap;
        cell.style.borderRightColor = gap;
        cell.style.borderBottomColor = gap;
        _board.Add(cell);
        return cell;
    }

    // Puts the element over one cell of the board, in percent of the board (row 0 is at the bottom, as in the game's coordinates)
    private void PlaceInCell(VisualElement element, Vector2Int cell)
    {
        element.style.left = Length.Percent(100f * cell.x / _columns);
        element.style.top = Length.Percent(100f * (_rows - 1 - cell.y) / _rows);
        element.style.width = Length.Percent(100f / _columns);
        element.style.height = Length.Percent(100f / _rows);
    }

    private bool IsPlaying() => _runner != null && _runner.Current != null && _runner.Current.Title == _playingNode;

    private float GetSpeed() =>
        _runner != null && _runner.HasParameter(_speedParameter, GraphParameterType.Float) ? _runner.GetFloat(_speedParameter) : DefaultSpeed;

    private int GetInt(string parameter) =>
        _runner != null && _runner.HasParameter(parameter, GraphParameterType.Int) ? _runner.GetInt(parameter) : 0;

    private void AddToScore(int amount)
    {
        if (_runner.HasParameter(_scoreParameter, GraphParameterType.Int))
        {
            _runner.SetInt(_scoreParameter, _runner.GetInt(_scoreParameter) + amount);
        }
    }

    private void OnNodeEntered(NodeData node)
    {
        if (node.Title == _readyNode)
        {
            ResetBoard();
        }
    }

    // The board is in the Game screen, which the Graph Screen Host adds to the UI Document only when the graph gets to Round.
    // Once found, it is the same element every time the screen comes back
    private void FindBoard()
    {
        _board = GetComponent<UIDocument>().rootVisualElement?.Q(_boardElement);
        if (_board != null)
        {
            Redraw();
        }
        else if (IsPlaying() && !_warnedNoBoard)
        {
            _warnedNoBoard = true;
            Debug.LogWarning($"[{GetType().Name}] No screen on the UI Document has an element named '{_boardElement}' to draw the snake in.", this);
        }
    }

    // The Graph Runner is on this GameObject (or anywhere else). Find it, and follow the graph's nodes
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
            Debug.LogWarning($"[{GetType().Name}] No Graph Runner is running this game's graph. Start from the game's scene, " +
                             "or press Play in the graph's Visual Node Editor window.", this);
        }
    }
}
