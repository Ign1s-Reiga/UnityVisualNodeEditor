# Changelog

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
