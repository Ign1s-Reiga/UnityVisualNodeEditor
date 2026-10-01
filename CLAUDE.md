# UnityVisualNodeEditor

Unity 上で「ゲームの大まかな構成」をノードグラフとして組み立てるためのエディタ拡張。
自前の UPM パッケージ `net.reiga7953.visual-node-editor` として開発し、他の Unity プロジェクト（自作ゲーム）へ取り込めるようにする。

## 目的と非目的

- 目的: シーン遷移・ゲームフロー・ステート・イベント・登場要素（Entity/System）といった**粗い粒度の構成**をノードで可視化・編集し、
  その結果を ScriptableObject として保存して、ランタイムから参照・実行できるようにする。
- 非目的: Bolt/Visual Scripting のような「ロジックを全部ノードで書く」ツールは作らない。ノードはあくまで構成図＋骨組み生成のためのもの。
- 非目的: Python を使う作業は一切行わない（ツール・スクリプト含む）。

## 技術スタック

- Unity 6 (6000.x) / C# 9（file-scoped namespace や `required` など C# 10+ の構文は使わない）/ .NET Standard 2.1
- UI: **UI Toolkit + `UnityEditor.Experimental.GraphView`**（ShaderGraph 系と同じ基盤）
- データ: ScriptableObject ベースのグラフアセット（`NodeGraphAsset`）。ノード・エッジは `[SerializeReference]` で多態シリアライズ
- テスト: Unity Test Framework（EditMode 中心）
- 外部ノードライブラリ（xNode 等）は使わない

## リポジトリ構成

```
.
├── CLAUDE.md
├── Assets/                      # 開発用ホストプロジェクト（サンプル・動作確認用。パッケージ本体は置かない）
├── Packages/
│   ├── manifest.json
│   └── net.reiga7953.visual-node-editor/   # ★ パッケージ本体（embedded package）
│       ├── package.json
│       ├── Runtime/             # ランタイムで参照されるデータ型・実行器（Editor 依存禁止）
│       ├── Editor/              # GraphView ウィンドウ・ノード UI・インスペクタ
│       ├── Tests/Editor/        # EditMode テスト
│       └── Tests/Runtime/       # PlayMode テスト
├── ProjectSettings/
└── docs/                        # 番号付き設計ドキュメント（00-overview, 01-architecture, ...）
```

## 名前空間 / アセンブリ

| asmdef | namespace | 参照可能 |
|---|---|---|
| `Reiga.VisualNodeEditor` (Runtime) | `Reiga.VisualNodeEditor` | UnityEngine のみ |
| `Reiga.VisualNodeEditor.Editor` | `Reiga.VisualNodeEditor.Editor` | Runtime + UnityEditor |
| `Reiga.VisualNodeEditor.Tests.Editor` | `Reiga.VisualNodeEditor.Tests` | 上記 + NUnit |

- Runtime 側に `UnityEditor` / `#if UNITY_EDITOR` を持ち込まない。エディタ専用の情報（ノード座標など）は Runtime の型に持たせてよいが、UnityEditor API は呼ばない。
- 新しいノード種別は Runtime に `NodeData` のサブクラス（`[Serializable]` 必須）、Editor に対応する `NodeView` を追加し、`[NodeMenu("Category/Name")]` 属性で検索メニューに登録する。

## コーディング規約

- C# は `.editorconfig` に従う（4スペース、`private` フィールドは `_camelCase`、`var` は型が明らかな場合のみ）。
- 1 ファイル 1 型。ファイル名は型名と一致させる。
- public API には `///` ドキュメントコメントを付ける（日本語可）。
- Unity のシリアライズ対象フィールドは `[SerializeField] private` を基本とし、public フィールドは使わない。
- `.meta` ファイルは必ずコミットする。新規ファイルを Unity 外で作った場合は、Unity を一度開いて .meta を生成させる。
- 既存の GUID を変える操作（.meta の削除・再生成）は禁止。

## 作業の進め方（エージェント向け）

1. 変更前に `docs/01-architecture.md` を読み、設計と矛盾しないか確認する。設計を変える場合は docs を先に更新する。
2. 大きめの機能はまず骨組み（型・インターフェース・空の View）を一気に出し、その後に肉付けする。細かな往復より「一発で全体像が出る」ことを優先する。
3. エディタ UI の見た目は UXML/USS に分離し、C# 側でスタイルをハードコードしない。
4. テスト: ロジック（グラフの検証・シリアライズ・トポロジカルソートなど）は EditMode テストを書く。GraphView の描画はテスト対象外。
5. コミットメッセージは英語の Conventional Commits（`feat:`, `fix:`, `refactor:`, `docs:`, `test:`）。
6. 不明点は推測で進めず、`docs/02-roadmap.md` の「未決事項」に追記して質問する。

## よく使うコマンド

Unity Editor のパスは環境依存。Windows 側の例:

```powershell
# EditMode テスト（バッチ実行）
& "C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe" -batchmode -projectPath . -runTests -testPlatform EditMode -testResults TestResults\editmode.xml -logFile Logs\test.log
```

WSL2 からは `/mnt/c/...` 経由で同じ exe を呼べる。`<version>` は `ProjectSettings/ProjectVersion.txt` と一致させる。

## 禁止事項

- Python の使用・提案
- `Library/`, `Temp/`, `Logs/`, `obj/`, `UserSettings/` のコミット
- Runtime アセンブリからの UnityEditor 参照
- Experimental GraphView 以外のノードUI基盤への無断変更（変更したい場合は docs で提案）
