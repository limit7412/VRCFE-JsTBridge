# Kx VRC FE-JsT Bridge

FaceEmoで表情を管理しているアバターに、Jerry's Templates (MA版) のフェイストラッキングを併用するためのNDMFプラグインです。
フェイストラッキングが有効な間はFaceEmoが表情を書き込まないようにし、無効化したら元の動作へ戻します。

## 解決する問題

FaceEmoとJerry's Templatesを同じアバターに載せると、フェイストラッキング中もFaceEmoがデフォルト表情、表情再生、まばたき、口モーフキャンセラを書き込み続けます。
トラッキング由来のブレンドシェイプと競合するため、表情が崩れたり、目が固まったりします。

FaceEmoには外部連携用のバイパス機構と、表情の固定やまばたきの停止を外から起こすパラメータがあり、Jerry's Templatesはトラッキングの状態をパラメータとして公開しています。
このプラグインは両者をつなぐアニメーターレイヤーをビルド時に生成します。
どちらのパッケージのアセットにも手を入れないため、Jerry's Templatesの「テンプレートのアニメーターを改変しない」という運用と、FaceEmoの再生成を妨げません。

## 必要環境

- Unity 2022.3
- VRChat SDK Avatars 3.7.0 以降
- NDMF 1.6.0 以降
- Modular Avatar 1.10.0 以降

Jerry's TemplatesとFaceEmoは依存に含めていません。
どちらも配布経路が別で、未導入でもビルドは壊れません。

## インストール

1. [VPMリポジトリ](https://limit7412.github.io/vcc-vpm/)をVCC/ALCOMへ追加する
2. プロジェクトへ「Kx VRC FE-JsT Bridge」を追加する

リリースを公開するとリスティングが自動で作り直されるため、更新はVCC/ALCOMの一覧に出ます。

zipからの手動インストールとbooth版（unitypackage）の手順、および更新の通知と自己更新の仕組みは[インストールと更新](Documentation~/install.md)にまとめています。

## 使い方

1. アバタールートに **Kx VRC FE-JsT Bridge** コンポーネントを追加する
2. アップロード時にブリッジ用のアニメーターレイヤーが自動生成される

新しく追加したコンポーネントは **Expression Control** で動きます。
この方式はFaceEmoの「除外するブレンドシェイプ」の設定を前提にするため、[制御方式](Documentation~/control-method.md)を読んでから使ってください。

すでにコンポーネントを追加してあるアバターは、パッケージを更新しても **Bypass** のままです。
方式を変えるときはインスペクタで選び直してください。

コンポーネントはビルド中に取り除かれるため、アップロード後のアバターには残りません。
インスペクタの表示は日本語と英語に対応しており、インスペクタ上部の言語切替 (NDMFの言語設定) で切り替わります。
Jerry's TemplatesかFaceEmoがアバターに載っていない場合は、NDMFのエラーレポートに警告が出ます。
このとき生成は続行しますが、ブリッジは何もしません。

## 設定項目

| 項目 | 既定値 | 説明 |
|---|---|---|
| Control Method | Expression Control | FaceEmoの書き込みを止める方式。[制御方式](Documentation~/control-method.md)を参照 |
| Face Emote Index | 0 | 表情制御方式で切り替え先にする表情の番号。FaceEmoの「表情選択」メニューが書き込む番号と同じ |
| Bypass Trigger | Facial Expressions Disabled | 発動条件。`Facial Expressions Disabled` は目か口のどちらかが有効なら発動し、`Lip Tracking Only` は口が有効なときだけ発動する |
| Enable Tracking Reapply | 有効 | Tracking Controlを再適用するレイヤーを生成するか |
| Reapply Delay Seconds | 0.2 | バイパスの成立を待つ秒数。0.05 から 1.0 |
| FX Layers To Remove | (空) | ビルド時に FX から取り除くレイヤーの名前。[素体の表情レイヤーの扱い](Documentation~/fx-layers.md)を参照 |

`Lip Tracking Only` は実験的な設定です。
目だけをトラッキングする構成ではFaceEmoのまばたきとデフォルト表情が目系シェイプと競合するため、この設定でも完全には解決しません。

`Reapply Delay Seconds` は、フェイストラッキングを有効化してもMouthのTracking Controlが追従しないときに増やします。
待ち時間はアニメーションの正規化時間で計るため、極端に低いフレームレートでは既定値では足りないことがあります。

## ドキュメント

- [インストールと更新](Documentation~/install.md): 手動インストール、booth版の取り込み、更新の通知と自己更新
- [制御方式](Documentation~/control-method.md): Expression ControlとBypassの違い、ビルド時に生成されるレイヤー
- [素体の表情レイヤーの扱い](Documentation~/fx-layers.md): バイパス方式で表に出る素体の表情レイヤーを、FX Layers To Removeで取り除く方法

## 既知の制限

- 目だけをトラッキングして表情はFaceEmoに任せる構成には対応していません
- フェイストラッキングを有効化した瞬間、パラメータの連鎖が終わるまでの数フレームはFaceEmoの表情が残ります
- VRCFury版のJerry's Templatesプレハブには対応していません
- 表情制御方式は、FaceEmoの「除外するブレンドシェイプ」の設定に依存します。ブリッジ側でFaceEmoのアニメーションを書き換えることはしません
- 表情制御方式では、フェイストラッキング中にFaceEmoの表情固定とまばたきOFFを書き換えます。装着者がメニューで設定していた場合は上書きされます (どちらもワールド移動をまたいで保存されない設定です)

## 仕様

設計の詳細と、依存先の内部名についての前提は [issue #2](https://github.com/limit7412/VRCFE-JsTBridge/issues/2) にまとめています。

## ライセンス

[MIT License](LICENSE)
