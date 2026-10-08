# Visual Node Editor (net.reiga7953.visual-node-editor)

ゲームの大まかな構成をノードで組み立てる Unity 6 向けエディタ拡張。

- `Window > Visual Node Editor` でウィンドウを開く
- `Assets > Create > Visual Node Editor > Node Graph` でグラフアセットを作成し、ダブルクリックで開く

## ゲームから使う（シーン遷移）

1. グラフを `Entry → Scene → Event（イベント名） → Scene …` のように繋ぐ（Event は待機中のノードから出す）
2. ウィンドウのツールバー「Add Scenes to Build」で、グラフのシーンを Build Settings に追加する
3. 最初に読み込まれるシーン（Build Settings の index 0）のルート GameObject に `Graph Runner` コンポーネントを付け、グラフを指定する
4. ゲーム側からイベントを発生させる

```csharp
// コンポーネント経由（UI Button の OnClick に Raise("StartGame") を設定してもよい）
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

## コンテナ（入れ子）

- `Flow/Container` ノードの中にノードを入れられる。ダブルクリックで中を開き、ツールバー下のパンくずで戻る
- 中は Entry から始まり、Exit ノードに着くと、コンテナの同じ出口の出力ポートから外へ進む。出口はコンテナのインスペクタで追加・改名・並べ替えできる
- ゲームのコードからは `container.TryGetExit("Clear", out var exit)` で出口を名前で引ける。実行中に今いるコンテナは `runner.ContainerPath`

詳しくは `docs/01-architecture.md`「コンテナ（サブグラフ）」を参照。
