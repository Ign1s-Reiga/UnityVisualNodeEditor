# Changelog

## [0.10.1] - 2026-10-09
- 修正: Graph Runner をプレハブのインスタンスの中に置いていると、「Build Settings のシーンに Graph Runner が無い」という誤った警告が出ていた。プレハブのインスタンスでグラフを指定しているか、元のプレハブがグラフを参照しているシーンは使われているとみなす
- 修正: ツールバーの Play が、Build Settings の先頭が無効なシーンのとき、不要な「先頭へ移すか」の確認を出していた。ビルドと同じく無効なシーンは飛ばして判定する
- 修正: Graph Event Button を付けたボタンの OnClick でも `Send()` を呼んでいると、1 回のクリックで 2 回送っていた（Advance ではノードを 1 つ飛ばす）。OnClick で既に（Off でない状態で）呼んでいれば自動では繋がず、インスペクタでも知らせる

## [0.10.0] - 2026-10-09
UX 改善のフェーズ 1: 初めての人が、シーンを 3 つ繋いだ流れをコードを書かずにボタンで動かせるようにする（調査は `docs/04-ux-audit.md`）。
- 新しいグラフは最初から Entry がある。空のキャンバス・空のコンテナには次の一手の案内（Add Entry / Add First Scene / Add Scene / Add State）を出す
- Project ビューのシーンアセットをキャンバスへドロップすると Scene ノードを作る。タイトルを付けていない Scene ノードはシーン名をタイトルに出す
- `Graph Event Button` コンポーネント: UI Button に付けると、クリックでそのグラフを実行中の Runner にイベントを送る（別のシーンからでも届く。参照もコードも不要）。
  イベント名はグラフの Event ノードの名前から選ぶ
- ツールバーの「Play」: シーンを Build Settings に入れ、グラフが始まるシーンを開き、Graph Runner が無ければ（確認して）追加・保存して Play に入る
- 検証: Scene ノードがあるのに、Build Settings のシーンにこのグラフの Graph Runner が無ければ警告する
- 変更: Node Graph の作成メニューは Runtime の `[CreateAssetMenu]` から Editor のメニューに移った（場所は同じ `Assets > Create > Visual Node Editor > Node Graph`）

## [0.9.1] - 2026-10-08
- 修正: 振る舞いの `OnUpdate` の中で同じノードへ戻る遷移をすると、入り直したばかりの残りの振る舞いの `OnUpdate` が同じフレームで呼ばれていた。どんな遷移でも、そのフレームの残りは呼ばないようにした
- 修正: Create Script… で、`NodeBehaviour` 以外の既存のクラス（名前空間の無い `Player` など）と同じ名前を付けるとスクリプトが書き出され、コンパイルエラーになっていた。読み込まれているすべての型と名前を比べて、重なれば作らないようにした

## [0.9.0] - 2026-10-08
- ノードの振る舞い（`NodeBehaviour`）: State・Scene ノードに C# のクラスを付け、Runner がそのノードにいる間の処理を書ける（Animator の StateMachineBehaviour に相当）。`OnEnter` / `OnUpdate(deltaTime)` / `OnFixedUpdate(fixedDeltaTime)` / `OnExit` を必要なものだけ上書きする。`Runner`・`Node`・`Host`（Graph Runner コンポーネント）を参照できる
- ランタイム: `GraphRunner.Update` / `FixedUpdate` で現在のノードの振る舞いを動かす（Graph Runner コンポーネントが毎フレーム呼ぶ）。振る舞いは Runner ごと・`Start()` ごとにアセットの値から複製する。振る舞いの例外はログに出して次へ進む
- インスペクタ: 振る舞いの一覧（フィールドの編集・並べ替え・削除・スクリプトを開く）、Add Behaviour（public な `NodeBehaviour` のサブクラスから選ぶ）、Create Script…（骨組みを書き出して開き、コンパイル後にノードへ自動で追加）。ノードには付いている振る舞いの名前を出す
- 検証: 型の改名・削除で読めなくなった振る舞いを警告する（実行時は飛ばす）

## [0.8.0] - 2026-10-08
- コンテナを作ると、中の Entry を最初の出口の Exit ノードに繋いだ状態で作る（作ったばかりのコンテナは、そのまま最初の出口へ通り抜ける）。既定の出口が無いコンテナでは繋がない

## [0.7.0] - 2026-10-08
- コンテナ（サブグラフ）: `Flow/Container` ノードの中にノードを入れて入れ子にできる。出口ごとに出力ポートを持ち、中の Exit ノード（`Container/Exit`）に着くと、同じ出口のポートから外へ進む。作成時に中の Entry と、出口ごとの Exit ノードを自動で作る
- 階層の切り替え: コンテナをダブルクリック（または右クリック → Open Container）で中を開き、ツールバー下のパンくずか右クリック → Open Parent Level で戻る。1 階層ずつ表示し、階層をまたぐエッジは作れない。実行中のノードの強調と検証の枠は、それを含むコンテナにも出る
- 出口の編集: コンテナのインスペクタで出口を追加・改名・ドラッグで並べ替え・削除（エッジや Exit ノードに影響があれば確認する、Undo 対応）。Exit ノードのインスペクタでは出口をドロップダウンで選び、「+ New exit…」で追加できる
- コピー・貼り付け・削除はコンテナの中身ごと行う。コンテナの Entry は単独では削除・コピーできない
- ランタイム: `GraphRunner` がコンテナに入り、Exit から出る（入れ子の深さに制限なし）。今いるコンテナは `ContainerPath`、遷移先は `GraphQuery.GetNextNode` / `GetContainerEntry` / `GetExitTarget` で引ける。Entry の無いコンテナや繋がっていない出口では、直前の待機ノードに留まって警告する
- 検証: コンテナの条件（階層をまたぐエッジ、Entry の数、Entry / Exit を置ける場所、親の参照、出口の名前と ID、Exit ノード・ポートが指す出口）と、パラメータ ID の重複を Error にする
- グラフアセットのインスペクタ: 生のリスト（`+` で内部 ID ごと複製できてしまう）の代わりに、「Open in Visual Node Editor」ボタン・問題の数・要素の数・パラメータの一覧を表示する

## [0.6.0] - 2026-10-08
- ノードの折りたたみ: タイトルの ▼ の状態を保存（Undo 対応）。折りたたむとサマリー行も隠す。右クリック → Collapse All / Expand All
- ノードの検索: ツールバーの検索欄でタイトル・型名から検索（大文字小文字を区別しない）。一致を強調し、それ以外を薄く表示。Enter で次の一致へ、Esc で解除
- Blackboard: グラフのパラメータ（Bool / Int / Float / String）を追加・改名・削除・並べ替え・既定値の編集（Undo 対応）。`GraphRunner.GetInt` / `SetInt` などで実行時に読み書き（Start のたびに既定値へ戻る、`ParameterChanged` で通知）。空・重複する名前は検証で Error
- グリッドへの吸着（View メニューの Snap to Grid、動かし終えたときに 20px のグリッドへ）と、選択したノードの整列（Align）・等間隔配置（Distribute）
- 変更: ツールバーの MiniMap ボタンを View メニュー（MiniMap / Blackboard / Snap to Grid）にまとめた

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
