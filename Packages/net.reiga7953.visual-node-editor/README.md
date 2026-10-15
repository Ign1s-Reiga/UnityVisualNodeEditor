# Visual Node Editor (net.reiga7953.visual-node-editor)

ゲームの大まかな構成をノードで組み立てる Unity 6 向けエディタ拡張。

- `Window > Visual Node Editor` でウィンドウを開く
- `Assets > Create > Visual Node Editor > Node Graph` でグラフアセットを作成し、ダブルクリックで開く

## ゲームから使う（シーン遷移）

1. グラフを `Entry → Scene → Event（イベント名） → Scene …` のように繋ぐ（Event は待機中のノードから出す）。
   シーンは Project ビューからキャンバスへドロップすると Scene ノードになる
2. ボタンからイベントを送る: UI Button に `Add Component > Visual Node Editor > Graph Event Button` を付け、グラフとイベント名（グラフにある名前から選ぶ）を指定する。
   別のシーンのボタンからでも届く（Graph Runner への参照もコードも要らない）
3. ウィンドウのツールバー「Play」を押す。グラフのシーンを Build Settings に入れ、グラフが始まるシーンを開き、そこに Graph Runner が無ければ
   （確認してから）追加して保存し、Play に入る

手で用意する場合は、ツールバー「Add Scenes to Build」でシーンを Build Settings に入れ、最初に読み込まれるシーン（Build Settings の index 0）の
ルート GameObject に `Graph Runner` コンポーネントを付けてグラフを指定する。

コードから送る場合:

```csharp
// コンポーネント経由
FindFirstObjectByType<GraphRunnerBehaviour>().Raise("StartGame");

// 実行器を直接使う
var runner = new GraphRunner(graph, new SceneManagerSceneLoader());
runner.On("OnBossDefeated", () => Debug.Log("boss defeated"));
runner.Start();
runner.Raise("StartGame");
```

- Scene・State ノードで止まり、Entry・Event ノードは通過する。`Advance()` はイベントを介さずに次のノードへ進む
- Play 中は、グラフウィンドウで実行中のノードが緑色に強調される
- Blackboard（View メニュー）で定義したパラメータは、実行中に `runner.GetInt("Lives")` / `runner.SetInt("Lives", 2)` のように読み書きできる（値は Runner ごと、`Start()` のたびに既定値へ戻る）

詳しい規則はリポジトリの `docs/01-architecture.md`「ランタイム実行（GraphRunner）」を参照。

## ノードの振る舞い（Update / FixedUpdate）

State・Scene ノードにいる間の処理は、C# の `NodeBehaviour` に書いてノードに付ける（Animator の StateMachineBehaviour と同じ考え方）。

1. ノードを選び、インスペクタの「Create Script…」で骨組みを作る（コンパイル後にそのノードへ自動で追加される）。既存のクラスは「Add Behaviour」から付ける
2. 必要なメソッドだけ書く

```csharp
[System.Serializable]
public class PlayerMovement : NodeBehaviour
{
    [SerializeField] private float _speed = 5f; // ノードのインスペクタで編集できる

    public override void OnEnter() { }
    public override void OnUpdate(float deltaTime) { /* 毎フレーム */ }
    public override void OnFixedUpdate(float fixedDeltaTime) { /* 物理の更新ごと */ }
    public override void OnExit() { }
}
```

- `Graph Runner` コンポーネントが毎フレーム `Update` / `FixedUpdate` を呼ぶ（`GraphRunner` を直接使うなら `runner.Update(Time.deltaTime)` を自分で呼ぶ）
- 振る舞いからは `Runner`（`Runner.Raise("Win")` など）、`Node`、`Host`（Graph Runner コンポーネント）を使える
- グラフはアセットなので、シーンのオブジェクトはフィールドで参照できない。`Host` や `FindFirstObjectByType` から実行時に引く
- 値は `Start()` のたびにアセットから複製されるので、実行中に書き換えてもアセットには残らない
- 組み込みの振る舞い（Add Behaviour > Visual Node Editor）: `Set Parameter`（ノードに入ったら `Score = 0`・`Level += 1` などを行う）、
  `Time Scale`（ノードにいる間 `Time.timeScale` を変える。ポーズ用）

## 画面と入力をグラフに繋ぐ（コード無し）

`Add Component > Visual Node Editor` の次のコンポーネントは、Graph Runner への参照なしで、実行中のグラフを探して繋ぐ。

- `Graph Node Activator`: グラフが指定したノード（コンテナなら、その中のどこか）にいる間だけ、指定した GameObject を表示する（ポーズ・ゲームオーバーのパネル）
- `Graph Parameter Text`: パラメータの値を、同じ GameObject のテキスト（uGUI の Text・TextMeshPro）に `Score: {0}` のような書式で出す
- `Graph Event Key`: キーを押したら Raise / Advance する（Esc → Pause）。キーは `GraphInput.GetKeyDown(KeyCode)` で読み、旧 Input Manager でも Input System でも動く
- `Graph Event Button`: ボタンのクリックで Raise / Advance / Save / Load する

## ゲームの Starter

`Assets > Create > Visual Node Editor > Snake Starter` / `Sliding Puzzle Starter` で、グラフ・Title と Game のシーン・ゲームのスクリプトを作る。
スクリプトはコンパイル後に Game のシーンの盤へ自動で付き、ツールバーの Play ですぐ遊べる（uGUI が必要）。書き出したスクリプトは自由に書き換えてよい。
`Snake Starter (UI Toolkit)` / `Sliding Puzzle Starter (UI Toolkit)` は、1 つのシーンで、画面を UI Toolkit（UXML / USS。UI Builder で直せる）にしてノードに付ける。

## UI Toolkit の画面（UI Builder）

画面はシーンではなく、State・Scene・Container ノードに付ける（インスペクタの Screen。Create Screen… で作って UI Builder で開き、Edit Screen で開く）。
Graph Runner に付けた `Graph Screen Host`（UIDocument と一緒に）が、入っているコンテナの画面の上に、今のノードの画面を重ねて出す。
ツールバーの Play で Graph Runner を置くとき、ノードに画面があれば一緒に付く。

UI Builder の Library に、グラフに繋がるコントロールが出る（コード無し）。`graph` 属性を入れると、イベント名などをグラフから選べる（空なら実行中のグラフ）。

- `GraphButton`: クリックで Raise / Advance / Save / Load
- `GraphParameterLabel`: パラメータを `Score: {0}` のような書式で出す
- `GraphNodePanel`: グラフが指定したノード（`node="Round/Paused"` のようにタイトルを `/` で並べる。コンテナなら中のどこか）にいる間だけ、中の要素を出す（画面の一部だけを出し分けるとき）

## コンテナ（入れ子）

- `Flow/Container` ノードの中にノードを入れられる。コンテナの中にコンテナも置ける（深さに制限は無い）。ダブルクリックで中を開き、ツールバー下のパンくずで戻る
- 中は Entry から始まり、Exit ノードに着くと、コンテナの同じ出口の出力ポートから外へ進む。出口はコンテナのインスペクタで追加・改名・並べ替えできる
- ゲームのコードからは `container.TryGetExit("Clear", out var exit)` で出口を名前で引ける。実行中に今いるコンテナは `runner.ContainerPath`

詳しくは `docs/01-architecture.md`「コンテナ（サブグラフ）」を参照。
