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
- `NodeData` : `Id`(GUID 文字列), `Title`, `Position`, `Collapsed`, `ParentId` を持つ抽象基底。ポート定義はサブクラスごとに Editor 側の `NodeView` が決める。
  `Id` / `Position` / `Collapsed` / `ParentId` は `[HideInInspector]`（インスペクタには出さない。`Collapsed` は Position と同じくエディタ専用の表示状態）。
  `ParentId` は所属するコンテナの ID（空ならルート階層）。ノードは入れ子にせず平らなリストのまま持ち、階層は `ParentId` だけで表す（「コンテナ」参照）
- `GraphParameter` : グラフ単位のパラメータ（Blackboard）。`Id`, `Name`（グラフ内で一意）, `Type`（Bool / Int / Float / String）と型ごとの既定値。
  ノードの処理を書くためのものではなく（非目的）、ゲームから参照・更新する設定値・状態の置き場
- `EdgeData` : `(FromNodeId, FromPort, ToNodeId, ToPort)` の 4 つ組。ポートはポート ID（文字列）で識別する（「ポート」参照）。
  エッジの並び順に意味がある（同じノードから複数出ているとき、`Advance`・Entry・Event は最初のエッジに進む）。
  エッジを付け替えるときは `NodeGraphAsset.ReplaceEdge` で同じ位置のまま置き換え、行き先を変えない
- `GroupData` : `Id`, `Title`, `Position`, `ParentId` と、所属ノードの ID リスト。ノードは高々 1 つのグループに属する。ノード削除時は全グループから ID を外す。
  グループは置かれた階層（`ParentId`）にだけ表示し、同じ階層のノードだけを含む
- `StickyNoteData` : 付箋（グラフ上のコメント）。`Id`, `Title`, `Contents`, `Rect`（位置とサイズ）, `Theme`, `FontSize`, `ParentId`（置かれた階層）。
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

- ポートは `NodeView` サブクラスが定義する。ポートには「ID」と「表示名」があり、`EdgeData` に保存されるのは ID
  - `AddInputPort(id)` / `AddOutputPort(id)` は ID をそのまま表示名にする（Scene・State などの `in` / `out`）
  - `AddOutputPort(id, label)` は ID と表示名を分ける（コンテナの出口: ID = 出口の ID、表示名 = 出口の名前）。改名してもエッジが壊れない
  - ID は `Port.userData` に持ち、`NodeView.GetPortId(port)` で取り出す（`Port.portName` は表示名）。型システムは持たない（未決事項参照）
- 接続可否は `NodeGraphView.GetCompatiblePorts` で判定する: 向きが逆・別ノード・同じポート対が未接続・**同じ階層（`ParentId` が同じ）**であること
- 既定のポート構成: Entry = `out` のみ / Scene・State・Event = `in` + `out` / Note = ポート無し /
  Container = `in` + 出口ごとに 1 つ（出口の並び順）/ コンテナの Entry = `out` のみ / コンテナの Exit = `in` のみ

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

## コンテナ（サブグラフ）

ゲームのテンプレート（ADV・アクションなど）の下地として、入れ子の構造を表すノード。例: 「Stage」ノードの中に Playing / Paused / GameOver / Clear があり、Clear か GameOver で外へ出る。
ADV は、ひな形のグラフを作る ADV Starter で用意する（「ADV Starter」参照）。テンプレート専用のコンテナのサブクラスはまだ作らない。

### データモデル（Runtime）

| 型 | 役割 |
|---|---|
| `ContainerNode`（`[NodeMenu("Flow/Container")]`） | 他のノードを含むノード。子は子の `ParentId` だけで表す（コンテナ側に子の一覧は持たない）。出口の一覧 `_exits` を持つ |
| `ContainerExit` | コンテナの出口 1 つ。`Id`（作成後は変わらない GUID 文字列）と `Name`（表示名、編集可） |
| `ContainerEntryNode` | コンテナの中の開始点。コンテナごとにちょうど 1 つ |
| `ContainerExitNode` | コンテナの中の出口。親コンテナの出口のどれかを `_exitId` で指す。同じ出口を複数の Exit ノードが指してよい |

- ルート階層の開始点は従来どおり `EntryNode`（`Flow/Entry`）。`ContainerEntryNode` / `ContainerExitNode` とは別の型（ルートの始め方は未決事項）
- コンテナのポート: 入力 1 つ（`in`）と、`_exits` の並び順に出口ごとの出力 1 つ。出力ポートの ID = 出口の `Id`、表示名 = 出口の `Name`。
  エッジは ID を保存するので、出口を改名・並べ替えてもエッジは壊れない
- 出口は作成時にだけ `DefaultExitNames`（`protected virtual IEnumerable<string>`、既定は `{ "Next" }`）から作る。作成後はインスタンスごとのデータで、エディタから追加・改名・並べ替え・削除する。
  サブクラスで上書きすると初期の出口を変えられる（例: 将来のステージ用コンテナは `{ "Clear", "GameOver" }`）
- 名前で出口を引く `TryGetExit(name, out exit)` を用意する（テンプレートやゲームのコードから使う）

### 守るべき条件（検証で Error にする）

1. エッジは同じ `ParentId` のノード同士だけを結ぶ
2. コンテナごとに `ContainerEntryNode` がちょうど 1 つある
3. `ContainerEntryNode` / `ContainerExitNode` はコンテナの中にだけ置ける（ルート階層には置けない）。逆に、ルートの `EntryNode` はコンテナの中に置けない
4. `ParentId` は存在する `ContainerNode` を指し、親をたどって輪にならない
5. 出口の名前は空でなく、コンテナの中で重複しない。出口の ID もコンテナの中で重複しない
6. `ContainerExitNode` の `_exitId` は親コンテナに存在する出口を指す
7. コンテナの出力ポートから出るエッジは、存在する出口 ID を指す
8. コンテナを削除すると、子孫（入れ子のコンテナの中身も含む）とそれらのエッジ・グループ・付箋もすべて削除する（`NodeGraphAsset.RemoveNode` が行う）
9. コンテナの Entry は単独では削除・複製できない（エディタで削除・コピーの対象から外す。それでも欠けたり増えたりすれば条件 2 で検出）

### 出口の削除

- 出口を削除すると、その出力ポートから出るエッジも同じ Undo 単位で削除する
- その出口を指していた Exit ノードは削除しない。付け替えるまで条件 6 の Error として表示する
- エッジか、指している Exit ノードがある出口を削除するときは、影響を受けるもの（エッジの行き先、Exit ノード）を一覧した確認ダイアログを出す

### ランタイムの進み方

- コンテナに着いたら、そのコンテナに入り、中の Entry から続ける（通過ノードとして扱う）
- Exit ノードに着いたら、そのコンテナから出て、コンテナの出力ポートのうち ID が Exit の `_exitId` と同じものから続ける（親の階層の次のノードへ）
- 入れ子の深さに制限は無い。今どのコンテナの中にいるかは、現在のノードの `ParentId` の連なりで決まり、`GraphRunner.ContainerPath` で取れる（入るたびに積み、出るたびに下ろすスタックと同じ）
- データだけで遷移先を引く API を `GraphQuery` に置く: `GetNextNode(nodeId, portId)`（あるポートの先）、`GetContainerEntry(container)`、`GetExitTarget(exitNode)`（Exit から出た先）
- Exit に対応する出力ポートが繋がっていない場合の扱いは未決事項。**当面は**、出口の無い Event と同じく「どこにも入らず、直前の待機ノードに留まって警告を出す」
- Entry の無いコンテナ、Entry から 1 本もエッジが出ていないコンテナに着いたときも同じく、入らずに留まって警告を出す
  （入ると通過ノードの Entry で止まり、以後どの Raise / Advance でも動けなくなるため）。
  Entry から Event へのエッジだけがある場合は、ルートの Entry と同じく Entry で Raise を待つ。
  `Start()` の直後でまだ待機ノードが無ければ、ルートの Entry に入って留まる（Entry の先が無いときと同じ）

### エディタ

- 表示は階層ごとに切り替える（ドリルダウン）。コンテナをダブルクリック（または右クリック → Open Container）で中へ入り、
  グラフの下のパンくず（Root > Stage > …、コンテナの中にいるときだけ出す）か、背景の右クリック → Open Parent Level で上の階層へ戻る。
  左のノードツリー（「エディタ UI」の「ノードツリー」）からは、どの階層のノードへも直接移れる。
  表示中の階層はウィンドウに保存する（ドメインリロード後も維持。別のアセットを開くとルートから）
- Play 中の実行ノードの強調と、検証の枠の色は、対象がより深い階層にあれば、それを含む表示中の階層のコンテナに出す
- 表示するのは `ParentId` が表示中の階層と同じノード・グループ・付箋だけ。新しく作るものには表示中の階層を `ParentId` に入れる
- 条件 1 は `GetCompatiblePorts` でも守り、階層をまたぐエッジは作れない
- コンテナを作ると、`DefaultExitNames` から出口を作り、中に Entry と、既定の出口ごとに Exit ノードを 1 つずつ作る。
  Entry は最初の出口の Exit ノードに繋いでおく（作ったばかりのコンテナは、そのまま最初の出口へ通り抜ける）。既定の出口が無ければ繋がない
- コンテナのインスペクタ: 出口の一覧を編集できる（追加・改名・ドラッグで並べ替え・削除）。変更はすぐ出力ポートに反映する（グラフを作り直し、同じノードを選び直す）。
  名前は前後の空白を除き、空や同じコンテナの中での重複は受け付けない（理由を出して元の名前に戻す）。追加した出口は Exit, Exit 2, … と名付ける
- Exit ノードのインスペクタ: 出口を、親コンテナの出口から選ぶドロップダウンで表示する。最後の「+ New exit…」で親に出口を追加して選ぶ。
  選んでいる出口の名前もここで変えられる（親の出口の改名なので、同じ出口を指す Exit ノードとコンテナのポートにも反映される）
- Exit ノードのタイトルは指している出口の名前（改名にも追従）。ユーザーがタイトルを付けたらそちらを出し、出口の名前はサマリー行に出す。
  コンテナのサマリー行は子ノードの数（Entry / Exit は数えない）
- コンテナの Entry / Exit は専用のカテゴリ色（`container`）で、通常のノードと区別する。Exit は Create Node メニューの `Container/Exit`（コンテナの中でだけ出す）。
  Entry はコンテナと一緒に自動で作るのでメニューには出さない。ルートの `EntryNode` はルート階層でだけメニューに出す
- コピー・貼り付け: コンテナをコピーすると子孫も新しい ID でコピーし、`ParentId` を付け替える（出口の ID はコンテナの中だけで意味を持つのでそのまま）。
  コンテナの Entry は単独ではコピーしない（そのコンテナごとコピーしたときだけ含める）。
  貼り付け先の階層に置けないノード（Create Node メニューと同じ規則: ルートの Entry はルートだけ、コンテナの Entry / Exit はコンテナの中だけ）は貼り付けず、
  そのエッジ・グループの所属も外す。貼り付けた Exit ノードの出口が貼り付け先のコンテナに無ければ、`CreateNode` と同じく最初の出口を指す
- 選択範囲をまとめる（右クリック → Group into Container。`ContainerGrouping`）: 選択中のノードを、まとめたノードの一番左・縦の中央に作る新しいコンテナへ移す。
  ルートの Entry とコンテナの Entry / Exit は移さない（それしか選んでいなければメニューを無効にする）。移したノードは表示中の階層のグループから外す。
  境界をまたぐエッジは次のように付け替え、まとめる前と同じ順に進むようにする。まとめたノード同士のエッジはそのまま。1 回の Undo で戻せ、作ったコンテナを選択する
  - Event ノードは、そこへ繋がっているノードと同じ側に置く（`Raise` は今のノードから直接繋がった Event だけを探すので、間に境界を挟むと起こせなくなる）。
    選んでいなくても、繋がっている元がすべて中に入るなら一緒に入れ、元がすべて外に残るなら選んでいても外に残す
  - まとめられないとき（メニューは押せるが、何も変えずに理由をウィンドウの通知に出す）:
    繋がっている元が中と外の両方にある Event がある / 外から入ってくるエッジの行き先が 2 つ以上ある（コンテナの入口は 1 つなので、別々のノードへの入り方を保てない）
  - 入ってくるエッジ: 外側はコンテナの `in` へ。中では Entry をその行き先へ繋ぐ。
    入ってくるエッジが無ければ、一番左の State・Scene・コンテナへ繋ぐ（Note はポートが無く、Event は Entry から通らないので選ばない。どれも無ければ繋がない）
  - 出ていくエッジ: 出元のポートごとに出口を 1 つ作る。中では出元のポート → その出口の Exit ノード、外ではその出口のポート → 元の行き先。
    出口の名前は、出元が名前のある Event ならそのイベント名、そうでなければ行き先の名前。重複すれば `Name 2` のように番号を付ける
  - 外へ出ていくエッジが無ければ、既定の出口（Next）と、その Exit ノードを繋がずに置く
  - 付け替えたエッジは元のエッジと同じ位置に置く（`ReplaceEdge`）。入ってくるエッジ → `in` と、出元のポートの最初の外へのエッジ → Exit ノードは置き換え、
    同じポートの残りの外へのエッジは消す。コンテナの出口からのエッジは、元の並び順で後ろに足す（コンテナから出るエッジは新しいので、他のノードの順に影響しない）
- ノード検索は表示中の階層が対象。問題一覧から別の階層のノードを選ぶと、その階層へ移ってから選択する
- すべて Undo 対応。Undo で表示中のコンテナが消えたら、存在する一番近い親の階層へ戻る

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
| Warning | イベント名が空の Event ノード（`GraphRunner.Raise` で指定できない）。トリガーが自動の Event は名前が無くてよいので対象外 |
| Error | 条件付き遷移: 条件のパラメータが無い / 比較がパラメータの型に合わない。Warning: 選ばれることの無い自動の Event、使われない条件（「条件付き遷移」参照） |
| （ノードによる） | ノード自身の検証（`NodeData.GetIssues()`。例: Yarn のノード名が空の Dialogue ノード） |
| Warning | 読み込めなかった振る舞い（`NodeBehaviour` の型の改名・削除で null になったもの。実行時は飛ばす） |
| Warning | イベント名の表記ゆれ: 大文字小文字・前後の空白だけが、先に出てきた別のイベント名と違う（`Raise` は完全一致なので、どちらかに揃える） |
| Warning | 前後に空白があるイベント名（`Raise` でその空白まで打たないと一致しない） |
| Error | 名前が空のパラメータ / 名前が重複するパラメータ / ID が重複するパラメータ（Blackboard） |
| Error | コンテナの条件 1〜7（階層をまたぐエッジ、Entry の数、Entry / Exit を置ける階層、親の参照と輪、出口の名前と ID、Exit が指す出口、出力ポートが指す出口）。「コンテナ」参照 |
| Warning | （エディタのみ）Build Settings に有効な状態で入っていないシーンを参照する Scene ノード（参照先のシーンが削除されていれば、その旨の警告） |
| Warning | （エディタのみ）Scene ノードがあるのに、有効な Build Settings のシーンのどれにもこのグラフの Graph Runner が無い（「ワンクリック Play」参照） |

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
| `GraphRunnerBehaviour` | シーンに置くコンポーネント。グラフを指定して開始し、UnityEvent でイベントに反応する。`Update` / `FixedUpdate` を Runner に渡す（「ノードの振る舞い」参照） |
| `GraphEventBinding` | `GraphRunnerBehaviour` のインスペクタで「イベント名 → UnityEvent」を対応付ける項目 |

### 実行の規則

- ノードは 2 種類に分かれる
  - 待機ノード: Scene・State（と、組み込み以外のノード型）。入ると止まり、次の操作を待つ。Scene に入るとそのシーンを読み込む
  - 通過ノード: Entry・Event。入ったら止まらずに次へ進む
- `Start()`: Entry に入る。Entry から Event 以外のノードへ繋がっていれば、そこへ進む（Event へしか繋がっていなければ Entry で待つ）
- `Raise(eventName)`: 現在のノードから出ているエッジのうち、`EventName` が一致する Event ノードへ進み、その Event から出ている最初のエッジの先へ進む
  - Event に条件があれば、満たすものだけを選ぶ（同じ名前の Event を条件違いで並べて行き先を分けられる）。トリガーが自動の Event は選ばない（「条件付き遷移」参照）
  - Event に出力エッジが無ければ、通知だけ行い現在のノードは変わらない（純粋な通知用のイベント）。
    Event → Event と連鎖した先が出力の無い Event でも同じで、その Event には入らず、直前の待機ノードに留まる
  - 一致する遷移が無ければ何もせず false を返す
- 待機ノードに入り終えたときと、パラメータの値が変わったときに、そのノードから出ている自動の Event の条件を調べ、満たせば進む（「条件付き遷移」参照）
- `Advance()`: 現在のノードから Event 以外のノードへ出ている最初のエッジの先へ進む（イベントを介さない「次へ」）
- 遷移先が複数ある場合は、アセット内のエッジの順で最初のものを使う
- コンテナ・コンテナの Entry / Exit は通過ノード。コンテナに入って中の Entry から続け、Exit で出て親の階層の対応する出力ポートから続ける（「コンテナ」の「ランタイムの進み方」参照）
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

### ボタンから送る（`GraphEventButton`）

Runner は最初のシーンに `DontDestroyOnLoad` で残るので、後のシーンのボタンの OnClick からは参照できない（シーンをまたぐ参照はできず、
そのシーンに 2 つ目の Graph Runner を置いても重複として破棄される）。コードを書かずにボタンからイベントを送れるよう、専用のコンポーネントを用意する。

- `GraphEventButton`（`Add Component > Visual Node Editor > Graph Event Button`）: 送り先のグラフ（空なら実行中のどれか）、操作（Raise / Advance）、イベント名
- Runner は `GraphRunner.Running` から、そのグラフを実行中のものを探す。参照は持たない
- 同じ GameObject にある、public な `onClick`（`UnityEvent`）を持つコンポーネント（uGUI の Button など）に、有効になったとき自動で繋ぐ。
  パッケージを uGUI に依存させない（CI のコンパイル確認にも uGUI は無い）ため、型ではなく名前で探す。それ以外からは任意の UnityEvent で `Send()` を呼ぶ。
  ボタンの OnClick（インスペクタ）で既にこのコンポーネントの `Send` を呼んでいれば（Off にした呼び出しは数えない）自動では繋がない（1 回のクリックで 2 回送らないように。インスペクタにもその旨を出す）
- 送れなかったとき（Runner が無い・今のノードからその名前の遷移が無い）は、開発ビルドだけ警告を出す
- グラフにあるイベント名は `GraphEventNames.Collect`（Runtime。空を除き、グラフ内の順で重複なし）で取る。インスペクタのドロップダウンが使う

### 実行中の Runner の一覧

- `GraphRunner.Running`（静的）に実行中の Runner を持ち、`Started` / `Stopped` で通知する。エディタの強調表示が使う
- 「Enter Play Mode Options」でドメインリロードを切っても前回の値が残らないよう、`RuntimeInitializeOnLoadMethod(SubsystemRegistration)` で初期化する

## ノードの振る舞い（NodeBehaviour）

Animator の `StateMachineBehaviour` と同じ考え方で、待機ノード（State・Scene）に C# のクラスを付け、
Runner がそのノードにいる間の処理（`Update` / `FixedUpdate` 相当）を書けるようにする。
処理は C# で書き、ノードには「どのクラスを、どの設定値で動かすか」だけを持たせる（ロジックをノードで組むビジュアルスクリプティングにはしない。非目的を守る）。

### データ（Runtime）

| 型 | 役割 |
|---|---|
| `NodeBehaviour` | 抽象基底。`[Serializable]` の純粋な C#（MonoBehaviour でも ScriptableObject でもない）。`OnEnter()` / `OnUpdate(float deltaTime)` / `OnFixedUpdate(float fixedDeltaTime)` / `OnExit()` を必要なものだけ上書きする。`Runner`・`Node`・`Host`（Runner を動かしている `GraphRunnerBehaviour`。コンポーネントを使っていなければ null）を参照できる |
| `IBehaviourHost` | 振る舞いを持てるノード。`StateNode`・`SceneNode` が実装し、`_behaviours` を `[SerializeReference]` のリストで持つ。独自の待機ノードも実装できる |

- `_behaviours` はノードのインスペクタが名前で除外し、専用の UI で出す。`[HideInInspector]` は付けない
  （付けると、その中の振る舞いのフィールドまで `SerializedProperty` 上で非表示扱いになり、編集欄を作れない）

- サブクラスにも `[Serializable]` が要る（属性は継承されない）。public / `[SerializeField]` のフィールドはノードのインスペクタで編集でき、グラフアセットに保存される
- グラフはアセットなので、シーン上のオブジェクトをフィールドで参照することはできない。シーンのものは `Host`（の `gameObject` など）や `FindFirstObjectByType` から実行時に引く
- 型が削除・改名されて読めなくなった振る舞い（リストの null）は実行時に飛ばし、検証で Warning にする

### 実行（GraphRunner）

- `GraphRunner.Update(deltaTime)` / `FixedUpdate(fixedDeltaTime)`: 現在のノードの振る舞いの `OnUpdate` / `OnFixedUpdate` を順に呼ぶ。
  `GraphRunnerBehaviour` が自分の `Update` / `FixedUpdate` から `Time.deltaTime` / `Time.fixedDeltaTime` を渡して呼ぶ（Runner を直接使う場合は自分で呼ぶ）
- 振る舞いを持つノードに入ったら `NodeEntered` の前に `OnEnter`、出るときは `NodeExited` の前に `OnExit` を呼ぶ（`Stop()` でも `OnExit`）。通過ノードは振る舞いを持たない。
  Scene ノードの `OnEnter` はシーンの読み込みを始める前に呼ばれる（読み込みは非同期）ので、そのシーンのオブジェクトは `OnUpdate` から引く
- 振る舞いのインスタンスは Runner ごとに、`Start()` のたびにアセットの値から複製して作る（`JsonUtility` の往復）。
  パラメータと同じく毎回まっさらから始まり、同じグラフを複数の Runner で動かしても状態が混ざらず、実行中の変更はアセットに書き戻されない
- 振る舞いの中から `Runner.Raise` / `Advance` / `Stop` を呼んでよい。`OnUpdate` / `OnFixedUpdate` の中で遷移・停止したら、そのノードの残りの振る舞いは呼ばない（同じノードへ戻る遷移でも同じ。入り直した振る舞いの更新は次の `Update` から）。
  `OnEnter` / `OnExit` の中からの `Raise` / `Advance` は、いま進めている遷移の後に行う（`NodeEntered` などの通知と同じ）
- 振る舞いで例外が出たら、ログに出して次の振る舞いへ進む（1 つの不具合で Runner 全体が止まらないように）

### エディタ

- State・Scene ノードのインスペクタに「Behaviours」の一覧を出す。各振る舞いのフィールドを `PropertyField` で編集し（Undo 対応）、
  Edit Script でスクリプトを開き、Remove で外し、上下のボタンで並べ替える。読めなくなった振る舞いは「Missing behaviour」と出し、Remove だけできる
- Add Behaviour: `NodeBehaviour` のサブクラス（抽象・ジェネリック・引数なしのコンストラクタが無いものを除く）から選んで追加する
- Create Script…: 保存先を選ぶと、ファイル名からクラス名を作り、`OnEnter` / `OnUpdate` / `OnFixedUpdate` / `OnExit` の空の骨組みを書き出して IDE で開く。同じ名前のファイルや、同じ名前のクラス（`NodeBehaviour` 以外も含む。骨組みは名前空間の無いクラスなので衝突する）が既にあれば作らない。
  コンパイル（ドメインリロード）後に、そのクラスを元のノードへ自動で追加する（追加待ちは `SessionState` に覚えておく）
- ノードには、付いている振る舞いのクラス名を 1 行で出す（USS クラス `vne-node__behaviours`）

## 条件付き遷移（Event の条件と自動遷移）

ADV のルート分岐（`Affection >= 5` なら Alice ルート）や、フラグによる遷移（`Lives <= 0` → GameOver）を、コードを書かずにグラフで表す。
Event ノードに宣言的な条件を持たせるだけにし、ロジックのグラフにはしない（非目的を守る。条件は「パラメータ・比較・値」の並びで、式や計算は書けない）。

### データ（Runtime）

| 型 | 役割 |
|---|---|
| `EventNode` | トリガー `_trigger`（`EventTriggerMode`）と条件の一覧 `_conditions`（`List<TransitionCondition>`）を持つ |
| `EventTriggerMode` | `Raise`（既定。名前で起こされたときに進む）/ `Automatic`（待機中に条件を満たしたら自分で進む） |
| `TransitionCondition` | 条件 1 つ。パラメータの ID（`GraphParameter.Id`。改名しても壊れない）、比較（`ConditionOperator`）、比べる値（型ごとの欄 `_intValue` / `_floatValue` / `_stringValue`。Bool は比較だけで表す） |
| `ConditionOperator` | `IsTrue` / `IsFalse`（Bool）、`Equal` / `NotEqual`（Int・Float・String）、`Less` / `LessOrEqual` / `Greater` / `GreaterOrEqual`（Int・Float） |

- 条件はすべて満たすとき（AND）に有効。条件が無ければ常に有効。OR は同じ行き先への Event を 2 つ置いて表す
- 比べ方: Int・Float は数値、String は大文字小文字を区別する完全一致。Float の `Equal` も丸めない完全一致
- 判定は純粋な関数（`TransitionCondition.IsMet`。パラメータの型と値を受け取る）にし、EditMode テストの対象にする

### 実行（GraphRunner）

- `Raise(name)`: 今のノードから出ているエッジの順に、トリガーが `Raise` で、名前が一致し、条件を満たす最初の Event へ進む。
  同じ名前の Event を条件違いで並べれば行き先を分けられ、最後に条件の無いものを置けば「それ以外」になる。満たすものが無ければ何もせず false
- 自動遷移: 待機ノードにいる間、そのノードから出ているトリガーが `Automatic` の Event をエッジの順に調べ、条件を満たす最初のものへ進む（進み方は `Raise` と同じ）。
  調べるのは次のときだけ（毎フレームは調べない）:
  1. 待機ノードに入り終えた直後（振る舞いの `OnEnter`・`NodeEntered`・シーンの読み込み開始の後）。Entry で待つ場合も同じ
  2. パラメータの値が変わったとき（`ParameterChanged` の後）。遷移の途中で変わった場合は、その遷移が終わってから調べる
- 自動の Event は `Raise` では選ばない（起こし方を 1 つに決める）。名前は無くてよい。名前があれば、通過時に `EventTriggered` / `On(name)` へ通知する
- 自動遷移が続く（A に入ると B へ、B に入ると A へ…）とき、1 回の操作の中で 100 回を超えたら警告を出し、その時点のノードに留まる
- 条件とトリガーを見るのは、待機ノードから直接繋がった Event だけ。Event から Event へ繋いだ先の Event の条件・トリガーは使わない（検証で Warning）
- パラメータが見つからない・型の合わない条件は満たさない扱い（検証で Error）

### 検証

| 重要度 | ルール |
|---|---|
| Error | 条件のパラメータが無い（削除された） |
| Error | 比較がパラメータの型に合わない（Bool に `>` など） |
| Warning | 条件の無い自動の Event より後ろ（同じノードからのエッジの順）にある自動の Event（選ばれることが無い） |
| Warning | 別の Event から繋がっている Event の条件・自動（使われない） |

### エディタ

- Event のインスペクタ: Trigger（Raise / Automatic）と Conditions の一覧。行ごとに、パラメータ（Blackboard から選ぶドロップダウン。名前は打たない）、
  比較（パラメータの型で使えるものだけ）、値（型に合った欄。Bool は無し）、削除ボタン。「+」で追加する（最初のパラメータと、その型の最初の比較）。すべて Undo 対応
- 条件は ID でパラメータを指すので、改名には追従する。削除されたパラメータを指す条件は「(missing)」と出す（検証で Error）
- ノードの表示: 条件をサマリー行に出す（例: `if Affection >= 5 and Met`。タイトルを付けていればイベント名と並べて `Done if …`）。
  自動の Event は札に印を付ける（USS クラス `vne-node--automatic`）。名前の無い自動の Event のタイトルは `Auto`
- Now running: Raise のボタンは、その名前で条件を満たす Event が無ければ薄く出し（USS クラス `vne-runtime-panel__action--blocked`）、
  ツールチップに満たしていない条件を出す（無効にするとツールチップが出ないので、押せるまま。押しても進まない）。値が変わったら出し直す（値は毎フレーム変わることもあるので、次の更新で 1 回にまとめる。スロット debug のセーブがあるかは、Runner を変えたとき・セーブしたときだけ確かめる）。自動の Event はボタンに出さない
- Graph Event Button のイベント名の候補には、自動の Event の名前を出さない（Raise では選ばれないため）
- MCP: `get_graph` の Event にトリガーと条件を出し、`add_node` / `update_node` で `trigger` と `conditions`（パラメータ名・比較・値）を指定できる

## セーブ・ロード

ADV のセーブスロット（「続きから」）のために、Runner の状態を文字列にして保存し、そこから再開できるようにする。

### データ（Runtime）

| 型 | 役割 |
|---|---|
| `GraphRunnerState` | 保存する状態（`[Serializable]`。`ToJson` / `FromJson` は `JsonUtility`）。形式の版、グラフ名（確認用）、今いるノードの ID、最後に読み込みを頼んだシーン、パラメータの値（名前・型・値）、Extras |
| `GraphExtras` | Runner の `Extras`。パラメータ（Blackboard）に無い値の置き場（キー → Bool / Int / Float / String）。Yarn Spinner の変数などアドオンが使う。`Start()` で空になり、セーブに含まれる |
| `IRestartOnLoad` | ロードするとノードの最初からやり直すノード（Dialogue ノード）の印。このノードにいる間に保存すると、ノードに入る前の値で保存する |
| `GraphSaveSlots` | スロット名でファイルに読み書きする（`Application.persistentDataPath/VisualNodeEditor/<グラフ名>-<グラフのセーブキー>/<スロット>.json`）。`Save` / `Load` / `Exists` / `Delete`。スロット名はファイル名に使える文字だけ |
| `NodeGraphAsset.SaveKey` | グラフごとに一意なセーブの置き場の名前。新しく作るグラフ（`NodeGraphFactory.CreateNew`。Create メニュー・MCP の `create_graph`・ADV Starter）に新しい ID を入れる。アセットを複製すると同じキーを持つので、読み込まれたグラフのキーが別のグラフと同じなら、`GraphSaveKeySync` が新しい ID に入れ直して保存する（未保存の変更があるグラフは保存せず、未保存状態にするだけ）。キーを持たないグラフ（0.13.0 までに作ったもの）は書き換えず、グラフ名だけのフォルダを使う（クローン・再インポート・CI でアセットが変わらないように）。キーを持つグラフのスロットが無ければ、グラフ名だけのフォルダ（キーを持つ前のセーブ）も探す |

`GraphSaveSlots.Save` は一時ファイル（`<スロット>.json.tmp`）に書いてから、`File.Replace` で 1 回で置き換える（書きかけのファイルも、スロットが無い瞬間も作らない）。
`File.Replace` が使えないとき（対応しないファイルシステム・プラットフォーム）は、古いセーブを消してから一時ファイルを移す。

### 実行（GraphRunner）

- `CaptureState()`: 今の状態を返す（実行中でなければ例外）。今のノードが `IRestartOnLoad` なら、そのノードに入る直前（振る舞いの `OnEnter` より前）に写した値を使う
  （Yarn の会話の途中で保存しても、ロードすると会話を最初からやり直すので、会話の中の `<<set $Affection += 1>>` が二重にかからないように）
- `RestoreState(state)`: 保存したノードから再開する。ノードが見つからなければ（グラフが変わってノードが消えた）、何も変えずに false
  1. 実行中なら今のノードから出る（`OnExit`・`NodeExited`）。止まっていれば実行中にする（`Started`）。振る舞いは `Start()` と同じく複製し直す
  2. パラメータを既定値に戻してから、保存した値のうち同じ名前・型のものを入れる（変わったものは `ParameterChanged`）。Extras を入れ替える
  3. 入るノードが Scene ノードでなく、保存したシーンが今のシーン（読み込み中ならそのシーン）と違えば、そのシーンを読み込む
  4. 保存したノードに入る（`OnEnter`・`NodeEntered`。Scene ノードならシーンを読み込み、そのあと自動遷移を調べる）
  - 遷移の通知の中から呼ばれたら、その遷移の後に行う（`Raise` と同じ）
- Runner は最後に読み込みを頼んだシーン（`LastScene`）を覚える（シーンを持たない待機ノードで保存しても、ロードで同じシーンに戻れるように）

### コンポーネントとエディタ

- `GraphRunnerBehaviour`: `SaveSlot(string)` / `LoadSlot(string)`（UnityEvent から呼べる）。止まっていてもロードできる（保存したノードから始まる）
- `GraphEventButton`: 操作に `Save` / `Load` を足す（スロット名の欄。既定 `slot1`）。タイトル画面の「続きから」、ゲーム中の「セーブ」をコード無しで置ける
- スロット名は英数字・空白・'-'・'_' だけで、Windows が装置の名前として予約している CON・PRN・AUX・NUL・COM0〜9・LPT0〜9 は使えない（`.json` を付けても作れないため。インスペクタで注意を出す）
- Now running パネルに Save / Load のボタン（スロット `debug`）を出し、Play 中にセーブ・ロードを試せるようにする

## Yarn Spinner 連携（任意）

ADV（ノベル・アドベンチャー）では、会話の中身を Yarn Spinner に書き、グラフはチャプター・ルート・シーン・フラグ・セーブを受け持つ（構成図としてのグラフ）。

- 対象: Yarn Spinner for Unity 3.x（パッケージ `dev.yarnspinner.unity`。開発用プロジェクトでは 3.2.8 を使う）。2.x は API が違う（`StartDialogue` が同期、Dialogue View）ので対象外
- 置き場: パッケージの `Integrations/YarnSpinner/`（`Runtime` / `Editor` / `Tests`）。Yarn Spinner が入っていないプロジェクトではコンパイルされず、Yarn を使わないゲームに影響しない。
  asmdef の `versionDefines` で `dev.yarnspinner.unity` 3.0.0 以上があれば `VNE_YARN_SPINNER` を定義し、`defineConstraints` でそれを要求する。
  Yarn のランタイム（`YarnSpinner.Unity`）と、自動で参照される `YarnSpinner.dll`（`Yarn.IVariableStorage` など）を使う

| asmdef | 名前空間 | 中身 |
|---|---|---|
| `Reiga.VisualNodeEditor.YarnSpinner` | `Reiga.VisualNodeEditor.YarnSpinner` | `DialogueNode`、`YarnGraphBridge`、`GraphVariableStorage` |
| `Reiga.VisualNodeEditor.YarnSpinner.Editor` | `Reiga.VisualNodeEditor.YarnSpinner.Editor` | Dialogue ノードの View・インスペクタ・検証、ADV Starter |
| `Reiga.VisualNodeEditor.YarnSpinner.Tests` | `Reiga.VisualNodeEditor.YarnSpinner.Tests` | EditMode テスト |

### Dialogue ノード（`DialogueNode`、`[NodeMenu("ADV/Dialogue")]`）

- 待機ノード。Yarn のノード名 `_yarnNode` と、会話が終わったときに起こすイベント名 `_completeEvent`（既定 `Done`）を持つ。振る舞い（`IBehaviourHost`）も付けられる。`IRestartOnLoad`
- ポートは `in` / `out`（State と同じ）。カテゴリ色は `adv`（メニューの `ADV/` から）。タイトル未入力なら Yarn のノード名をタイトル・ノードツリー・Now running に出す（`NodeData.ContentTitle`）
- ノード自身の検証（`NodeData.GetIssues()`）: Yarn のノード名が空なら Warning

### 会話の進め方（`YarnGraphBridge`）

DialogueRunner と同じ GameObject に付けるコンポーネント（`Add Component > Visual Node Editor > Yarn Graph Bridge`）。グラフの Runner と Yarn の DialogueRunner を繋ぐ。

- 繋ぐ Runner: 指定したグラフ（空なら実行中のどれか）を実行中のものを `GraphRunner.Running` から探す。後から始まったものには `GraphRunner.Started` で付く
- Dialogue ノードに入ったら、そのノードの Yarn ノードで会話を始める（`StartDialogue`）。会話中なら先に止める
- DialogueRunner に Yarn Project が無い・そのノードが無いときは、始めずに警告を出し、会話が終わったときと同じく次へ進める
  （Yarn は始めるのに失敗すると終わりの知らせを出さないので、待つとその Dialogue ノードで止まってしまう）
- Bridge が有効になった時点で Runner がすでに Dialogue ノードにいれば、その会話を始める（シーンの読み込みが終わる前に、自動遷移で Dialogue ノードへ進んでいた場合）
- 会話が終わったら（`onDialogueComplete`）、次のフレームで次の順に試す（会話の終わりの通知の中では次の会話を始められないため）:
  1. 会話の中で `<<graph_next 名前>>` があれば、その名前で Raise
  2. なければ Dialogue ノードの完了イベント（既定 `Done`）で Raise（条件付きの Event で分岐できる）
  3. どちらも無ければ Advance（Dialogue → Dialogue と直接繋いだ一本道）
  4. どれもできなければ警告を出して留まる
  - そのときの Runner のノードが、その会話を始めた Dialogue ノードでなければ何もしない
- Dialogue ノードから出たとき、まだ会話中なら止める（`Stop()`。`<<graph_event>>`・自動遷移・外からの Raise でノードを離れた場合）
- Bridge が無効になったとき（GameObject を消した・ポーズで止めたなど）も会話を止める。止まった知らせ（`onDialogueComplete`）は無効の間も聞き続け（外すのは Bridge を壊したとき）、
  また有効になっても、その知らせが来るまでは次の会話を始めない（`DialogueFlow.Shutdown`。Yarn は止めた会話の後片付けが終わってから知らせるので、
  その前に始めると、遅れて来た知らせを新しい会話の終わりと取り違えて次へ進んでしまう）。コマンドは無効の間は外す
- Yarn のコマンド（Bridge が DialogueRunner に登録する）:
  - `<<graph_event 名前>>`: すぐ Raise する（演出の合図など。それで Dialogue ノードを離れれば会話は止まる）
  - `<<graph_next 名前>>`: 会話が終わったときに Raise する名前を決める（選択肢でルートを選ぶ）
- オートセーブ: `Auto Save Slot`（空なら無効）を指定すると、Dialogue ノードに入るたびにそのスロットへ保存する（「続きから」用）
- エディタで追加したとき（`ObjectFactory.componentWasAdded`。`YarnBridgeSetup`）、同じ GameObject に `GraphVariableStorage` が無ければ足し、
  DialogueRunner の Variable Storage に設定する（別の Variable Storage が設定されていれば置き換えない）
- 判定は Yarn に依存しない純粋なクラス `DialogueFlow` に置き、会話の開始・停止を `IDialoguePlayer`（実物は `DialogueRunnerPlayer`）越しに行う（EditMode テストで偽物に差し替える）。
  同じ Dialogue ノードに入り直したときも会話をやり直せるよう、ノードに入るたびに番号を振って見分ける。
  Yarn は `StartDialogue` 1 回につき、終わったときも `Stop()` で止めたときも `onDialogueComplete` をちょうど 1 回呼ぶので、止めるよう頼んだ後の 1 回は「止まった」として扱い、次へは進めない

### 変数の共有（`GraphVariableStorage`）

Yarn の `VariableStorageBehaviour`。DialogueRunner の Variable Storage に設定する。

- Yarn の `$Name` は、Blackboard のパラメータ `Name` と同じもの。読み書きは Runner のパラメータに対して行う
  （`ParameterChanged` も出るので、条件付き遷移・Play 中の Blackboard・セーブがすべて同じ値を見る）
- 型の対応: Bool ↔ Yarn の bool、Int ↔ 数（書くときは四捨五入し、整数でなければ警告）、Float ↔ 数、String ↔ 文字列。型の合わない書き込みはエラーを出して無視する
- Blackboard に無い変数（Yarn だけで使う変数や、Yarn が使う訪問回数 `$Yarn.Internal.*`）は Runner の `Extras` に置く（セーブに含まれ、`Start()` で消える）
- Runner が見つからない間は、自分の中の一時的な置き場を使い、警告を 1 回出す（会話は続けられる）
- 値が変わったら Yarn の変更リスナー（`AddChangeListener`）にも知らせる。有効な間は Runner の `ParameterChanged` を聞き、グラフ側（ゲームのコード・ロードなど）で変わった値も知らせる
- `Clear()` は Yarn だけの変数（Extras と一時的な置き場の `$` で始まるもの）を消す。Blackboard のパラメータはグラフのものなので消さない

### エディタ

- Dialogue ノードのインスペクタ: Yarn Node（`[YarnNode]` の欄。`YarnNodeDrawer`）は、入力欄と「▾」のメニュー（プロジェクトの YarnProject のノード名。
  プロジェクトが 2 つ以上ならプロジェクトごとにまとめる。一覧に無い名前も入力できる）。Yarn が内部で作るノード（`$` で始まる・`Yarn.Internal` を含む）は出さない。
  Complete Event（`[EventName]` の欄）は Event 名の入力欄（`EventNameField` と同じ候補）
- YarnProject のノード名と変数の初期値は `YarnProjects` が集めて覚え、プロジェクトのアセットが変わったら（`EditorApplication.projectChanged`）捨てる（編集のたびの検証で読み直さない）
- エディタでの検証（Warning。`YarnIssueCheck`。Dialogue ノードの無いグラフには何も言わない）: YarnProject が 1 つも無い / どの YarnProject にも無い Yarn のノード名 /
  Blackboard のパラメータと、同じ名前の Yarn の変数（YarnProject の初期値から型が分かる。Yarn の数は Int・Float のどちらとも合う）の型が合わない
- 拡張点（コアに足す。Yarn 以外のアドオンも使える）: Runtime の `NodeData.ContentTitle`（タイトル未入力のときに型名の代わりに出す名前）・
  `NodeData.GetIssues()`（ノード自身の検証。`GraphValidator` が呼ぶ）・`[EventName]`（イベント名を選べる欄にする属性）・`NodeBehaviourList`（public に）・
  `GraphRunner.FindRunning` / `TryGetParameter`。Editor の `IGraphIssueCheck`（`TypeCache` で集め、`GraphIssues.Collect` が呼ぶ。AssetDatabase を見る検証用。例外はログに出して次へ）

### 中断と再開

- Yarn はノードの途中から再開できないので、Dialogue ノードにいるときのセーブをロードすると、その会話の最初から始まる。値はその Dialogue ノードに入る前のもの（`IRestartOnLoad`）

### CI

- CI（`dotnet build` のコンパイル確認）では Yarn 連携をコンパイルしない。Yarn Spinner のランタイムが uGUI（`UnityEngine.UI` / `EventSystems`）を使い、
  uGUI はソースのパッケージで、その TextMeshPro がエンジンの内部 API に依存するため、Unity の外ではコンパイルできない。
  Yarn 連携は、開発用プロジェクト（Yarn Spinner 3.2.8 入り）での EditMode テストで確かめる（`Integrations/YarnSpinner/Tests`）。
  CI のコア（`Runtime` / `Editor` / `Tests`）のコンパイル確認は、Yarn が無いプロジェクトでもコアが単独でコンパイルできることの確認にもなる

## ADV Starter（ADV のひな形）

`Assets > Create > Visual Node Editor > ADV Starter`（Yarn 連携のエディタ）。選んでいるフォルダに次を作り、グラフを開く（同じ名前があれば番号を付ける）:

- グラフ（`ADV.asset`）。Scene ノードのシーンは未設定（自分のシーンを入れる。検証の Warning と「次の一手」のボタンで案内される）

  ```
  Entry → [Scene: Title] ─(NewGame)→ [Scene: Story] ─(自動)→ [Container: Story] ─(Ending)→ [Scene: Title]
  Story の中: Entry → [Dialogue: Chapter1] → [Dialogue: Chapter2] ─(Done, Affection >= 3)→ [Dialogue: GoodEnd] → Exit (Ending)
                                                                  └(Done)→ [Dialogue: NormalEnd] → Exit (Ending)
  Blackboard: Affection（Int、0）
  ```

- Yarn スクリプト（`ADV.yarn`）: 上の 4 つのノード。Chapter1 の選択肢で `$Affection` を増やす
- Yarn プロジェクト（`ADV.yarnproject`）: そのフォルダ（と親のフォルダ）に Yarn プロジェクトが無ければ作る（Yarn Spinner の `YarnEditorUtility.CreateYarnProject`）。
  あれば、そのプロジェクトをすぐ読み込み直す（Yarn Spinner は新しいスクリプトを次のフレームで入れるので、開いたグラフが「無いノード」の警告を出さないように）
- Yarn のノード名がプロジェクトの中で重なるとコンパイルできないので、既にあれば（2 つ目のひな形など）グラフのファイル名を頭に付ける（例: `ADV1_Chapter1`）。
  `$Affection` は宣言せず、`<<set>>` の使い方から数と決めさせる（宣言が重ならないように）

## Build Settings 連携

- `BuildSettingsSync`（Editor）: グラフが参照するシーンのうち、Build Settings に無い・無効なものを求め、追加・有効化する
  - 既存の並び順は変えず、足りないシーンを末尾に追加する（ビルドでは index 0 のシーンから始まるため、Runner を置くシーンの位置はユーザーが決める）
  - 判定と新しいシーン一覧の組み立ては純粋な関数にし、EditMode テストの対象にする
- 実行方法: グラフウィンドウのツールバー「Add Scenes to Build」、または Project ビューでグラフを選んで `Assets > Visual Node Editor > Add Graph Scenes to Build Settings`
- 削除されたシーンは追加できないので飛ばし、結果のメッセージでその数を知らせる

### ワンクリック Play（ツールバーの「Play」）

「組んだ流れを動かす」までを 1 操作にする（`PlaySetup`）。シーンや Build Settings の並びを変える前には必ず確認する。

1. グラフのシーンを Build Settings に入れる（上と同じ `BuildSettingsSync.Apply`）
2. グラフが始まるシーン（`PlayStart.FindStartScene`: Runner と同じように Entry から最初の待機ノードまでたどり、それが Scene ノードならそのシーン。
   そうでなければ Entry から辿れる最初の Scene ノード）が Build Settings の有効な最初のシーン（ビルドが始まるシーン。無効なものは飛ばす）でなければ、先頭へ移すか尋ねる
   （エディタの Play はどちらでも動くので「並びはそのまま」も選べる。選んだらそのセッションの間は尋ねない）
3. そのシーンを開く（未保存のシーンがあれば Unity の保存確認を出す）。グラフにシーンが無ければ今開いているシーンを使う
4. そのシーンにこのグラフの Graph Runner が無ければ、「Graph Runner (グラフ名)」という GameObject を追加してシーンを保存してよいか尋ねる
5. Play に入る。Play 中はボタンが「Stop」になり、押すと Play を終える

あわせて、Scene ノードを持つグラフなのに有効な Build Settings のシーンのどれにもこのグラフの Graph Runner が無ければ、検証の Warning を出す
（Play しても何も起きないことを先に知らせる）。判定はシーンファイル（テキストの YAML）の Graph Runner のコンポーネントが指すグラフの GUID で行い
（`RunnerUsage`）、結果はファイルの更新日時ごとに覚えて、編集のたびの再検証でシーンを読み直さない。バイナリのシーンなど読めないものがあれば、
誤った警告を出さないよう「使われている」とみなす。プレハブの中の Runner はシーンファイルに書き出されないので、プレハブのインスタンスでグラフを上書きしているか、
インスタンスの元のプレハブ（入れ子を含む）がグラフを参照していれば「使われている」とみなす（シーンに直接置いた Graph Event Button の参照は数えない）。
元のプレハブの依存（`AssetDatabase.GetDependencies` で入れ子までたどる）はプレハブごとに覚え、プロジェクトのアセットが変わったとき（`EditorApplication.projectChanged`）に捨てる。コードから Runner を作るだけのグラフでは警告が出るが、文言で「その場合は無視してよい」と伝える

## Play Mode 中の強調表示

- Play 中、開いているグラフを実行している `GraphRunner` があれば、その現在のノードを強調する（USS クラス `vne-node--running`）
- 遷移のたびに強調を移す。Runner が止まる・Play を終えると強調を消す
- 同じグラフを複数の Runner が動かしている場合は、最初に見つかった Runner を表示する

### Play 中のデバッグ

シーンにボタンを置かなくても、エディタから流れを動かして確かめられるようにする。

- 「Now running」パネル（`RuntimePanel`）: Play 中、インスペクタの上に、今いる場所（入っているコンテナを外側から並べる。例: `Stage › Inner › Boss`）と、
  そこから起こせる操作のボタンを出す。ボタンは、今のノードから出ている Event ノードのイベント名ごとの Raise と、Event 以外へのエッジがあれば
  Advance（行き先のタイトル付き）。どれを出すかは純粋関数 `RuntimeActions.For`。押すとその Runner で実行する
- Blackboard: Play 中は各パラメータの型の欄に、実行中の値を出す（例: `Int = 2`。`ParameterChanged` で更新）。既定値の欄はアセットの値のまま。
  Runner のパラメータは開始時のアセットの写しなので、Play 中に追加・削除・改名したものは、同じ名前と型が Runner にあるときだけ値を出す
  （`GraphRunner.HasParameter(name, type)`。無ければ型名だけ）
- 軌跡: 直近に通ったノード（今いるノードを除いて 4 つ。`RunTrail`）を薄く強調する（USS クラス `vne-node--visited`）。より深い階層のものはそれを含むコンテナに出す。
  新しく実行を見始めたとき・Play を終えたときに消す

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
| `NodeGraphAssetInspector.uss` | アセットのインスペクタ（`NodeGraphAssetEditor`） | 概要・パラメータ一覧・注記（レイアウトは `NodeGraphAssetInspector.uxml`） |

GraphView / Node は自身に既定の USS を持つ。同じ詳細度のルールは要素に近い USS が勝つため、
グリッドやノードのスタイルはウィンドウのルートではなく、その要素自身に USS を付けて確実に効かせる。

カテゴリ色は `.vne-root` に USS 変数（`--vne-category-flow` など）として定義し、ノードとインスペクタの両方から `var()` で参照する。

### レイアウト

```
┌ Toolbar ───────────────────────────────────────────────────────────┐
│ [Save] …                                  [Issues toggle] [asset name *] │
├────────────┬──────────────────────────────┬──────────────────┤
│ node tree  ┃ graph (NodeGraphView)        ┃ inspector        │  ← TwoPaneSplitView の入れ子
│            ┃                              ┃                  │     外（body）: ノードツリー（左ペイン固定・初期 200px）| 内
│            ├──────────────────────────────┤                  │     内（main）: グラフ | インスペクタ（右ペイン固定・初期 300px）
│            │ Root > Stage > …              │                  │     境界はドラッグで変更、幅は view-data-key で保持
├────────────┴──────────────────────────────┴──────────────────┤
│ issue list（問題があり、かつトグルが開いているときだけ表示）                │
└────────────────────────────────────────────────────────────────────┘
```

- ノードツリー・グラフ領域・インスペクタは USS で最小幅を持つ。ウィンドウ自体にも最小サイズを設定し、ツールバーやインスペクタが画面外へ押し出されないようにする
- 階層のパンくず（Root > Stage > …）はグラフの下（ステータスバーの位置）に置く。コンテナの中にいるときだけ出す
- ノードツリーは View メニューの「Node Tree」で出し入れする（既定で表示。状態はウィンドウに保存する）。隠すと左ペインごと畳む
- ツールバー
  - 問題件数は `ToolbarToggle`。押すと問題一覧を開閉する
  - アセット表示は名前のみ（パスはツールチップ）。未保存の変更があれば末尾に ` *`。クリックで Project ビューの該当アセットを Ping する。長い名前は先頭側を省略する
  - 未保存状態はアセット側の変更（Ctrl+S など）でも変わるため、定期的に確認して表示を更新する

### ノードツリー（左のペイン、`NodeTreePanel`）

グラフは 1 階層ずつしか見せないので、コンテナの中まで含めた全体を一覧し、どこへでも 1 クリックで移れるようにする。

- UI Toolkit の `TreeView`。ルートのノードを並べ、コンテナの中のノードをその子にする（入れ子の深さに制限なし）。
  階層の組み立ては純粋関数 `NodeTree.Build`（EditMode テストの対象）。親の参照が壊れたノード（どのコンテナにも入っていない。検証で Error）は出さない
- 各階層の並び: Entry → 位置の順（左から右、同じ列なら上から下）→ コンテナの Exit
- 行: カテゴリ色の丸（インスペクタのアイコンと同じ色）+ 名前（グラフのタイトルと同じ規則。コンテナの Exit は出口の名前）。ツールチップに型の表示名。コンテナは太字
- クリック: そのノードへ移る（`NodeGraphView.FocusNode`。別の階層ならその階層を開き、選択して画面の中央に出す）。
  ダブルクリック・Enter: コンテナならその中を開く（それ以外はクリックと同じ）
- グラフでノードを選ぶと、ツリーでも同じノードを選び、入っているコンテナを開いて見える所まで送る（ツリーからの移動は起こさない）
- 表示中の階層のコンテナを強調する（`vne-node-tree__item--level`）。Play 中は実行中のノード（`--running`）と、それを含むコンテナ（`--running-path`、薄く）を強調する。
  強調するものが変わらなければ表示を作り直さない（Play 中は同じノードが何度も知らされる）
- 作り直すのは、階層・名前・カテゴリが変わったときだけ（インスペクタで入力するたびには作り直さない）。項目の ID はノードごとに固定し、開いた・閉じた状態を保つ。
  別のアセットを開いたら、コンテナをすべて開いた状態から始める。後から足したコンテナ（Undo などで戻ったものも）は開いた状態で出す（ツリーから消えたノードの項目 ID は忘れる）
- 選んだノードの入っているコンテナを開くのは、選択が変わったときだけ（作り直しや同じノードの選び直しでは、ユーザーが閉じたコンテナを開き直さない）
  閉じたコンテナの中の選んだノードは、項目を選ばずに覚えておき（TreeView は選んだ項目の親を開いてしまうため）、そのコンテナが開いたら選び直す

### 最初の一歩（新しいグラフと空のキャンバス）

- `Assets > Create > Visual Node Editor > Node Graph` は Editor の `NodeGraphFactory` が作る（`ProjectWindowUtil.CreateAsset` で名前を入力して保存）。
  作ったグラフには最初から Entry がある（Unity の State Graph の Start と同じく、最初の Error で止まらないように）。Runtime の `[CreateAssetMenu]` は使わない
- 表示中の階層にノードがほとんど無いとき、キャンバスの中央に案内を出す。どの案内かは純粋関数 `EmptyStateHint.For`（EditMode テストの対象）で決める
  - ルートにノードが無い: 「Add Entry」と「Add First Scene」
  - ルートに Entry しか無い: 「Add First Scene」
  - コンテナの中に Entry / Exit しか無い: 「Add Scene」と「Add State」
  - ノードの追加方法（Space / 右クリック）も一緒に出す。付箋・グループはノードに数えない
- ボタンの動き（どれも 1 回の Undo で戻せる）
  - Add Entry: ルートの Entry を作る
  - Add First Scene: Entry の右に Scene ノードを作って Entry と繋ぎ、選択する（インスペクタでシーンを選べる）。Entry が無ければ一緒に作る
  - Add Scene / Add State（コンテナの中）: Entry と最初の出口の Exit ノードの間に作り、Entry → 新しいノード → Exit に繋ぎ直す
- 案内の文言とボタンは UXML（`EmptyCanvasHint.uxml`）に置き、どれを見せるかは USS クラス（`vne-empty-hint--<kind>`）で切り替える。
  ボタン以外はクリックを通す（`picking-mode="Ignore"`）ので、案内が出ていても右クリックや範囲選択はそのまま使える

### イベント名の入力（同じ文字列を 2 回打たせない）

- Event ノードのインスペクタの Event Name 欄の横に「▾」ボタンを置き、グラフで既に使っているイベント名から選べるようにする（新しい名前は欄に入力する）。
  選択肢は `GraphEventNames.Collect`（Graph Event Button のドロップダウンと同じ一覧）
- Event ノードの右クリック → Copy Raise Call: `Raise("イベント名")` をクリップボードにコピーする（コードから送るときに名前を打たない）。
  文字列の組み立ては純粋関数 `EventNameTools.FormatRaiseCall`（`"` と `\` はエスケープ）
- 大文字小文字・前後の空白だけが違う名前と、前後に空白がある名前は検証で警告する（判定は Runtime の `EventNameCheck`。EditMode テストの対象）

### ポートから作って繋ぐ

- ポートからエッジをドラッグして空き地で離すと、ノードの検索を開く。選んだノードをそこに作り、ドラッグ元のポートと繋いで選択する（作成と接続で 1 回の Undo）。
  既存のエッジを端から外して空き地で離したとき（元のエッジは GraphView の既定どおり消える）は、繋がったままの側のポートに繋ぐ。
  GraphView は離すまで外した側の端もエッジに残すことがあるので、両端があれば、その端の `EdgeDragHelper.draggedPort` が自分自身の側を選ぶ（`NodePort.GetDraggedPort`。ShaderGraph と同じ判定）
- 検索には、ドラッグ元と繋げるノードだけを出す（出力からなら入力ポートを持つ型、入力からなら出力ポートを持つ型。表示中の階層の規則も同じ）。
  State・Scene の出力からなら、遷移を作ることが多いので Event を一覧の先頭に出す
- ポートは `NodePort`（GraphView の `Port` のサブクラス）で作り、独自の `IEdgeConnectorListener` を持たせる。ポートの上で離したときの動きは GraphView の既定と同じ
  （容量 1 のポートなら既存のエッジを外してから繋ぐ）。型がどのポートを持つかと、検索に出すかどうかは純粋関数 `ConnectionCandidates` で決める

### シーンアセットのドロップ

- Project ビューのシーンアセット（複数可）をキャンバスへドロップすると、落とした位置から右へ並べて Scene ノードを作り、作ったノードを選択する
  （表示中の階層に作る。1 回の Undo で戻せる）。シーン以外のアセットを含むドラッグでは、シーンだけを使う。シーンが 1 つも無ければ受け付けない
- ドラッグ中のシーンの取り出し（`.unity` だけ・重複を除く・選んだ順）と並べ方は純粋関数 `SceneDrop` にし、EditMode テストの対象にする
- 空のキャンバスの案内にも「Project ビューからシーンをドロップできる」ことを書く

### ノードの表示

- カテゴリ = `[NodeMenu]` パスの先頭セグメント（小文字化）。`vne-node--<category>` クラスを付ける
  - 色付きカテゴリ: `flow` / `state` / `event` / `entity` / `system`。それ以外（`misc` やカテゴリ無し）はグレー
  - 組み込みノードのパス: Entry・Scene = `Flow/…`、State = `State/State`、Event = `Event/Event`、Note = `Misc/Note`
- タイトル領域の上端にカテゴリ色の帯（3px）
- タイトル = ユーザーが入力したタイトル。空（空白のみを含む）なら型の表示名（`[NodeMenu]` パスの末尾、無ければ型名から `Node` を除いたもの）
- Scene ノードは、ユーザーがタイトルを付けていなければシーン名をタイトルにする（シーンも未設定なら「Scene」）。
  同じ「Scene」が並んで見分けが付かない、を無くすため
- Event ノードは遷移として読めるようにする: タイトルを付けていなければイベント名をタイトルにし（イベント名も空なら「Event」）、
  ステートの箱と見分けられるよう、背が低く角の丸い札の形にする（USS クラス `vne-node--transition`。見た目は USS だけで決める）
- 文章の中でノードを呼ぶ名前（`NodeDisplay.GetNodeLabel`。Now running の場所、Group into Container の理由など）も、このタイトルと同じ規則にする
- タイトルの直下にサマリー 1 行。内容は `NodeView.GetSummary()`（virtual）で型ごとに決める。空なら行ごと出さない
  - Event = イベント名（タイトルがイベント名のときは出さない）、Scene = シーン名（タイトルがシーン名のときは出さない）、State = 説明の 1 行目、Note = 本文の 1 行目
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
- 直し方が決まっている問題は、項目の右に直すボタンを出す（行き止まりで止まらないように）。問題の種類は `GraphIssue.Kind`（`GraphIssueKind`）で見分け、
  ボタンの文言は純粋関数 `IssueFixes.GetLabel` で決める
  - Entry が無い（`MissingEntry`）→ Add Entry（ルート階層に戻って Entry を作る）
  - Scene ノードにシーンが無い・シーンが削除された（`SceneNotSet` / `SceneMissing`）→ Pick Scene…（プロジェクトの `Assets` 以下のシーンの一覧と、最後に Create Scene…）
  - シーンが Build Settings で有効でない（`SceneNotInBuildSettings`）→ Add to Build Settings（ツールバーの Add Scenes to Build と同じ）
  - Graph Runner が無い（`NoGraphRunner`）→ Add Graph Runner…（ツールバーの Play と同じ準備をして、Play には入らない）
- 問題の集め方（`GraphValidator` + Build Settings の確認）は `GraphIssues.Collect` にまとめ、ウィンドウとアセットのインスペクタで共有する

### シーンの指定（`SceneReferenceDrawer`）

- シーンの欄（SceneAsset の ObjectField）が空のときは、下に Pick…（`ScenePicker` のシーンの一覧）と Create Scene…（保存先を選んで新しいシーンを作り、欄に入れる）を出す
- Create Scene… は、空のシーン（Camera と Light）を追加で開いて保存し、すぐ閉じる。今開いているシーンは切り替えない。
  未保存の無題のシーンが開いているとき（Unity が追加でシーンを作れない）と Play 中は作らず、理由を出す
- Scene ノードのインスペクタには「読み込み済みのシーンへ戻っても読み直さない（シーンの中の物は前の状態のまま）」ことを常に小さく出す
  （ランタイムは、アクティブなシーンと同じシーンへの遷移では読み込まない。`SceneManagerSceneLoader`）

### Graph Event Button のインスペクタ（`GraphEventButtonEditor`）

- 送り先のグラフが決まっていれば、イベント名をそのグラフの Event ノードの名前からドロップダウンで選ぶ（同じ文字列を打たせない）。
  グラフが無い・Event ノードが無いときは名前を直接入力する。今の値がグラフに無ければ、選択肢に残したまま注意を出す
- 設定の問題は、直し方を添えて HelpBox で出す（グラフ未設定、イベント未選択、グラフに無いイベント名、同じ GameObject にボタンが無い）。
  どれを出すかは純粋関数 `GraphEventButtonHints.Get` にし、EditMode テストの対象にする
- 欄はグラフと操作が変わったときだけ作り直す（名前の入力中に作り直すとフォーカスが外れるため）

### アセットのインスペクタ（`NodeGraphAssetEditor`）

Project ビューでグラフアセットを選んだときの Inspector。Unity 既定のインスペクタは `_nodes` / `_parameters` などの生のリストを編集可能に見せてしまい、
リストの「+」で要素を複製すると内部 ID まで複製される（Blackboard や同期は ID で要素を引くため、別の要素を変更してしまう）。
そのため生のデータは出さず、次だけを表示する（編集はグラフウィンドウで行う）。

- 「Open in Visual Node Editor」ボタン
- 問題の件数（ウィンドウのツールバーと同じ文言・色）
- ノード・エッジ・グループ・付箋・パラメータの数
- パラメータの一覧（名前・型・既定値。読み取り専用）
- グラフウィンドウで編集すると、表示中のインスペクタも更新する（`TrackSerializedObjectValue`）

生のデータは Inspector の Debug モードでは引き続き見えるため、検証でもパラメータ ID の重複を Error にする（最後の防波堤）。
件数・パラメータの表示文字列は純粋な関数（`GraphSummary`）にし、EditMode テストの対象にする。

## MCP サーバー（AI エージェントからグラフを読み書きする）

Claude Code などの AI エージェントが、グラフを読み・組み立て・検証し、Play 中の流れを確かめられるようにする
（[Model Context Protocol](https://modelcontextprotocol.io/)）。範囲はグラフの操作だけで、エディタ全般の操作（任意のメニュー・シーンの編集など）は持たない。

### サーバー（`Editor/Mcp/`、名前空間 `Reiga.VisualNodeEditor.Editor.Mcp`）

- Unity エディタの中で動く HTTP サーバー（MCP の Streamable HTTP）。`http://127.0.0.1:<port>/mcp`（既定のポートは 8790）。外部の実行環境・パッケージは使わない
- **既定では止まっている**（opt-in）。`Edit > Preferences > Visual Node Editor` の「Enable MCP server」で有効にする。
  有効・ポートはユーザーごとの設定（`EditorPrefs`）で、プロジェクトには保存しない。ドメインリロードの前に止め、後に（有効なら）また始める。エディタの終了時に止める
- 安全のため 127.0.0.1 だけで待ち受ける。`Origin` ヘッダーがあれば localhost / 127.0.0.1 のものだけ受け付ける（ブラウザからの DNS リバインディング対策。MCP の仕様どおり）。
  有効にしている間は、同じ PC のどのプロセスからでもグラフを書き換えられることを設定画面に書いておく
- HTTP: `POST /mcp` に JSON-RPC 2.0 のメッセージ 1 つ（または 2025-03-26 以前のクライアントが送るメッセージの配列。応答も配列）。
  応答は `application/json`（SSE は使わない）。応答の要らないもの（通知・クライアントからの応答）だけなら 202。`GET` / `DELETE` は 405。
  セッション（`Mcp-Session-Id`）は使わない。本文は 1 MB まで（超えたら 413）、JSON の入れ子は 64 段まで（読むのが再帰なので、深すぎる本文でエディタごと落ちないように）
- MCP のメソッド: `initialize`（クライアントの `protocolVersion` が 2025-06-18 / 2025-03-26 / 2024-11-05 のどれかならそれを、そうでなければ最新を返す。`capabilities.tools`）、
  `ping`、`tools/list`、`tools/call`。それ以外は JSON-RPC の Method not found。解析できない本文は Parse error
- 受け取りは別スレッドで行い、処理（ツールの実行）はすべてメインスレッドで行う（`EditorApplication.update` で順に実行。AssetDatabase などはメインスレッドでしか使えない）。
  コンパイル中などで 30 秒以内に**始まらなければ**取り消してエラーを返す（何も変えない）。始まった処理は終わるまで待つ
  （保存まで済んだ編集を「失敗」と返すと、エージェントがやり直して同じノードを重ねてしまうため）。
  時間切れのエラーは、要求と同じ `id` の JSON-RPC のエラーとして HTTP 200 で返す（`id` が無い・HTTP のエラーだと、クライアントはどの要求の失敗か分からない）。
  どのメッセージに応答するかは、ふだんの処理と同じ規則（`McpProtocol.NeedsReply`）で決める（ふだん答えるものに時間切れで答えないと、クライアントが待ち続ける）
- 始められなかったとき（ポートが使われているなど。ドメインリロードの直後は前のポートがまだ放されていないことがある）は、1 秒おきに 3 回までやり直す
- JSON は依存を増やさないよう、小さな自前の読み書き（`McpJson`）で扱う。プロトコルの処理（`McpProtocol`）は HTTP と切り離した純粋なクラスにし、EditMode テストの対象にする
- ツールが失敗したとき（グラフが見つからない、置けない階層など）は、JSON-RPC のエラーではなく `isError: true` の結果に、エージェントが直せる理由の文を入れて返す（MCP の仕様どおり）。
  失敗したツールはプロジェクトを変えない（`create_graph` が途中で失敗したら、書いたアセット（Unity が読み込めなかったファイルや .meta も）と、そのために作ったフォルダも消す。書き始める前に失敗したなら、そのパスの物には触らない）

### ツール

グラフはアセットのパス（`Assets/…/Flow.asset`）で、ノードは ID で指す（`get_graph` で分かる）。結果は JSON の文字列（`content` の text）。

| ツール | 内容 |
|---|---|
| `list_graphs` | プロジェクトのグラフ（パス・名前・ノード数） |
| `get_graph` | グラフの中身: ノード（ID・型・名前・親のコンテナ・位置・型ごとの値・ポート）、エッジ、パラメータ、コンテナの出口 |
| `create_graph` | 新しいグラフを作る（Entry 入り。`Assets/Create` と同じ） |
| `add_node` | ノードを作る（型は表示名 `Scene` / `State` / `Event` / `Container` / `Exit` / `Note` / `Entry`、メニューのパス、クラス名のどれでも）。親のコンテナ・位置・タイトル・イベント名・シーン・本文を指定できる。コンテナは中に Entry / Exit も作る（エディタと同じ）。置けない階層なら理由を返す |
| `update_node` | タイトル・イベント名・シーン・説明・本文・位置・トリガーと条件を変える。組み込み以外のノード型（Yarn の Dialogue ノードなど）の値は `fields`（`get_graph` の `fields` と同じ名前） |
| `remove_node` | ノードを消す（コンテナなら中身ごと。コンテナの Entry は単独では消さない。エディタと同じ） |
| `connect` / `disconnect` | エッジを足す・消す。ポートは省略すると `out` → `in`。コンテナの出力は出口の名前でも指せる。階層をまたぐ・無いポート・重複は理由を返す |
| `group_into_container` | 選んだノードをコンテナにまとめる（Group into Container と同じ規則。まとめられなければ理由） |
| `add_parameter` | Blackboard にパラメータ（名前・型・既定値）を足す（条件付き遷移のフラグ。Yarn 連携では Yarn の `$Name` と同じもの） |
| `validate_graph` | 問題の一覧（重要度・種類・メッセージ・ノード）。エディタの問題一覧と同じ |
| `open_graph` | グラフを Visual Node Editor のウィンドウで開く（Computer Use で画面を確かめるとき用） |
| `get_runtime_state` | Play 中、そのグラフを動かしている Runner の今いるノード・場所（`Stage › Play`）・起こせる操作・パラメータの値。Play 中でなければそう返す |
| `send_event` | Play 中、イベントを Raise する（または Advance する）。Now running パネルのボタンと同じ |

- 書き換えるツールは、エディタでの操作と同じく `Undo.RecordObject` で記録し（エディタで Undo できる）、終わったらアセットを保存する（エージェントの変更が保存し忘れで消えないように）
- ツールの呼び出し 1 回を、それだけで 1 つの Undo にする（`Undo.IncrementCurrentGroup`。Unity はマウスやキーの入力でしか Undo を区切らないので、
  区切らないとエージェントの続けての変更やユーザーの直前の操作と 1 回の Ctrl+Z にまとまる）
- 値はすべて確かめてから変える（`update_node` などで一部の値だけが変わったまま残らないように）。位置は有限の数だけ（NaN・無限大はノードが消える）。
  数は JSON の数のほか、数の文字列（`"5"`・`"0.5"`。小数点は `.`）も受け付ける。Int に小数・int の範囲外の数、Float に float の範囲外の数は、丸めずに断る
- `create_graph` は、どの OS でも使えるパス（`: * ? " < > |`・空の名前・`.` / `..`・前後の空白・末尾の `.`・`CON`・`NUL`・`COM0`・`CONIN$` などの予約名（拡張子の前の半角スペースも無視して比べる）を含まない）だけを受け付け、Unity が本当にアセットとフォルダを作ったかを確かめる。
  Windows は末尾の `.` と空白を黙って削るので、そのままでは作ったフォルダの名前が変わり、やり直すたびに増えてしまう
- `create_graph` は、ディスクにはあるが Unity が読み込んでいない（自動更新が切れているなど）ファイル・フォルダのあるパスを断る。
  失敗したときは作ったフォルダと書いたファイルを消すので、そうしたものを自分が作ったものと取り違えて、ユーザーのファイルを消さないように。
  Unity が読み込まない名前（`.` で始まる・`~` で終わる・`cvs`・`.tmp` で終わる）は、パスのどこにあっても、何かを作る前に断る
  （更新しても読み込まれず、作ってしまうと Unity が知らないフォルダになって、失敗したときに消せない）
- ポート・出口を名前で指すときは、まず完全一致、無ければ大文字小文字を区別せずに 1 つだけ当てはまるもの。いくつも当てはまるときは選ばずに理由を返す
- 開いているウィンドウのグラフが書き換わったら、ウィンドウは表示中の階層のまま作り直す（`GraphEdits.Edited` を受け取る）
- 書き換えの処理（`GraphEdits`）とグラフの説明（`GraphDescription`）はアセットだけを相手にするので、EditMode テストの対象にする。
  コンテナの中身（Entry と出口ごとの Exit）の作り方はエディタの `CreateNode` と共有する（`ContainerContents`）

### Claude Code から使う

- リポジトリの `.mcp.json` に `visual-node-editor`（`type: http`、`http://127.0.0.1:8790/mcp`）を置く。Unity 側でサーバーを有効にしてから Claude Code を使う
- 他のプロジェクトでは、同じ設定を自分の `.mcp.json` に書く（README）

## エージェント用スキル（`.claude/skills/`、このリポジトリだけ）

Claude Code のスキル。パッケージには入れない（このリポジトリでの開発・確認用）。

| スキル | 内容 |
|---|---|
| `editor-visual-check` | Computer Use で Unity エディタを操作し、Visual Node Editor の見た目と操作を確かめる（Event の札、ノードツリー、パンくず、問題を直すボタン、シーンの Pick… / Create Scene…、エッジを外して落としたときの繋ぎ先など）。スクリーンショット付きで報告する |
| `ux-scenario-run` | Computer Use でシナリオ A・B（`docs/04-ux-audit.md`）を実機で通して時間を計り、Before / After に記入する |
| `build-flow-with-mcp` | MCP のツールで、説明からゲームの流れのグラフを組み立て、検証まで行う |
| `debug-flow-with-mcp` | MCP のツールで、Play 中の流れ（今いる場所・起こせるイベント・パラメータ）を確かめ、イベントを送って進める |

- Computer Use のスキルは、MCP のツールで準備（グラフを作る・開く）と確認（`get_graph` / `get_runtime_state`）をし、画面の操作だけを Computer Use で行う（クリックの手数を減らし、結果を確実に確かめるため）

## 技術的な注意

- `UnityEditor.Experimental.GraphView` は Experimental だが Unity 6 でも利用可能。将来 UI Toolkit に正式なグラフ API が来たら移行を検討する（docs で提案してから）。
- UXML/USS は `Editor/Resources/VisualNodeEditor/` に置き、`Resources.Load` で読む（パッケージ内でもパス解決が安定するため）。
- Editor から Runtime の private フィールド名（`_nodes`, `_guid` など）を `SerializedProperty` で参照している箇所は、EditMode テストで名前の存在を検証する。
