# Changelog

## [0.1.0] - Unreleased
- 初期スキャフォールド（NodeGraphAsset / NodeData / EdgeData / GraphView ウィンドウ骨組み）
- M1: 最小限の編集
  - `NodeViewFactory` と `[CustomNodeView]` による型 → View の自動登録
  - `NodeSearchWindow`（右クリック → Create Node / Space キーで `[NodeMenu]` から追加）
  - Entry / Scene / State / Event のポート定義、エッジの接続・切断をアセットへ反映
  - ノード移動の保存、Undo/Redo 対応、ツールバーの Save ボタン
  - `NodeGraphAsset.FindEdge`
