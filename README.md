# Kx VRC FE-JsT Bridge

FaceEmoで表情を管理しているアバターに、Jerry's Templates (MA版) のフェイストラッキングを併用するためのNDMFプラグインです。
フェイストラッキングが有効な間はFaceEmoを自動でバイパスさせ、無効化したら元の動作へ戻します。

## 解決する問題

FaceEmoとJerry's Templatesを同じアバターに載せると、フェイストラッキング中もFaceEmoがデフォルト表情、表情再生、まばたき、口モーフキャンセラを書き込み続けます。
トラッキング由来のブレンドシェイプと競合するため、表情が崩れたり、目が固まったりします。

FaceEmoには外部連携用のバイパス機構があり、Jerry's Templatesはトラッキングの状態をパラメータとして公開しています。
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

### VCC/ALCOM経由（推奨）

1. [VPMリポジトリ](https://limit7412.github.io/vcc-vpm/)をVCC/ALCOMへ追加する
2. プロジェクトへ「Kx VRC FE-JsT Bridge」を追加する

リリースを公開するとリスティングが自動で作り直されるため、更新はVCC/ALCOMの一覧に出ます。

### 手動インストール

1. [Releases](https://github.com/limit7412/VRCFE-JsTBridge/releases)からzipファイルをダウンロードする
2. VCCのプロジェクト管理画面で「Add Package」から「Add from Archive」を選ぶ
3. ダウンロードしたzipファイルを選ぶ

この方法ではVCC/ALCOMが更新を検知しないため、新しいバージョンは手で入れ替えます。

## 使い方

1. アバタールートに **Kx VRC FE-JsT Bridge** コンポーネントを追加する
2. アップロード時にブリッジ用のアニメーターレイヤーが自動生成される

コンポーネントはビルド中に取り除かれるため、アップロード後のアバターには残りません。
インスペクタの表示は日本語と英語に対応しており、インスペクタ上部の言語切替 (NDMFの言語設定) で切り替わります。
Jerry's TemplatesかFaceEmoがアバターに載っていない場合は、NDMFのエラーレポートに警告が出ます。
このとき生成は続行しますが、ブリッジは何もしません。

## 設定項目

| 項目 | 既定値 | 説明 |
|---|---|---|
| Bypass Trigger | Facial Expressions Disabled | バイパスの発動条件。`Facial Expressions Disabled` は目か口のどちらかが有効なら発動し、`Lip Tracking Only` は口が有効なときだけ発動する |
| Enable Tracking Reapply | 有効 | Tracking Controlを再適用するレイヤーを生成するか |
| Reapply Delay Seconds | 0.2 | バイパスの成立を待つ秒数。0.05 から 1.0 |
| FX Layers To Remove | (空) | ビルド時に FX から取り除くレイヤーの名前 |

`Lip Tracking Only` は実験的な設定です。
目だけをトラッキングする構成ではFaceEmoのまばたきとデフォルト表情が目系シェイプと競合するため、この設定でも完全には解決しません。

`Reapply Delay Seconds` は、フェイストラッキングを有効化してもMouthのTracking Controlが追従しないときに増やします。
待ち時間はアニメーションの正規化時間で計るため、極端に低いフレームレートでは既定値では足りないことがあります。

## 素体の表情レイヤーの扱い

FaceEmo をバイパスすると、FaceEmo はブレンドシェイプの書き込みを止めます。
このとき、FaceEmo より前にあるアバター素体の表情レイヤーが表に出てきます。
ジェスチャーで表情が変わる、まばたきが復活する、Tracking Control が切り替わる、といった形で現れます。

通常時は後ろにいる FaceEmo が上書きするため表に出ませんが、バイパスするとその前提が崩れます。
ダンスギミックなど他のバイパス経路でも同じことが起きるため、FaceEmo は本来、素体の表情レイヤーを削除したうえで使うものです。

**FX Layers To Remove** にレイヤー名を並べると、ビルド時にそのレイヤーを FX から取り除きます。
アバター素体のアセットは書き換わりません。

どのレイヤーを指定すればよいかは、インスペクタの **除去候補を調べる** ボタンで分かります。
アバターの FX を解析し、競合するレイヤーを根拠つきで挙げます。

- **ブレンドシェイプの重なり**: FaceEmo と Jerry's Templates が書くブレンドシェイプを集め、それと同じものを書くレイヤーを競合とみなします。服のトグルのようにブレンドシェイプを使うだけのレイヤーは候補になりません
- **Tracking Control の切り替え**: Eyes か Mouth を切り替えるレイヤーは、再適用レイヤーと競合します

候補は **追加** ボタンでそのまま一覧へ入ります。
FaceEmo や Modular Avatar が生成するレイヤーは、誤って消さないよう候補から外します。

解析するのはアバター自身の FX です。
Merge Animator であとからマージされるレイヤーは除去できないため、対象に含めません。

取り除けるのは素体の FX にあるレイヤーだけです。
Merge Animator であとからマージされるレイヤーは対象外で、指定しても見つからない旨の警告が出ます。

## 生成されるもの

アバタールートの下に `FEJsTBridge` という空のオブジェクトを作り、MA Merge AnimatorでFXへ次の2レイヤーを追加します。

- **BypassBridge**: トラッキングの状態をFaceEmoのバイパス用パラメータへ写す
- **TrackingReapply**: バイパスの成立後に、Tracking ControlをJerry's Templatesの状態へ合わせ直す

TrackingReapplyが要るのは、JerryとFaceEmoがどちらもステート突入時にTracking Controlを一度だけ適用するためです。
バイパスはパラメータの連鎖で成立するぶんFaceEmo側の適用が数フレーム遅れ、Jerryの適用を上書きしてしまいます。

## 既知の制限

- 目だけをトラッキングして表情はFaceEmoに任せる構成には対応していません
- フェイストラッキングを有効化した瞬間、パラメータの連鎖が終わるまでの数フレームはFaceEmoの表情が残ります
- VRCFury版のJerry's Templatesプレハブには対応していません

## 仕様

設計の詳細と、依存先の内部名についての前提は [issue #2](https://github.com/limit7412/VRCFE-JsTBridge/issues/2) にまとめています。

## ライセンス

[MIT License](LICENSE)
