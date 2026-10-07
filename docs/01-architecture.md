# 01 - Architecture

## レイヤ

```
┌──────────────────────────────┐
│ Editor (Reiga.VisualNodeEditor.Editor)         │
│  NodeGraphEditorWindow → NodeGraphView → NodeView │
│  NodeSearchWindow / Inspector / UXML・USS          │
└───────────────┬──────────────┘
                │ 参照（逆方向は禁止）
┌───────────────▼──────────────┐
│ Runtime (Reiga.VisualNodeEditor)              │
│  NodeGraphAsset (ScriptableObject)             │
│  NodeData (abstract) / 各ノード型 / EdgeData  │
│  NodeMenuAttribute                             │
│  GraphQuery / GraphRunner / GraphRunnerBehaviour │
└──────────────────────────────┘
```

## データモデル

- `NodeGraphAsset` : `List<NodeData>` を `[SerializeReference]` で、`List<EdgeData>` / `List<GroupData>` / `List<StickyNoteData>` / `List<GraphParameter>` を `[SerializeField]` で保持。
- `NodeData` : `Id`(GUID 文字列), `Title`, `Position`, `Collapsed` を持つ抽象基底。ポート定義はサブクラスごとに Editor 側の `NodeView` が決める。
  `Id` / `Position` / `Collapsed` は `[HideInInspector]`（インスペクタには出さない。`Collapsed` は Position と同じくエディタ専用の表示状態）。
- `GraphParameter` : グラフ単位のパラメータ（Blackboard）。`Id`, `Name`（グラフ内で一意）, `Type`（Bool / Int / Float / String）と型ごとの既定値。
  ノードの処理を書くためのものではなく（非目的）、ゲームから参照・更新する設定値・状態の置き場
- `EdgeData` : `(FromNodeId, FromPort, ToNodeId, ToPort)` の 4 つ組。ポートは文字列名で識別する。
- `GroupData` : `Id`, `Title`, `Position` と、所属ノードの ID リスト。ノードは高々 1 つのグループに属する。ノード削除時は全グループから ID を外す。
- `StickyNoteData` : 付箋（グラフ上のコメント）。`Id`, `Title`, `Contents`, `Rect`（位置とサイズ）, `Theme`, `FontSize`。
  ポートを持たずエッジも繋がらない。グループには入れられない（`GroupView` が付箋を受け付けない）。
  `Theme` / `FontSize` は GraphView の enum に依存しないよう Runtime 側に同じ値の enum（`StickyNoteTheme` / `StickyNoteFontSize`）を持つ。
  ポート無しの Note ノードとは役割を分ける: Note は構成要素としてのメモ（検索・検証の対象）、付箋は自由に置けるレイアウト上の注釈
- `SceneReference` : シーンの `Guid` と `Path`（`Name` は Path から導出）。Runtime は `SceneAsset` 型に触れず文字列だけを持つ。
  `SceneAsset` との相互変換は Editor の `SceneReferenceDrawer` が行う。
  ランタイムは Path（と Name）でシーンを読むため、Path は常に GUID から引き直して最新に保つ（`SceneReferenceSync`）:
  - シーンの移動・改名・再インポート時: `SceneReferencePostprocessor` が全グラフを更新する。未保存の変更が無かったグラフはそのまま保存し、
    未保存の変更があるグラフ（エディタで編集中など）は保存せず未保存状態にする（ユーザーの編集を勝手に書き込まないため）
  - グラフをエディタで開いたとき: 取りこぼし（Unity を閉じている間の変更など）を更新し、未保存状態にする
  - ドロワーの描画時: 表示中の参照を更新する
  - GUID から Path が引けない（シーンが削除された）参照は変更しない

Position などエディタ専用の情報も Runtime の型に持たせる（UnityEditor API は使わないため問題ない）。
ビルドサイズが気になる段階になったら `#if UNITY_EDITOR` で strip するのではなく、別の EditorData に分離する。

## ノード種別の追加手順

1. `Runtime/Nodes/XxxNode.cs` に `NodeData` のサブクラスを作り、必ず `[Serializable]` を付ける（属性は継承されないため、無いと `[SerializeReference]` で保存されない）
2. `[NodeMenu("Category/Xxx")]` を付ける
3. `Editor/Views/XxxNodeView.cs` に `NodeView` のサブクラスを作り、`[CustomNodeView(typeof(XxxNode))]` を付ける。
   `CreatePorts()` をオーバーライドして `AddInputPort("in")` / `AddOutputPort("out")` でポートを定義する
4. `NodeViewFactory` が `TypeCache` で `[CustomNodeView]` を収集し、型→View を自動登録する（手動登録は不要）。
   対応する View が無いノード型は、最も近い基底型の View、最終的には素の `NodeView`（ポート無し）で表示される
5. インスペクタは `[SerializeField]` フィールドから自動生成される（`PropertyField`）。独自 UI が要る型は `PropertyDrawer` を書く
6. EditMode テストを追加する

## ポート

- ポートは `NodeView` サブクラスが定義し、`Port.portName` がそのまま `EdgeData` のポート名になる（型システムは持たない。未決事項参照）
- 接続可否は `NodeGraphView.GetCompatiblePorts` で判定する: 向きが逆・別ノード・同じポート対が未接続であること
- 既定のポート構成: Entry = `out` のみ / Scene・State・Event = `in` + `out` / Note = ポート無し

## エディタ ↔ アセットの同期

- 開く: `NodeGraphView.Populate(asset)` が Nodes/Edges/Groups から View を生成。再構築中はアセットへ書き戻さない
- 追加: 検索ウィンドウ（`NodeSearchWindow`、`[NodeMenu]` のパスで階層化）で選んだ型を生成し、アセットと View の両方に追加
- 編集: `graphViewChanged` コールバックで以下を即座にアセットへ反映し、`EditorUtility.SetDirty`
  - `edgesToCreate` → `EdgeData` を追加
  - `elementsToRemove` → `NodeView` / `Edge` / `GroupView` / `StickyNoteView` に対応するデータを削除
  - `movedElements` → `NodeData.Position` / `GroupData.Position` / `StickyNoteData.Rect` の位置を更新
  - データの特定は常に ID（エッジはポート対の 4 つ組）で行い、View が保持するインスタンスの同一性には頼らない
    （`SerializedObject` 経由の編集後にインスタンスが差し替わっても壊れないようにするため）
- グループ: 右クリック → Create Group で選択中のノードを囲む。`elementsAddedToGroup` / `elementsRemovedFromGroup` / `groupTitleChanged` で同期
- 付箋: 右クリック → Create Sticky Note。タイトル・本文・テーマ・文字サイズの変更は `StickyNoteChangeEvent`、リサイズは `OnResized` で同期
- 保存: 通常の `AssetDatabase.SaveAssets`（ツールバーの Save ボタンは `AssetDatabase.SaveAssetIfDirty` で明示保存）
- Undo: `Undo.RecordObject(asset, ...)` を各変更の前に呼ぶ。`Undo.undoRedoPerformed` でビューを `Populate` し直す

## コピー・貼り付け・複製

GraphView 標準のショートカット（Ctrl+C / Ctrl+X / Ctrl+V / Ctrl+D、右クリックメニューの Copy / Cut / Paste / Duplicate）を、
`serializeGraphElements` / `canPasteSerializedData` / `unserializeAndPaste` に処理を渡して有効にする。

- クリップボードの中身は `GraphClipboard`（Editor）が作る JSON。ノードは `[SerializeReference]` のまま `JsonUtility` で多態シリアライズする
  - 先頭に識別子を持たせ、他ツールの文字列やこのツール以外の JSON は貼り付け不可と判定する。
    識別子に改行を含めない（OS のクリップボードで改行コードが変わっても判定がずれないように）
- コピー対象: 選択中のノード・グループ・付箋。グループを選ぶと中のノードも含める。エッジは両端のノードが対象に含まれるものだけ
- 貼り付け: すべての要素に新しい ID を振り、エッジ・グループの参照を新しい ID に付け替える。位置は元から少しずらす（同じ内容を続けて貼ると、さらにずらす）
- 貼り付けた要素を選択状態にする。1 回の貼り付け・複製・切り取りは 1 回の Undo で戻せる
- Entry を複製すると Entry が 2 つになるが、禁止はせず検証（Validation）のエラーで知らせる
- JSON の組み立てと ID の付け替えは純粋なロジックとして切り出し、EditMode テストの対象にする

## 表示の操作

- ツールバーの「View」メニュー（`ToolbarMenu`）にまとめた表示切り替え。状態はウィンドウに保存する（ドメインリロード後も維持）
  - MiniMap: GraphView の `MiniMap`。位置・大きさは USS
  - Blackboard: グラフのパラメータのパネル（後述）
  - Snap to Grid: ノード・付箋を動かし終えたときにグリッドへ吸着する（後述）
- ショートカット（グラフにフォーカスがあるとき）: `F` = 選択範囲に合わせる（何も選んでいなければ全体）、`A` = 全体を表示。
  テキスト入力欄（付箋の見出し・Blackboard の名前や数値の欄など、`TextInputBaseField` 系すべて）に入力中は、文字入力を優先してショートカットを無視する
  ツールバーにも同じ操作のボタン（Frame Selection / Frame All）を置く

### ノードの折りたたみ

- GraphView の標準の折りたたみ（タイトルの ▼ ボタン。接続されていないポートを隠す）を使い、状態を `NodeData.Collapsed` に保存する（Undo 対応）
  - 折りたたむと「その時点で接続されていないポート」が隠れる。グラフを開く（再構築する）ときはエッジを繋いだ後に折りたたみ表示を更新し、
    接続済みのポートが隠れたままにならないようにする
- 折りたたんだノードはサマリー行も隠す（USS クラス `vne-node--collapsed`）
- 右クリックメニュー「Collapse All」/「Expand All」: 選択中のノードがあればそれだけ、無ければ全ノードを対象にする

### ノードの検索

- ツールバーの検索欄（`ToolbarSearchField`）。入力すると、タイトルか型の表示名に含まれる（大文字小文字は区別しない）ノードを強調し、それ以外を薄くする
- 件数を表示し、Enter で次の一致ノードを選択してフレームに収める（最後まで行ったら先頭へ戻る）。Esc で検索を消す
- グラフを再構築しても（Undo など）検索結果の表示を保つ
- 一致の判定は純粋な関数にし、EditMode テストの対象にする

### Blackboard（グラフのパラメータ）

- GraphView の `Blackboard` を使ったパネル。グラフの左上に重ねて表示する（位置・大きさは USS）
- 「+」で型（Bool / Int / Float / String）を選んで追加。名前は型名から重複しないように付ける（`Bool`, `Bool1`, …）
- 名前のダブルクリックで改名（空・重複する名前は受け付けない）。行の右クリック → Delete で削除。ドラッグで並べ替え
  - Blackboard がドロップ時に渡す位置は「ドラッグ中の行を取り除く前」の一覧で数えたものなので、下へ動かすときは 1 引いてから
    `NodeGraphAsset.MoveParameter`（取り除いた後の最終位置を受け取る）に渡す。自分のすぐ下に落としたときは何もしない
- 既定値は行の中の入力欄で編集する。すべて Undo 対応で、変更後はパネルを作り直す
- 検証: 空の名前・重複する名前を Error にする
- ランタイム: `GraphRunner` が開始時に既定値をコピーし、`GetBool` / `SetBool` / `GetInt` / … で読み書きする（`ParameterChanged` で通知）。
  名前が無い・型が違う場合は例外。値は Runner ごとで、アセットの既定値は変わらない。
  `Start()` で既定値に戻すときも、値が変わったパラメータは `ParameterChanged` で通知する（HUD などの表示が古い値のまま残らないように）

### グリッドへの吸着・整列

- Snap to Grid が有効なとき、ノード・付箋を動かし終えた時点でグリッドの間隔（`NodeGraphView.GridSpacing`、USS の `--spacing` と同じ 20px）に吸着させる
- 右クリックメニュー（ノードを 2 つ以上選択しているとき）
  - Align: Left / Right / Top / Bottom / Center Horizontally / Center Vertically
  - Distribute（3 つ以上）: Horizontally / Vertically。両端のノードは動かさず、間隔を均等にする
- 移動量の計算は純粋な関数にし、EditMode テストの対象にする。1 回の整列は 1 回の Undo で戻せる

## 検証（Validation）

グラフの整合性チェックは Runtime 側の純粋な C#（`GraphValidator`）で実装し、EditMode テストの対象にする。
結果は `GraphIssue`（重要度 Warning / Error、メッセージ、関係するノード ID）のリスト。

| 重要度 | ルール |
|---|---|
| Error | Entry ノードが無い / 2 つ以上ある（2 つ目以降のそれぞれに出す） |
| Error | 存在しないノード ID を指すエッジ |
| Error | ノード ID の重複 |
| Warning | Note ノードに接続しているエッジ（Note は接続不可） |
| Warning | 同じポート対を結ぶ重複エッジ |
| Warning | シーン未設定の Scene ノード |
| Warning | 読み込めなかったノード（型の改名・削除で `SerializeReference` が null になったもの） |
| Warning | イベント名が空の Event ノード（`GraphRunner.Raise` で指定できない） |
| Error | 名前が空のパラメータ / 名前が重複するパラメータ（Blackboard） |
| Warning | （エディタのみ）Build Settings に有効な状態で入っていないシーンを参照する Scene ノード（参照先のシーンが削除されていれば、その旨の警告） |

循環は許可する（State 間・Scene 遷移とも）。

Build Settings の確認は `EditorBuildSettings` を読むため Editor 側（`BuildSettingsSync`）で行い、`GraphValidator` の結果に追加して表示する。
エディタはグラフが変わるたびに再検証する。表示方法は「エディタ UI」の「問題の表示」を参照。

## ランタイム実行（GraphRunner）

ゲームからグラフを「シーン遷移表・ステートマシン」として動かす。すべて Runtime（UnityEngine のみ）に置く。

| 型 | 役割 |
|---|---|
| `GraphQuery` | グラフの読み取り専用ビュー。Entry、ノードの出力エッジ・遷移先、参照しているシーンの一覧などを引く（生成時のスナップショット） |
| `GraphRunner` | 純粋な C# の実行器。現在のノードを持ち、イベントで遷移する。シーンの読み込みは `ISceneLoader` に任せる |
| `ISceneLoader` / `SceneManagerSceneLoader` | シーン読み込みの抽象と、`SceneManager.LoadSceneAsync`（Single）による実装。テストでは偽物に差し替える |
| `GraphRunnerBehaviour` | シーンに置くコンポーネント。グラフを指定して開始し、UnityEvent でイベントに反応する |
| `GraphEventBinding` | `GraphRunnerBehaviour` のインスペクタで「イベント名 → UnityEvent」を対応付ける項目 |

### 実行の規則

- ノードは 2 種類に分かれる
  - 待機ノード: Scene・State（と、組み込み以外のノード型）。入ると止まり、次の操作を待つ。Scene に入るとそのシーンを読み込む
  - 通過ノード: Entry・Event。入ったら止まらずに次へ進む
- `Start()`: Entry に入る。Entry から Event 以外のノードへ繋がっていれば、そこへ進む（Event へしか繋がっていなければ Entry で待つ）
- `Raise(eventName)`: 現在のノードから出ているエッジのうち、`EventName` が一致する Event ノードへ進み、その Event から出ている最初のエッジの先へ進む
  - Event に出力エッジが無ければ、通知だけ行い現在のノードは変わらない（純粋な通知用のイベント）。
    Event → Event と連鎖した先が出力の無い Event でも同じで、その Event には入らず、直前の待機ノードに留まる
  - 一致する遷移が無ければ何もせず false を返す
- `Advance()`: 現在のノードから Event 以外のノードへ出ている最初のエッジの先へ進む（イベントを介さない「次へ」）
- 遷移先が複数ある場合は、アセット内のエッジの順で最初のものを使う
- 通過ノードだけで輪になっている場合（Event → Event → …）は無限ループせず、警告を出して止める
- 通知（`NodeEntered` など）の中で `Stop()` したら、そのノードの残りの処理（シーンの読み込み・イベントの通知）は行わない
- `Stop()` は `NodeExited` を通知する前に停止状態にする。その通知の中で `Raise` / `Advance` を呼んでも false を返して何もしない（次の `Start()` 後に実行されないように）
- 通知の中で `Stop()` → `Start()` し直した場合は、進行中だった遷移を打ち切り、新しい実行を通知の後に始める（世代番号で古い遷移を止める）
- 通知の中で例外が出た場合は、その遷移の間に積まれた Raise / Advance を捨てる（後の無関係な呼び出しで実行されないように）
- シーンの読み込み: `SceneReference.Path`（無ければ `Name`）を `SceneManager` に渡す。読み込み中のシーンが無ければ、すでにアクティブなシーンと同じなら読み込まない
  （Play ボタンを押したシーンが最初の Scene ノードと同じ場合に、二重に読み込まないため）。
  読み込み中（非同期で未完了）のシーンがあれば、アクティブなシーンではなく読み込み中のシーンと比べる
  （読み込み中はアクティブなシーンがまだ古いままなので、A → B → A と素早く戻ると A の読み込みを飛ばしてしまうのを防ぐ）

### イベント（C# / UnityEvent）

- `GraphRunner` の C# イベント: `NodeEntered` / `NodeExited`（ノードの出入り）、`EventTriggered`（Event ノードを通過した、または通知用イベントが発生した）
- `On(eventName, handler)` / `Off(...)`: 特定のイベント名だけを購読する
- `GraphRunnerBehaviour` は上記を UnityEvent として公開する（`On Node Entered (string nodeTitle)` と、イベント名ごとの `GraphEventBinding`）。
  `Raise(string)` / `Advance()` は public なので、UI の Button の OnClick などから直接呼べる
- `GraphRunnerBehaviour` は既定で `DontDestroyOnLoad`。同じグラフを動かす永続インスタンスが既にあれば、後から来た方は自分を破棄する
  （最初のシーンに置いた Runner が、そのシーンへ戻ったときに増えないように）

### 実行中の Runner の一覧

- `GraphRunner.Running`（静的）に実行中の Runner を持ち、`Started` / `Stopped` で通知する。エディタの強調表示が使う
- 「Enter Play Mode Options」でドメインリロードを切っても前回の値が残らないよう、`RuntimeInitializeOnLoadMethod(SubsystemRegistration)` で初期化する

## Build Settings 連携

- `BuildSettingsSync`（Editor）: グラフが参照するシーンのうち、Build Settings に無い・無効なものを求め、追加・有効化する
  - 既存の並び順は変えず、足りないシーンを末尾に追加する（ビルドでは index 0 のシーンから始まるため、Runner を置くシーンの位置はユーザーが決める）
  - 判定と新しいシーン一覧の組み立ては純粋な関数にし、EditMode テストの対象にする
- 実行方法: グラフウィンドウのツールバー「Add Scenes to Build」、または Project ビューでグラフを選んで `Assets > Visual Node Editor > Add Graph Scenes to Build Settings`
- 削除されたシーンは追加できないので飛ばし、結果のメッセージでその数を知らせる

## Play Mode 中の強調表示

- Play 中、開いているグラフを実行している `GraphRunner` があれば、その現在のノードを強調する（USS クラス `vne-node--running`）
- 遷移のたびに強調を移す。Runner が止まる・Play を終えると強調を消す
- 同じグラフを複数の Runner が動かしている場合は、最初に見つかった Runner を表示する

## エディタ UI

レイアウト・色・余白はすべて UXML/USS に置き、C# は USS クラスの付け外しだけを行う（スタイル値を C# に書かない）。
見た目に依らない判定ロジック（カテゴリ名の抽出、問題件数の表示文言、ポートラベルの表示可否、タイトルのフォールバック）は
純粋関数として切り出し、EditMode テストの対象にする。

### USS の構成

| ファイル（`Editor/Resources/VisualNodeEditor/`） | 読み込み先 | 内容 |
|---|---|---|
| `NodeGraphEditor.uss` | ウィンドウのルート | レイアウト、ツールバー、インスペクタ、問題一覧、カテゴリ色の変数 |
| `NodeGraphView.uss` | `NodeGraphView` 自身 | グリッド |
| `NodeView.uss` | 各 `NodeView` 自身 | ノードのカテゴリ帯、サマリー行、ポートラベル、検証結果の枠色 |

GraphView / Node は自身に既定の USS を持つ。同じ詳細度のルールは要素に近い USS が勝つため、
グリッドやノードのスタイルはウィンドウのルートではなく、その要素自身に USS を付けて確実に効かせる。

カテゴリ色は `.vne-root` に USS 変数（`--vne-category-flow` など）として定義し、ノードとインスペクタの両方から `var()` で参照する。

### レイアウト

```
┌ Toolbar ─────────────────────────────────────────┐
│ [Save]                 [Issues toggle] [asset name *] │
├──────────────────────────────┬──────────────────┤
│ graph (NodeGraphView)        ┃ inspector        │  ← TwoPaneSplitView（右ペイン固定・初期 300px）
│                              ┃                  │     境界はドラッグで変更、幅は view-data-key で保持
├──────────────────────────────┴──────────────────┤
│ issue list（問題があり、かつトグルが開いているときだけ表示）  │
└──────────────────────────────────────────────────┘
```

- グラフ領域とインスペクタは USS で最小幅を持つ。ウィンドウ自体にも最小サイズを設定し、ツールバーやインスペクタが画面外へ押し出されないようにする
- ツールバー
  - 問題件数は `ToolbarToggle`。押すと問題一覧を開閉する
  - アセット表示は名前のみ（パスはツールチップ）。未保存の変更があれば末尾に ` *`。クリックで Project ビューの該当アセットを Ping する。長い名前は先頭側を省略する
  - 未保存状態はアセット側の変更（Ctrl+S など）でも変わるため、定期的に確認して表示を更新する

### ノードの表示

- カテゴリ = `[NodeMenu]` パスの先頭セグメント（小文字化）。`vne-node--<category>` クラスを付ける
  - 色付きカテゴリ: `flow` / `state` / `event` / `entity` / `system`。それ以外（`misc` やカテゴリ無し）はグレー
  - 組み込みノードのパス: Entry・Scene = `Flow/…`、State = `State/State`、Event = `Event/Event`、Note = `Misc/Note`
- タイトル領域の上端にカテゴリ色の帯（3px）
- タイトル = ユーザーが入力したタイトル。空（空白のみを含む）なら型の表示名（`[NodeMenu]` パスの末尾、無ければ型名から `Node` を除いたもの）
- タイトルの直下にサマリー 1 行。内容は `NodeView.GetSummary()`（virtual）で型ごとに決める。空なら行ごと出さない
  - Event = イベント名、Scene = シーン名、State = 説明の 1 行目、Note = 本文の 1 行目
- 入力・出力がそれぞれ 1 つ以下のノードはポートラベル（`in` / `out`）を隠す（`vne-node--hide-port-labels`）。ポート名自体はエッジの識別子なので変えない
- インスペクタでの編集は、タイトル・サマリーへ即座に反映する

### インスペクタ

- `NodeInspectorView`。ノードを 1 つだけ選択しているときに、その `NodeData` を `SerializedObject` 越しに表示する
- ヘッダー: カテゴリ色のアイコン + 型の表示名（小さい文字）+ タイトル入力欄。タイトルが空なら薄い文字のプレースホルダー `(Title)` を出す
- その下に残りのフィールドを `PropertyField` で並べる。ラベルはフィールド名から生成した英語で統一する
  （エディタの言語設定によって `displayName` が一部だけ翻訳され、言語が混ざるのを避ける）
- ラベル幅はパネル幅に対する割合、入力欄は縮められるようにし、幅 220px でも全フィールドを操作できるようにする
- 編集はすべて `SerializedObject` のバインディング経由（Undo が効く）。変更を検知したらノード表示と検証結果を更新する

### 問題の表示

- 該当ノードの枠を色分けし（`vne-node--error` / `vne-node--warning`）、ツールチップにメッセージを出す
- ツールバーのトグルに件数を表示する。問題が無ければ `No issues`、エラーがあれば赤、警告のみならオレンジ
- 問題一覧は既定で閉じる。新しいエラーが出たら自動で開く（アセットを開いた時点で既にあるエラーでは開かない）
- 問題が無いときは、トグルの状態に関わらず一覧を表示しない（空の一覧の "List is empty" も出さない）
- 一覧の項目をクリックすると該当ノードを選択してフレームに収める

## 技術的な注意

- `UnityEditor.Experimental.GraphView` は Experimental だが Unity 6 でも利用可能。将来 UI Toolkit に正式なグラフ API が来たら移行を検討する（docs で提案してから）。
- UXML/USS は `Editor/Resources/VisualNodeEditor/` に置き、`Resources.Load` で読む（パッケージ内でもパス解決が安定するため）。
- Editor から Runtime の private フィールド名（`_nodes`, `_guid` など）を `SerializedProperty` で参照している箇所は、EditMode テストで名前の存在を検証する。
