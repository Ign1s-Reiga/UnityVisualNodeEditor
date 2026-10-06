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
│  (将来) GraphRunner / GraphQuery               │
└──────────────────────────────┘
```

## データモデル

- `NodeGraphAsset` : `List<NodeData>` を `[SerializeReference]` で、`List<EdgeData>` / `List<GroupData>` / `List<StickyNoteData>` を `[SerializeField]` で保持。
- `NodeData` : `Id`(GUID 文字列), `Title`, `Position` を持つ抽象基底。ポート定義はサブクラスごとに Editor 側の `NodeView` が決める。
  `Id` / `Position` は `[HideInInspector]`（インスペクタには出さない）。
- `EdgeData` : `(FromNodeId, FromPort, ToNodeId, ToPort)` の 4 つ組。ポートは文字列名で識別する。
- `GroupData` : `Id`, `Title`, `Position` と、所属ノードの ID リスト。ノードは高々 1 つのグループに属する。ノード削除時は全グループから ID を外す。
- `StickyNoteData` : 付箋（グラフ上のコメント）。`Id`, `Title`, `Contents`, `Rect`（位置とサイズ）, `Theme`, `FontSize`。
  ポートを持たずエッジも繋がらない。グループには属さない（GraphView 上でグループに入れても保存しない）。
  `Theme` / `FontSize` は GraphView の enum に依存しないよう Runtime 側に同じ値の enum（`StickyNoteTheme` / `StickyNoteFontSize`）を持つ。
  ポート無しの Note ノードとは役割を分ける: Note は構成要素としてのメモ（検索・検証の対象）、付箋は自由に置けるレイアウト上の注釈
- `SceneReference` : シーンの `Guid` と `Path`（`Name` は Path から導出）。Runtime は `SceneAsset` 型に触れず文字列だけを持つ。
  `SceneAsset` との相互変換は Editor の `SceneReferenceDrawer` が行う。
  ランタイムは Path（と Name）でシーンを読むため、Path は常に GUID から引き直して最新に保つ（`SceneReferenceSync`）:
  - シーンの移動・改名・再インポート時: `SceneReferencePostprocessor` が全グラフを更新して保存する
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
  - 先頭に識別子を持たせ、他ツールの文字列やこのツール以外の JSON は貼り付け不可と判定する
- コピー対象: 選択中のノード・グループ・付箋。グループを選ぶと中のノードも含める。エッジは両端のノードが対象に含まれるものだけ
- 貼り付け: すべての要素に新しい ID を振り、エッジ・グループの参照を新しい ID に付け替える。位置は元から少しずらす（同じ内容を続けて貼ると、さらにずらす）
- 貼り付けた要素を選択状態にする。1 回の貼り付け・複製・切り取りは 1 回の Undo で戻せる
- Entry を複製すると Entry が 2 つになるが、禁止はせず検証（Validation）のエラーで知らせる
- JSON の組み立てと ID の付け替えは純粋なロジックとして切り出し、EditMode テストの対象にする

## 表示の操作

- ミニマップ: GraphView の `MiniMap`。ツールバーのトグルで表示を切り替え、状態はウィンドウに保存する（ドメインリロード後も維持）。位置・大きさは USS
- ショートカット（グラフにフォーカスがあるとき）: `F` = 選択範囲に合わせる（何も選んでいなければ全体）、`A` = 全体を表示。
  ツールバーにも同じ操作のボタン（Frame Selection / Frame All）を置く

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

循環は許可する（State 間・Scene 遷移とも）。

エディタはグラフが変わるたびに再検証する。表示方法は「エディタ UI」の「問題の表示」を参照。

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
