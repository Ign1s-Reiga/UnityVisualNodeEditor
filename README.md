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

## AI エージェントから使う（MCP）

Unity エディタの中で MCP サーバーを動かし、Claude Code などの AI エージェントがグラフを読み・組み立て・検証し、Play 中の流れを確かめられるようにできる。

1. `Edit > Preferences > Visual Node Editor` で **Enable MCP server** をオンにする（既定はオフ。ポートの既定は 8790）
2. Claude Code の `.mcp.json`（このリポジトリには同梱）に次を書く。設定画面にも同じものが出る

```json
{
  "mcpServers": {
    "visual-node-editor": { "type": "http", "url": "http://127.0.0.1:8790/mcp" }
  }
}
```

ツール: `list_graphs` / `get_graph` / `create_graph` / `add_node` / `update_node` / `remove_node` / `connect` / `disconnect` /
`group_into_container` / `validate_graph` / `open_graph` / `get_runtime_state` / `send_event`。
変更はエディタと同じ規則で行われ、Undo でき、保存される。サーバーは 127.0.0.1 だけで待ち受けるが、
有効にしている間は同じ PC のどのプログラムからでもグラフを書き換えられる。

このリポジトリの `.claude/skills/` には、Claude Code 用のスキル（Computer Use でエディタを確かめる `editor-visual-check`、
シナリオを実機で計る `ux-scenario-run`、MCP で流れを組む `build-flow-with-mcp`、Play 中の流れを調べる `debug-flow-with-mcp`）がある。
詳しくは [docs/01-architecture.md](docs/01-architecture.md) の「MCP サーバー」を参照。

## CI

GitHub Actions でパッケージのコンパイルチェックを行う（Unity ライセンス不要）。
仕組み・ローカル実行・Git ブランチからの取り込み方は [docs/03-ci.md](docs/03-ci.md) を参照。
