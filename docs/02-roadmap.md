# 02 - Roadmap

## M0: スキャフォールド（完了）
- [x] UPM パッケージ構成・asmdef
- [x] NodeGraphAsset / NodeData / EdgeData
- [x] 空の GraphView ウィンドウ

## M1: 最小限の編集ができる（完了）
- [x] NodeViewFactory（型 → NodeView の自動登録）
- [x] NodeSearchWindow（右クリックで `[NodeMenu]` から検索・追加）
- [x] ポート定義とエッジの接続・切断をアセットに反映
- [x] Undo/Redo 対応
- [x] ノード移動の保存

## M2: 構成ツールとして使える（完了）
- [x] ノード選択時のインスペクタ（フィールド編集）
- [x] Validation（Entry 唯一性・孤立エッジ検出）と警告表示
- [x] グラフ内グループ（コメントは既存の Note ノードで代用。未決事項参照）
- [x] Scene ノードから SceneAsset を参照（`SceneReference`）

## M3: ゲームから使う
- [ ] `GraphQuery`（遷移先の列挙など）のランタイム API
- [ ] サンプル: シーン遷移テーブルとしての利用
- [ ] 骨組みコード生成（任意）

## 未決事項
- ポートの型システムを持つか（当面は文字列名のみ、型なし）
- Entity / System ノードの粒度と、それが既存ゲームプロジェクトのアーキテクチャとどう対応するか
- Git URL 配布時の `Samples~` の扱い
- コメント機能: 既存の Note ノード（ポート無しのノード）で足りるか、GraphView の `StickyNote`（リサイズ可能な付箋）を別途データ化するか
