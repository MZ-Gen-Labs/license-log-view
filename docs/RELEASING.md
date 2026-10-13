# リリースとインストーラー

## 構成

- `.github/workflows/release.yml`: タグpush、リリースpublished、手動実行に対応。
- `VERSION`: 配布バージョン。タグは`vMAJOR.MINOR.PATCH`で一致が必須。
- `packaging/windows/installer.iss`: Inno Setup 6。固定AppIdを維持し、利用者単位でインストール。
- `packaging/windows/build.ps1`: 配布ZIP、セットアップEXEの作成、無人インストール・解析・HTTP・アンインストール確認。
- `packaging/macos/build.sh`: .app、PKG、ZIPの作成、署名整合性・インストール後の解析・HTTP確認。
- `packaging/macos/launcher`と`Start.command`: Terminal起動と利用者用データ領域の初期化。
- `packaging/RELEASE_NOTES.md`: 新規リリースの本文。新バージョンごとに更新。

GitHub ActionsはWindows Server 2022、Apple Silicon / IntelのmacOS 15で各OS用のself-contained実行ファイルを生成します。SDKは.NET 10の最新パッチを取得するため、同一タグの再ビルドで実行ファイルのハッシュが変わる場合があります。主要ActionsはコミットSHAで固定しています。

配布物名は`LicenseLogView-VERSION-win-x64-setup.exe`、`win-x64-portable.zip`、`osx-arm64.pkg`/`.zip`、`osx-x64.pkg`/`.zip`です。Windows ARM64ネイティブ版は今回含めません。

## 新しいリリース

1. `VERSION`と`src/LicenseLogView/LicenseLogView.csproj`のVersionを同じ値へ更新。
2. リリースノートを更新し、テストしてコミット・push。
3. `git tag v0.0.2`のようにタグを作成してpush。
4. Actionsの3種類のビルドが成功するとリリースを公開。失敗時は成功した一部だけを公開しません。

タグを作る前の確認はRun workflowで`main`を指定できます。この場合はビルドとパッケージ検証だけを行い、リリースを公開しません。既存タグからの再実行はGitHub Actions画面のRun workflowでタグを指定します。同じ名前の添付ファイルは置き換えます。手動でリリースを先に公開した場合もpublishedイベントで処理しますが、その場合は添付が揃うまで公開済みの状態になります。通常はタグpushによる自動公開を推奨します。

## 保存先と削除

Windowsは`%LOCALAPPDATA%\Programs\License Log View`にインストールし、設定とサンプルログを同じ場所に保存します。アンインストールはプログラムとショートカットを削除し、config.jsonとdataは残します。利用者が作成した集計出力も削除しません。完全削除時は残ったフォルダを手動で削除してください。

Macは`/Applications/License Log View.app`へ配置し、初回起動時に`~/Library/Application Support/License Log View`へ設定とサンプルデータをコピーします。更新時に既存データを置き換えません。削除はアプリをゴミ箱へ移動してください。設定も削除する場合は利用者用フォルダを別途削除します。アプリ起動時にTerminalが開き、Ctrl+Cでサーバーを停止します。

合成サンプルは初期検証のために同梱しています。顧客実ログはGitや配布物へ追加しないでください。

## 検証と制限

全プラットフォームで既存13テストを実行します。WindowsではInnoインストーラーを一時フォルダへ無人インストールし、実行ファイルによる解析、localhostのAPI応答、アンインストール、設定の保持を確認します。Macでは使い捨てCI環境にPKGをインストールし、アプリ内の実行ファイルによる解析とlocalhost APIを確認します。いずれも50利用者・155拒否を確認します。

デスクトップ上のショートカット／Finderからの起動や、ブラウザ自動起動、OSの保護機能の表示は目視確認が別途必要です。自動検証の成功はSiemens実ログ互換性を保証しません。

Windows Authenticode署名、Apple Developer ID署名・公証には利用者が保有する証明書等が必要です。初回リリースでは未設定です。Macのアドホック署名は発行元を証明する署名ではありません。

## ローカルでの作成

Macでは本体を`osx-arm64`または`osx-x64`にpublish後、`bash packaging/macos/build.sh 0.0.1 osx-arm64`を実行します。ローカルではPKGを展開して確認し、Applicationsを変更しません。jqとmacOS標準のpkgbuild/pkgutil/codesignが必要です。

Windowsではwin-x64へpublish後、Inno Setup 6をインストールし、PowerShell 7から`./packaging/windows/build.ps1 -Version 0.0.1`を実行します。インストール・アンインストール検証も実行されます。

参考: [Inno Setup compiler](https://jrsoftware.org/ishelp/topic_compilercmdline.htm)、[GitHub runner images](https://github.com/actions/runner-images)、[setup-dotnet](https://github.com/actions/setup-dotnet)。
