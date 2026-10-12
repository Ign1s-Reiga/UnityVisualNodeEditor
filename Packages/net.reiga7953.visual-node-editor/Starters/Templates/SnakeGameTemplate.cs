using System.Collections.Generic;
using Reiga.VisualNodeEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Snake, made by the Snake Starter of Visual Node Editor. Change it freely: it is your game's code now.
/// <para>
/// The graph decides the flow (Ready → Playing ⇄ Paused → Game Over → Retry or Title), resets Score when a round starts,
/// shows the panels and the score, and turns keys into events. This script only plays the game:
/// it moves the snake while the graph is on the Playing node, adds to Score when the snake eats,
/// and raises Crash when the snake hits a wall or itself.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class SnakeGameTemplate : MonoBehaviour
{
    [Tooltip("The graph this game plays in. Empty: any running graph.")]
    [SerializeField] private NodeGraphAsset _graph;

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

    private const float DefaultSpeed = 8f;

    // The snake's cells, head first
    private readonly List<Vector2Int> _body = new List<Vector2Int>();
    private readonly List<Image> _segments = new List<Image>();
    private readonly System.Random _random = new System.Random();
    private Image _food;
    private Vector2Int _foodCell;
    private Vector2Int _direction = Vector2Int.right;
    private Vector2Int _nextDirection = Vector2Int.right;
    private float _stepTimer;
    private GraphRunner _runner;
    private bool _warnedNoRunner;

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
        while (_segments.Count < _body.Count)
        {
            _segments.Add(CreateCell("Snake"));
        }

        for (var i = 0; i < _segments.Count; i++)
        {
            var visible = i < _body.Count;
            _segments[i].gameObject.SetActive(visible);
            if (visible)
            {
                _segments[i].color = i == 0 ? _headColor : _bodyColor;
                PlaceInCell(_segments[i].rectTransform, _body[i]);
            }
        }

        if (_food == null)
        {
            _food = CreateCell("Food");
        }

        _food.color = _foodColor;
        _food.gameObject.SetActive(_foodCell.x >= 0);
        if (_foodCell.x >= 0)
        {
            PlaceInCell(_food.rectTransform, _foodCell);
        }
    }

    private Image CreateCell(string cellName)
    {
        var cell = new GameObject(cellName, typeof(RectTransform), typeof(Image));
        cell.transform.SetParent(transform, false);
        var image = cell.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    // Stretches the image over one cell of the board (the board is this object's RectTransform)
    private void PlaceInCell(RectTransform rect, Vector2Int cell)
    {
        rect.anchorMin = new Vector2((float)cell.x / _columns, (float)cell.y / _rows);
        rect.anchorMax = new Vector2((float)(cell.x + 1) / _columns, (float)(cell.y + 1) / _rows);
        rect.offsetMin = new Vector2(1f, 1f);
        rect.offsetMax = new Vector2(-1f, -1f);
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
