# UnityVisualNodeEditor

Unity 6 向けの、ゲームの大まかな構成（シーン遷移・ゲームフロー・ステート・イベント・登場要素）をノードグラフで組み立てるエディタ拡張。
UPM パッケージ `net.reiga7953.visual-node-editor` として開発しています。

## 構成

- `Packages/net.reiga7953.visual-node-editor/` — パッケージ本体（embedded package）
- `Assets/` — 開発・動作確認用のホストプロジェクト
- `docs/` — 設計ドキュメント

## 他プロジェクトへの導入

`Packages/manifest.json` に以下を追加:

```json
"net.reiga7953.visual-node-editor": "file:../../UnityVisualNodeEditor/Packages/net.reiga7953.visual-node-editor"
```

（または Git URL: `https://github.com/Ign1s-Reiga/UnityVisualNodeEditor.git?path=Packages/net.reiga7953.visual-node-editor#main`。`#` の後ろはブランチ名・コミットに変更可）

## 開発

1. Unity Hub でこのフォルダを追加して開く（Unity 6000.x）
2. `Window > Visual Node Editor` でエディタウィンドウを開く
3. テストは `Window > General > Test Runner` の EditMode

詳細は [CLAUDE.md](CLAUDE.md) と [docs/](docs/) を参照。

## CI

GitHub Actions でパッケージのコンパイルチェックを行う（Unity ライセンス不要）。
仕組み・ローカル実行・Git ブランチからの取り込み方は [docs/03-ci.md](docs/03-ci.md) を参照。
