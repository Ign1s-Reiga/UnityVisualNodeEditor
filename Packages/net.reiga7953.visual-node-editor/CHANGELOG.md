# Changelog

## [0.5.1] - 2026-10-07
- 修正: `NodeExited` の通知の中（`Stop()` の途中）で `Raise` / `Advance` すると積まれたまま残り、次の `Start()` の後に勝手に実行されていた。停止状態にしてから通知するようにし、その呼び出しは false を返す
- 修正: Event → Event と連鎖した先が出力の無い Event だと、その Event に入ったまま動けなくなっていた。連鎖全体を通知として扱い、直前の待機ノードに留まる

## [0.5.0] - 2026-10-07
- ランタイム実行: `GraphQuery`（遷移先・シーン一覧などの読み取り）と `GraphRunner`（Scene・State で待機、Entry・Event は通過、`Raise` / `Advance` で遷移、`SceneManager` でシーンを読み込む）
- `Graph Runner` コンポーネント（`GraphRunnerBehaviour`）: グラフを指定して開始。シーンを切り替えても残り、重複しない
- イベントのフック: C# の `EventTriggered` / `On` / `Off` と、インスペクタの UnityEvent（On Node Entered、イベント名ごとのバインド）
- Build Settings 連携: ツールバー「Add Scenes to Build」/ `Assets > Visual Node Editor > Add Graph Scenes to Build Settings`。Build Settings に無いシーンを警告
- Play Mode 中、実行中のノードをグラフウィンドウで強調表示
- 検証: イベント名が空の Event ノードを警告

## [0.4.1] - 2026-10-07
- 修正: クリップボードの識別子が改行で終わっていたため、OS のクリップボードで改行コードが変わると貼り付けが何も起きなかった可能性がある。識別子から改行を除いた
- 修正: 付箋をグループへドラッグで入れられたが保存されず、再読み込み後に外へ出ていた。グループが付箋を受け付けないようにした
- 修正: シーンの改名・移動時に、エディタで編集中（未保存）のグラフまで勝手に保存していた。未保存の変更があるグラフは保存せず未保存状態にするだけにした

## [0.4.0] - 2026-10-07
- 付箋（StickyNote）: 右クリック → Create Sticky Note。見出し・本文・配色・文字サイズ・位置と大きさを `StickyNoteData` として保存（Undo 対応）。Note ノードとは併存
- コピー・切り取り・貼り付け・複製（Ctrl+C / X / V / D、右クリックメニュー）。ノード・グループ（所属ノードごと）・付箋と、コピーしたノード間のエッジが対象。新しい ID で、元から少しずらして貼り付け、貼り付けた要素を選択。1 回の Undo で戻せる
- ミニマップ（ツールバーのトグル、表示状態はドメインリロード後も保持）
- 表示ショートカット `F`（選択範囲、無選択なら全体）/ `A`（全体）と、ツールバーの Frame Selection / Frame All ボタン

## [0.3.1] - 2026-10-07
- 修正: グラフアセットを選択したまま別のものをダブルクリックすると（Console のログ、シーンのフィールドなど）、Visual Node Editor が開いてしまい本来の対象が開かなかった。開こうとしているアセット自体で判定するようにした
- 修正: Unity 6000.5 でダブルクリックからグラフを開けなかった可能性がある（6000.5 の `OnOpenAsset` は `EntityId` 版のみ）。6000.5 以降は `EntityId` 版を使う
- 修正: シーンを改名・移動しても、そのノードをインスペクタで開くまで古いシーン名がランタイムに残っていた。シーンの移動・再インポート時とグラフを開いたときに、全参照の Path を GUID から引き直す（`SceneReferenceSync`）
- CI: 6000.3 / 6000.4 を追加し、各バージョンの `UNITY_6000_N_OR_NEWER` を定義してコンパイルチェック

## [0.3.0] - 2026-10-07
- エディタ UI の改善
  - レイアウト: グラフとインスペクタを TwoPaneSplitView に（境界をドラッグで変更、幅をドメインリロード後も保持、最小幅あり）
  - インスペクタ: カテゴリ色のアイコン・型名・タイトル入力欄（空なら `(Title)`）のヘッダー。幅 220px でもはみ出さない。ラベルを英語に統一
  - ノード表示: `[NodeMenu]` の先頭セグメントをカテゴリとして色帯を表示、タイトル直下にサマリー 1 行（`NodeView.GetSummary()`）、ポートが各 1 つ以下ならラベルを隠す
  - 問題表示: ツールバーのトグルで一覧を開閉（既定は閉、新しいエラーで自動で開く、問題が無ければ場所を取らない）
  - ツールバー: アセット名と未保存マーク ` *`、ツールチップにパス、クリックで Ping
  - グリッドが表示されない問題を修正（スタイルを GraphView 自身に付ける）
- 変更: State / Event ノードのメニューパスを `State/State`・`Event/Event` に変更（Create Node メニューの階層が変わる）
- 追加: Note ノード用の `NoteNodeView`

## [0.2.0] - 2026-10-06
- M2: 構成ツールとして使える
  - ノード選択時のインスペクタ（`NodeInspectorView`、`PropertyField` で自動生成・Undo 対応）
  - `GraphValidator`（Entry の欠落・重複、存在しないノードへのエッジ、ID 重複、Note への接続、重複エッジ、シーン未設定）と、ノード枠の色分け・問題一覧表示
  - グループ（右クリック → Create Group。`GroupData` として保存）
  - `SceneReference` と SceneAsset 選択 UI（GUID で移動・改名に追従、Build Settings 未登録を警告）
  - **破壊的変更**: `SceneNode.SceneName` の setter を削除し、`SceneNode.Scene`（`SceneReference`）に置き換え。既存の `_sceneName` の値は引き継がれない
- 修正: 全ファイルの `.meta` を追加（Git URL で取り込むと、`.meta` の無いファイルは Unity に無視され何も表示されなかった）
- 修正: Unity 6000.5 でコンパイルエラーになる `EditorUtility.InstanceIDToObject` の使用をやめた
- CI: 6000.0（最小サポート）と 6000.5 の両方でコンパイルチェック

## [0.1.0] - 2026-10-02
- 初期スキャフォールド（NodeGraphAsset / NodeData / EdgeData / GraphView ウィンドウ骨組み）
- M1: 最小限の編集
  - `NodeViewFactory` と `[CustomNodeView]` による型 → View の自動登録
  - `NodeSearchWindow`（右クリック → Create Node / Space キーで `[NodeMenu]` から追加）
  - Entry / Scene / State / Event のポート定義、エッジの接続・切断をアセットへ反映
  - ノード移動の保存、Undo/Redo 対応、ツールバーの Save ボタン
  - `NodeGraphAsset.FindEdge`
