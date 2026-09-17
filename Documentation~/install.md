# インストールと更新

Kx VRC FE-JsT Bridgeの入れ方は3通りあり、どの経路で入れたかによって更新の手段が変わります。

## VCC/ALCOM経由（推奨）

1. [VPMリポジトリ](https://limit7412.github.io/vcc-vpm/)をVCC/ALCOMへ追加する
2. プロジェクトへ「Kx VRC FE-JsT Bridge」を追加する

リリースを公開するとリスティングが自動で作り直されるため、更新はVCC/ALCOMの一覧に出ます。

## 手動インストール

1. [Releases](https://github.com/limit7412/VRCFE-JsTBridge/releases)から `com.qazx7412.kx-vrc-fe-jst-bridge-<バージョン>.zip` をダウンロードする
2. VCCのプロジェクト管理画面で「Add Package」から「Add from Archive」を選ぶ
3. ダウンロードしたzipファイルを選ぶ

この方法ではVCC/ALCOMが更新を検知しないため、新しいバージョンは手で入れ替えます。

## booth版（unitypackage）

Releasesの `VRCFE-JsTBridge_<バージョン>.zip` に、unitypackageが1つ入っています。
展開してUnityへドラッグすると `Assets/AtelierKairox/VRCFE-JsTBridge/` へ取り込まれます。

**VPM版と同時に入れないでください。**
どちらもアセンブリ名が同じため、1つのプロジェクトへ両方を入れるとコンパイルが通りません。

更新は下の「更新の確認」の通知から行えます。
手で入れ直す場合は `Assets/AtelierKairox/VRCFE-JsTBridge/` をフォルダごと削除してから、新しいunitypackageを取り込んでください。
unitypackageの取り込みはファイルの追加と上書きだけを行うため、上書きするだけでは、そのバージョンで削除されたファイルが残ります。

アバターに追加済みのコンポーネントは、削除して入れ直しても失われません。
配布物のGUIDをリポジトリで固定しており、バージョンが変わっても参照が切れないようにしています。

## 更新の確認

新しいバージョンが出ているかを[Releases](https://github.com/limit7412/VRCFE-JsTBridge/releases/latest)へ問い合わせます。
問い合わせはエディタの読み込み時に自動で走るため、インスペクタを開く必要はありません。

エディタから外部へ通信しますが、送るのはリリース情報の取得要求だけで、1日1回までです。

通信を望まない場合は Preferences > Kx VRC FE-JsT Bridge から止められます。
止めれば問い合わせを行わず、更新の通知も出ません。

更新の手段はインストール方法で違うため、扱いもそれに合わせて変わります。

`Packages/` 配下（VCC/ALCOM経由）の場合は、新しいバージョンが出ていることをインスペクタで知らせるだけにとどめます。
バージョンはVCC/ALCOMが `vpm-manifest.json` で管理しているため、こちらでファイルを置き換えると管理側の記録と実態がずれます。

`Assets/` 配下（unitypackageから導入）の場合は、そのまま更新できます。
新しいバージョンを見つけると、そのバージョンについて一度だけダイアログで知らせます。
そこで「あとで」を選んだ場合は、インスペクタの先頭に出る「更新する」ボタンからいつでも実行できます。

更新は、Releasesからbooth用zipを取得し、GitHubが示すダイジェスト（sha256）と照合してから取り込みます。
取り込みの前に、新しいバージョンに無くなったファイルを削除し、更新前のフォルダを `Temp/` へ控えます。
取り込みは取り消せないため、実行前に確認を挟みます。
フォルダの中を自分で書き換えている場合は、先に控えを取ってください。

フォルダを `Assets/AtelierKairox/VRCFE-JsTBridge/` から動かしている場合、この更新は行えません。
取り込み先はunitypackageの側で決まっており、手元の位置へは追従しないため、実行すると同じアセンブリがプロジェクトに二組できてしまいます。
この場合は上の「booth版」の手順で入れ直してください。
