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

- `NodeGraphAsset` : `List<NodeData>` を `[SerializeReference]` で、`List<EdgeData>` と `List<GroupData>` を `[SerializeField]` で保持。
- `NodeData` : `Id`(GUID 文字列), `Title`, `Position` を持つ抽象基底。ポート定義はサブクラスごとに Editor 側の `NodeView` が決める。
  `Id` / `Position` は `[HideInInspector]`（インスペクタには出さない）。
- `EdgeData` : `(FromNodeId, FromPort, ToNodeId, ToPort)` の 4 つ組。ポートは文字列名で識別する。
- `GroupData` : `Id`, `Title`, `Position` と、所属ノードの ID リスト。ノードは高々 1 つのグループに属する。ノード削除時は全グループから ID を外す。
- `SceneReference` : シーンの `Guid` と `Path`（`Name` は Path から導出）。Runtime は `SceneAsset` 型に触れず文字列だけを持つ。
  `SceneAsset` との相互変換は Editor の `SceneReferenceDrawer` が行い、シーンの移動・改名は GUID から Path を引き直して追従する。

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
  - `elementsToRemove` → `NodeView` / `Edge` / `GroupView` に対応するデータを削除
  - `movedElements` → `NodeData.Position` / `GroupData.Position` を更新
  - データの特定は常に ID（エッジはポート対の 4 つ組）で行い、View が保持するインスタンスの同一性には頼らない
    （`SerializedObject` 経由の編集後にインスタンスが差し替わっても壊れないようにするため）
- グループ: 右クリック → Create Group で選択中のノードを囲む。`elementsAddedToGroup` / `elementsRemovedFromGroup` / `groupTitleChanged` で同期
- 保存: 通常の `AssetDatabase.SaveAssets`（ツールバーの Save ボタンは `AssetDatabase.SaveAssetIfDirty` で明示保存）
- Undo: `Undo.RecordObject(asset, ...)` を各変更の前に呼ぶ。`Undo.undoRedoPerformed` でビューを `Populate` し直す

## インスペクタ

- ウィンドウ右側のパネル（`NodeInspectorView`）。ノードを 1 つだけ選択しているときに、その `NodeData` を `SerializedObject` 越しに `PropertyField` で表示する
- 編集は `SerializedObject` のバインディングが適用するので Undo も自動で効く。変更を検知したらノードのタイトル表示と検証結果を更新する

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

エディタはグラフが変わるたびに再検証し、該当ノードの枠を色分け（USS クラス `vne-node--error` / `vne-node--warning`、ツールチップにメッセージ）し、
ウィンドウ下部の一覧に表示する。一覧の項目を選ぶと該当ノードを選択してフォーカスする。

## 技術的な注意

- `UnityEditor.Experimental.GraphView` は Experimental だが Unity 6 でも利用可能。将来 UI Toolkit に正式なグラフ API が来たら移行を検討する（docs で提案してから）。
- UXML/USS は `Editor/Resources/VisualNodeEditor/` に置き、`Resources.Load` で読む（パッケージ内でもパス解決が安定するため）。
- Editor から Runtime の private フィールド名（`_nodes`, `_guid` など）を `SerializedProperty` で参照している箇所は、EditMode テストで名前の存在を検証する。
