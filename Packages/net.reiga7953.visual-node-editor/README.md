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

詳しい規則はリポジトリの `docs/01-architecture.md`「ランタイム実行（GraphRunner）」を参照。
