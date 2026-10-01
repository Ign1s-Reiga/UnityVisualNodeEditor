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

- `NodeGraphAsset` : `List<NodeData>` を `[SerializeReference]` で、`List<EdgeData>` を `[SerializeField]` で保持。
- `NodeData` : `Id`(GUID 文字列), `Title`, `Position` を持つ抽象基底。ポート定義はサブクラスごとに Editor 側の `NodeView` が決める。
- `EdgeData` : `(FromNodeId, FromPort, ToNodeId, ToPort)` の 4 つ組。ポートは文字列名で識別する。

Position などエディタ専用の情報も Runtime の型に持たせる（UnityEditor API は使わないため問題ない）。
ビルドサイズが気になる段階になったら `#if UNITY_EDITOR` で strip するのではなく、別の EditorData に分離する。

## ノード種別の追加手順

1. `Runtime/Nodes/XxxNode.cs` に `NodeData` のサブクラスを作り、必ず `[Serializable]` を付ける（属性は継承されないため、無いと `[SerializeReference]` で保存されない）
2. `[NodeMenu("Category/Xxx")]` を付ける
3. `Editor/Views/XxxNodeView.cs` に `NodeView` のサブクラスを作り、ポートとフィールド UI を定義する
4. `NodeViewFactory`（未実装）に型→View の対応を登録する（Reflection による自動登録を予定）
5. EditMode テストを追加する

## エディタ ↔ アセットの同期

- 開く: `NodeGraphView.Populate(asset)` が Nodes/Edges から View を生成
- 編集: `graphViewChanged` コールバックで追加・削除・移動を即座にアセットへ反映し、`EditorUtility.SetDirty`
- 保存: 通常の `AssetDatabase.SaveAssets`（ツールバーの Save ボタンは明示保存用）
- Undo: `Undo.RecordObject(asset, ...)` を各変更の前に呼ぶ

## 検証（Validation）

グラフの整合性チェックは Runtime 側の純粋な C# で実装し、EditMode テストの対象にする。
- Entry が 1 つだけ存在する
- 存在しないノード ID を指すエッジがない
- 循環を許可するかはノード種別ごとに決める（State 間は許可、Scene 遷移も許可、Note は接続不可）

## 技術的な注意

- `UnityEditor.Experimental.GraphView` は Experimental だが Unity 6 でも利用可能。将来 UI Toolkit に正式なグラフ API が来たら移行を検討する（docs で提案してから）。
- UXML/USS は `Editor/Resources/VisualNodeEditor/` に置き、`Resources.Load` で読む（パッケージ内でもパス解決が安定するため）。
