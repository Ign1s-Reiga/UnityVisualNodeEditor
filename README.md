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

## ADV を作る（Yarn Spinner 連携）

会話の中身は [Yarn Spinner](https://yarnspinner.dev/) に書き、グラフはチャプター・ルート・フラグ・セーブを受け持つ。

1. ゲームのプロジェクトに Yarn Spinner for Unity 3.x（`dev.yarnspinner.unity`）を入れる。入れると、このパッケージの Yarn Spinner 連携
   （`Integrations/YarnSpinner`）が有効になる（入れていなければコンパイルされず、影響しない）
2. `Assets > Create > Visual Node Editor > ADV Starter` で、ひな形（グラフ・Yarn スクリプト・Yarn プロジェクト・Title と Story のシーン）を作る
   - `ADV Title`: 「New Game」（Raise `NewGame`）と「Continue」（スロット `auto` から Load）のボタンと、このグラフの Graph Runner
   - `ADV Story`: Yarn Spinner の Dialogue System（Yarn Project・Yarn Graph Bridge・Yarn の変数を Blackboard と共有する Graph Variable Storage 入り。オートセーブはスロット `auto`）
3. グラフのツールバーの Play で動かす（シーンを Build Settings に入れ、Title を先頭に置くかを尋ねてから Play に入る）

自分のシーンで組むときは、Dialogue Runner と同じ GameObject に `Add Component > Visual Node Editor > Yarn Graph Bridge` を足す
（Graph Variable Storage も自動で設定される）。タイトルのボタンには Graph Event Button を付ける。

| グラフでできること | 使うもの |
|---|---|
| Yarn のノードを流れの中で再生する | `ADV/Dialogue` ノード（会話が終わると完了イベント `Done` → 無ければ次のノードへ） |
| フラグでルートを分ける | Event ノードの条件（`Affection >= 3` など。同じ名前の Event を条件違いで並べる） |
| フラグを満たしたら自動で進む | Event ノードのトリガー Automatic |
| Yarn からグラフを動かす | Yarn のコマンド `<<graph_event 名前>>`（すぐ）/ `<<graph_next 名前>>`（会話が終わったら） |
| フラグを Yarn と共有する | Blackboard のパラメータ `Name` = Yarn の `$Name` |
| セーブ・ロード | Graph Event Button の Save / Load、`GraphRunnerBehaviour.SaveSlot` / `LoadSlot`、`GraphSaveSlots` |

Yarn はノードの途中から再開できないので、会話の途中でセーブすると、ロードしたときにその会話の最初から（入る前のフラグで）やり直す。
既読管理・バックログ・オート / スキップ・立ち絵などは Yarn Spinner の Presenter とゲーム側で行う。
詳しくは [docs/01-architecture.md](docs/01-architecture.md) の「条件付き遷移」「セーブ・ロード」「Yarn Spinner 連携（任意）」を参照。

## スネーク・パズルを作る（ゲームの Starter）

遊び（蛇の動き・タイルの入れ替え）は C# に書き、タイトル・プレイ中・ポーズ・ゲームオーバー・クリア・レベル・スコアはグラフで扱う。

1. `Assets > Create > Visual Node Editor > Snake Starter`（スネークゲーム）か `Sliding Puzzle Starter`（15 パズル。3 × 3 → 4 × 4 → 5 × 5）で、
   ひな形（グラフ・Title と Game のシーン・ゲームのスクリプト）を作る。スクリプトはコンパイルが終わると Game のシーンの盤に自動で付く
2. グラフのツールバーの Play で動かす（Snake: Space で始め、矢印キー / WASD で動かし、Esc でポーズ。パズル: 隙間の隣のタイルをクリックか矢印キー）
3. 書き出したスクリプト（`SnakeGame.cs` など）を書き換えて、自分のゲームにする

ゲームのスクリプトを書かずに、どのゲームでも使えるもの:

| やりたいこと | 使うもの |
|---|---|
| ポーズ・ゲームオーバーのパネルを、そのノードにいる間だけ出す | `Graph Node Activator`（コンテナを選べば、その中のどこかにいる間） |
| スコアなどのパラメータを画面に出す | `Graph Parameter Text`（`Score: {0}` のような書式。uGUI の Text・TextMeshPro） |
| キーでイベントを送る（Esc → Pause） | `Graph Event Key`（旧 Input Manager でも Input System でも動く。コードからは `GraphInput.GetKeyDown`） |
| ノードに入ったらパラメータを変える（`Score = 0`・`Level += 1`） | State・Scene ノードの振る舞い `Set Parameter` |
| ポーズ中は時間を止める | 振る舞い `Time Scale`（ノードにいる間だけ `Time.timeScale` を変える） |

コンテナは何段でも入れ子にできる（スライドパズルの Starter は Levels › Level の 2 段）。
詳しくは [docs/01-architecture.md](docs/01-architecture.md) の「ゲーム向けのコンポーネント」「組み込みの振る舞い」「ゲームの Starter」を参照。

## UI Toolkit で画面を作る（UI Builder）

ランタイムの UI Toolkit（UIDocument・UXML・USS）でも、コードを書かずに画面をグラフに繋げられる。画面の配置は Unity の UI Builder で行う。

- 画面（タイトル・設定・ポーズ・HUD）は Unity のシーンではなく、グラフのノードに付ける。State・Scene・Container ノードのインスペクタの **Screen** に UXML を入れる。
  空なら **Create Screen…** で、グラフの隣に `<グラフ名> <ノード名>.uxml` を作って入れ、UI Builder で開く。入っていれば **Edit Screen** で開く
- Graph Runner に付けた **Graph Screen Host**（UIDocument と一緒に付ける）が、今の場所の画面を出す。入っているコンテナの画面の上に、今のノードの画面を重ねる
  （例: コンテナ `Round` の HUD の上に、`Paused` のポーズ画面）。ツールバーの Play で Graph Runner を置くとき、ノードに画面があれば一緒に付く
- タイトル・設定のように画面だけが替わるところは State ノードにすればよく、シーンを分けなくてよい
- UI Builder の Library に、グラフに繋がるコントロールが出る:

  | コントロール | すること（uGUI のコンポーネント版） |
  |---|---|
  | `GraphButton` | クリックで Raise / Advance / Save / Load（Graph Event Button） |
  | `GraphParameterLabel` | パラメータを `Score: {0}` のような書式で出す（Graph Parameter Text） |
  | `GraphNodePanel` | グラフが指定したノード（`Paused`・`Round/Paused`、コンテナなら中のどこか）にいる間だけ、中の要素を出す（Graph Node Activator）。画面の一部だけを出し分けるときに使う |

  `graph` 属性を入れると、イベント名・パラメータ・ノードを UI Builder のインスペクタでグラフから選べる（空なら実行中のグラフを使う）
- `Snake Starter (UI Toolkit)` / `Sliding Puzzle Starter (UI Toolkit)` は、1 つのシーンで、Title・Game・ポーズなどの画面をノードに付けた Starter（遊びとキーは uGUI 版と同じ）

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
`group_into_container` / `add_parameter` / `validate_graph` / `open_graph` / `get_runtime_state` / `send_event`。
変更はエディタと同じ規則で行われ、Undo でき、保存される。サーバーは 127.0.0.1 だけで待ち受けるが、
有効にしている間は同じ PC のどのプログラムからでもグラフを書き換えられる。

このリポジトリの `.claude/skills/` には、Claude Code 用のスキル（Computer Use でエディタを確かめる `editor-visual-check`、
シナリオを実機で計る `ux-scenario-run`、MCP で流れを組む `build-flow-with-mcp`、Play 中の流れを調べる `debug-flow-with-mcp`）がある。
詳しくは [docs/01-architecture.md](docs/01-architecture.md) の「MCP サーバー」を参照。

## CI

GitHub Actions でパッケージのコンパイルチェックを行う（Unity ライセンス不要）。
仕組み・ローカル実行・Git ブランチからの取り込み方は [docs/03-ci.md](docs/03-ci.md) を参照。
