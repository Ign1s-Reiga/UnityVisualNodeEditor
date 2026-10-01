# 00 - Overview

## 何を作るか

Unity 上で「ゲームの大まかな構成」をノードグラフとして描き、保存し、ランタイムから参照できるようにするエディタ拡張。
ShaderGraph のような見た目で、以下のような粒度のものを配置・接続する。

- Entry（開始点）
- Scene / Stage（Unity シーン単位）
- State（タイトル・プレイ中・ポーズ・リザルト など）
- Event（ステート遷移や演出のトリガー）
- Note（メモ）

将来的には Entity / System / Data などの「登場要素」ノードを追加し、
グラフから C# の骨組みコード（空の MonoBehaviour / ScriptableObject）を生成することも視野に入れる。

## 何を作らないか

- ロジックをノードで記述するビジュアルスクリプティング（Bolt 相当）
- ランタイムでの動的なグラフ編集

## 利用イメージ

1. `Assets > Create > Visual Node Editor > Node Graph` でアセットを作る
2. ダブルクリックでエディタウィンドウが開く
3. 右クリック → 検索メニューからノードを追加し、ポートをドラッグして接続
4. 保存すると `NodeGraphAsset` に書き戻される
5. ゲーム側は `NodeGraphAsset` を参照し、シーン遷移テーブルやステートマシンの初期化に使う

## 配布形態

UPM パッケージ `net.reiga7953.visual-node-editor`（このリポジトリに embedded）。
自作ゲーム（Unity 6）から `file:` または Git URL で参照する。
