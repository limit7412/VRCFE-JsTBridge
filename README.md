# Kx VRC FE-JsT Bridge

FaceEmoで表情を管理しているアバターに、Jerry's Templates (MA版) のフェイストラッキングを併用するためのNDMFプラグインです。
フェイストラッキングが有効な間はFaceEmoが表情を書き込まないようにし、無効化したら元の動作へ戻します。

## 解決する問題

FaceEmoとJerry's Templatesを同じアバターに載せると、フェイストラッキング中もFaceEmoがデフォルト表情、表情再生、まばたき、口モーフキャンセラを書き込み続けます。
トラッキング由来のブレンドシェイプと競合するため、表情が崩れたり、目が固まったりします。

FaceEmoには表情の固定やまばたきの停止を外から起こすパラメータがあり、Jerry's Templatesはトラッキングの状態をパラメータとして公開しています。
このプラグインは両者をつなぐアニメーターレイヤーをビルド時に生成します。
どちらのパッケージのアセットにも手を入れないため、Jerry's Templatesの「テンプレートのアニメーターを改変しない」という運用と、FaceEmoの再生成を妨げません。

## 必要環境

- Unity 2022.3
- VRChat SDK Avatars 3.7.0 以降
- NDMF 1.6.0 以降
- Modular Avatar 1.12.0 以降

Jerry's TemplatesとFaceEmoは依存に含めていません。
どちらも配布経路が別で、未導入でもビルドは壊れません。

## インストール

1. [VPMリポジトリ](https://limit7412.github.io/vcc-vpm/)をVCC/ALCOMへ追加する
2. プロジェクトへ「Kx VRC FE-JsT Bridge」を追加する

リリースを公開するとリスティングが自動で作り直されるため、更新はVCC/ALCOMの一覧に出ます。

zipからの手動インストールとbooth版（unitypackage）の手順、および更新の通知と自己更新の仕組みは[インストールと更新](Documentation~/install.md)にまとめています。

## 使い方

1. ヒエラルキーでアバターを右クリックし、「Kx VRC FE-JsT Bridge」の `Kx FE-JsT Bridge` を選ぶ。アバター直下に、コンポーネントを付けたオブジェクトができる
2. アップロード時にブリッジ用のアニメーターレイヤーが自動生成される

コンポーネントはアバター内のどのオブジェクトに置いても動きます。
既存のオブジェクト（アバタールートを含む）へ「Add Component」で付けてもかまいません。
1つのアバターに置けるコンポーネントは1つだけで、2つ目を置くと自動で取り除かれます。

アバタールートに直接付けたコンポーネントは、ヒエラルキーでアバターを右クリックし、「Kx VRC FE-JsT Bridge」の `Move Bridge out of Avatar Root` を選ぶと専用のオブジェクトへ移せます。
コンポーネントのInspectorの右上のメニューにある `Move to Separate Object` でも同じことができます。
移した後も設定値はそのまま残り、Undoを1回行えば移す前の状態に戻ります。

ブリッジはFaceEmoを動かしたまま、表情固定とまばたきOFFで表情を止めます。
この制御はFaceEmoの「除外するブレンドシェイプ」の設定を前提にするため、[表情の制御](Documentation~/control-method.md)を読んでから使ってください。

以前の版にあった **Bypass** 方式は廃止しました。
Bypassで組んでいたアバターも、更新後は同じ制御で動くため、「除外するブレンドシェイプ」を設定してからビルドしてください。

コンポーネントはビルド中に取り除かれるため、アップロード後のアバターには残りません。
インスペクタの表示は日本語と英語に対応しており、インスペクタ上部の言語切替 (NDMFの言語設定) で切り替わります。
Jerry's TemplatesかFaceEmoがアバターに載っていない場合は、NDMFのエラーレポートに警告が出ます。
このとき生成は続行しますが、ブリッジは何もしません。

## 設定項目

| 項目 | 既定値 | 説明 |
|---|---|---|
| Face Emote Index | 0 | フェイストラッキング中に切り替え先にする表情の番号。FaceEmoの「表情選択」メニューが書き込む番号と同じ |
| Bypass Trigger | Facial Expressions Disabled | 発動条件。`Facial Expressions Disabled` は目か口のどちらかが有効なら発動し、`Lip Tracking Only` は口が有効なときだけ発動する |
| Enable Tracking Reapply | 有効 | Tracking Controlを再適用するレイヤーを生成するか |
| Reapply Delay Seconds | 0.2 | FaceEmo側のTracking Controlの適用を待つ秒数。0.05 から 1.0 |
| Extra Parameters | (空) | フェイストラッキング中だけ値を書き込むパラメータ。MA Menu Itemかパラメータ名で指定する。[追加で書き込むパラメータ](Documentation~/extra-parameters.md)を参照 |
| FX Layers To Remove | (空) | ビルド時に FX から取り除くレイヤーの名前。[素体の表情レイヤーの扱い](Documentation~/fx-layers.md)を参照 |

`Lip Tracking Only` は実験的な設定です。
目だけをトラッキングする構成ではFaceEmoのまばたきとデフォルト表情が目系シェイプと競合するため、この設定でも完全には解決しません。

`Reapply Delay Seconds` は、フェイストラッキングを有効化してもMouthのTracking Controlが追従しないときに増やします。
待ち時間はアニメーションの正規化時間で計るため、極端に低いフレームレートでは既定値では足りないことがあります。

## ドキュメント

- [インストールと更新](Documentation~/install.md): 手動インストール、booth版の取り込み、更新の通知と自己更新
- [表情の制御](Documentation~/control-method.md): フェイストラッキング中の表情の止め方と前提、ビルド時に生成されるレイヤー
- [追加で書き込むパラメータ](Documentation~/extra-parameters.md): フェイストラッキング中だけMAのトグルなどを切り替える方法
- [素体の表情レイヤーの扱い](Documentation~/fx-layers.md): フェイストラッキングと競合する素体のレイヤーを、FX Layers To Removeで取り除く方法

## 既知の制限

- 目だけをトラッキングして表情はFaceEmoに任せる構成には対応していません
- フェイストラッキングを有効化した瞬間、パラメータの連鎖が終わるまでの数フレームはFaceEmoの表情が残ります
- VRCFury版のJerry's Templatesプレハブには対応していません
- ブリッジは、FaceEmoの「除外するブレンドシェイプ」の設定に依存します。ブリッジ側でFaceEmoのアニメーションを書き換えることはしません
- フェイストラッキング中はFaceEmoの表情固定とまばたきOFFを書き換えます。装着者がメニューで設定していた場合は上書きされます (どちらもワールド移動をまたいで保存されない設定です)

## 仕様

設計の詳細と、依存先の内部名についての前提は [issue #2](https://github.com/limit7412/VRCFE-JsTBridge/issues/2) にまとめています。

## ライセンス

[MIT License](LICENSE)
