# License Log View — NX / Solid Edge ライセンス稼働検証

C# / .NET 10だけで、仮想ログ生成、ログ解析、ローカルブラウザ画面、CSV出力を提供します。Python、Node.js、外部JavaScriptライブラリ、クラウドサービスは利用時に不要です。画面には組み込みのHTML/JavaScriptを使用し、サーバー処理はC#です。

## Windowsで使う

1. `license-log-view-windows-x64.zip` を任意の書き込み可能なフォルダに展開します。
2. `Start-Windows.cmd` をダブルクリックします。
3. `http://127.0.0.1:8765` がブラウザで開きます。
4. 終了は起動時のコンソールで Ctrl+C。

配布済み `LicenseLogView.exe` はWindows x64向けself-contained版で、顧客端末への.NET SDK / ランタイム導入は不要です。Windows ARM64は別途 `win-arm64` でpublishしてください。実行ファイルは未署名です。

画面から複数のログを選択して再集計できます。設定欄で期間・機能マッピングを変更してからログを読み込んでください。アップロード先は端末内のローカルアプリです。実ログは保存せずメモリ内で解析し、集計JSON・CSV・単体HTMLを任意に保存できます。入力はUTF-8（BOM有無両対応）で、その他の文字コードは事前にUTF-8へ変換してください。

CSVと保存HTMLは読み込んだ全期間・全利用者です。画面の期間フィルターは各集計に、利用者フィルターはユーザー・VBL・拒否表に反映されます。HTML保存版はサーバーや.NETがなくてもブラウザだけで閲覧・絞り込みできますが、新しいログの解析・生成は実行ファイルが必要です。

## Macで起動・開発・検証する

`Start-Mac.command`をダブルクリックするか、次のコマンドで起動できます。

```sh
cd license-log-view
dotnet run --project src/LicenseLogView/LicenseLogView.csproj -c Release -- serve
```


.NET 10 SDKが必要です。追加NuGetライブラリは使用しません。

```sh
dotnet build -c Release
dotnet run --project tests/LicenseLogView.Tests/LicenseLogView.Tests.csproj -c Release
dotnet run --project src/LicenseLogView/LicenseLogView.csproj -c Release --no-build -- generate
dotnet run --project src/LicenseLogView/LicenseLogView.csproj -c Release --no-build -- audit
dotnet run --project src/LicenseLogView/LicenseLogView.csproj -c Release --no-build -- analyze
dotnet run --project src/LicenseLogView/LicenseLogView.csproj -c Release --no-build -- serve
```

Mac向け実行ファイルは `.NET SDK` を利用して `osx-arm64` または `osx-x64` でpublish可能です。Macでの機能検証とWindows用実行ファイルの作成はできますが、Windows EXEの起動・ブラウザ連携はWindows端末で確認してください。

## Generator

標準コマンドは `generate` です（ご依頼のランダムログGenerator）。同一シード・設定・.NETバージョンで再現できます。

```bat
LicenseLogView.exe generate --config config.json --logs generated-seed-123 --seed 123
LicenseLogView.exe audit --config config.json --logs generated-seed-123
LicenseLogView.exe analyze --config config.json --logs generated-seed-123 --out reports-seed-123
LicenseLogView.exe serve --config config.json --logs generated-seed-123
```

Macでは `LicenseLogView.exe` を `dotnet src/LicenseLogView/bin/Release/net10.0/LicenseLogView.dll` に置き換えてください。

`--seed` の代わりにconfig.jsonのseedを変更しても構いません。画面のGeneratorでは「ランダムに変更」→「検証ログ一式を生成・ZIP保存」で別パターンを保存できます。生成ZIPにはログ、設定、正解データ、生成情報、監査結果が入ります。既定の検証ログは上書きしません。CLIでは指定出力フォルダの同名ファイルを置き換えるため、別フォルダを指定してください。

生成期間は `start` 以上 `endExclusive` 未満です。3か月版は2026-07-01〜2026-09-30。1か月版なら `endExclusive` を2026-08-01にします。50人・10 SMD3・200トークンが基準です。少ない利用者数で高い稼働率を要求する等、達成不能な設定では監査が失敗することがあります。画面は監査に合格したZIPのみ返します。

## フォルダ構成

```text
license-log-view/
├── LicenseLogView.slnx           開発用ソリューション
├── src/LicenseLogView/          実行に使うソースコード
│   ├── LicenseLogView.csproj
│   ├── Program.cs              起動・ブラウザ向けAPI
│   ├── Core/                   ログ生成・解析・監査・データ定義
│   └── Web/index.html          組み込み画面
├── tests/LicenseLogView.Tests/  テスト専用プロジェクト
├── docs/                       仮定・検証結果・参考資料
├── config.json                 実行設定
├── data/                       同梱の検証ログ・正解データ
├── reports/                    集計結果と保存HTML
├── Start-Mac.command           Mac起動（SDKが必要）
├── Start-Windows.cmd           Windows配布EXEの起動
├── Build-Windows.cmd           Windows配布EXEのビルド
├── Test-Mac.command            テスト実行
└── Test-Windows.cmd             テスト実行
```

`src`だけが本体のビルド対象です。`tests`は独立プロジェクトで、本体EXEにテストコードを含めません。`docs`は説明資料です。

`data`には月別ログ、`manifest.json`（生成情報）、`truth-events.json`（正解イベント）、`audit.json`（監査結果）を保存しています。`reports`には集計JSON・CSV・オフラインHTMLを保存しています。

Windowsで実行するだけなら、`LicenseLogView.exe`・`config.json`・`data`・`Start-Windows.cmd`を同じフォルダに置きます。`src`・`tests`・`docs`・SDKは実行には不要です。CSV等の出力時には`reports`が作成されます。

本体はまず起動時の作業フォルダのconfig.jsonを確認し、見つからない場合は実行ファイルの場所から親フォルダを探します。ソースのサブフォルダから実行しても、リポジトリ直下の設定とログを見つけられます。

## Windows配布版をMacからビルド

```sh
dotnet publish src/LicenseLogView/LicenseLogView.csproj -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:DebugType=None -o publish/win-x64
```

初回publishにはWindows用の公式.NETランタイムパッケージ取得が必要です。開発環境でビルドを済ませた配布ZIPを閉域の顧客端末へ移せば、顧客側ではネットワーク接続なしで動作します。

## 実ログへの適用

今回の検証ログは製品の利用モデルを検証するための合成データであり、Siemens実ログの完全な再現ではありません。仮の機能名は `SYNTH_` で始まります。実データへ適用する前に、匿名化した実ログと機能一覧・契約上の換算規則で設定を確認してください。

基本形式 `hh:mm:ss (daemon) OUT/IN/DENIED: "feature" user@host`、`TIMESTAMP M/d/yyyy`、日付またぎ、複数月の取得/返却、`(N licenses)`、部分返却に対応します。合成ログでは取得単位を一意にする `[session=ID]` を付与します。IDのない実ログは機能・利用者・端末ごとにFIFOで対応付けます。異なる期間/サーバーのログを混ぜないでください。

日付を特定できない行、未知機能、未対応のverbose形式、取得のない返却、未返却、容量超過、再起動/停止は画面に警告します。同内容ファイルは重複除外しますが、部分的に重複したローテーションログは自動除外しません。重複区間は前処理してください。

スイートの親子機能がログに両方出る場合、その両方を消費機能に登録すると二重計上します。解析対象と除外対象を実際の取得構造から決めてください。未返却は期間末まで占有と仮定し、未対応INは除外するため、ログ欠落や借用・再起動があるデータの数値は警告と併せて評価してください。実ログ対応の完成確認には実サンプルが必要です。

## 参考資料

- [Revenera FlexNet Publisher 2026 R1 License Administration Guide](https://docs.revenera.com/fnp/2026r1/pdf/fnp_LicAdmin.pdf): 基本ログ形式、OUT/IN/DENIED、TIMESTAMP。
- [Siemens: Introducing Solid Edge Value-Based Licensing](https://blogs.sw.siemens.com/solidedge/introducing-value-based-licensing/): オプションを共通トークンで利用する仕組み。
- [Microsoft: Single-file deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview): Windows向けself-contained配布。

SMD3換算比率はご指定の条件を採用し、上記資料から契約上の比率を検証したものではありません。VBLの機能名・消費数は公式値として扱わないでください。

## 仕様調査・OSSライセンスの検討メモ

[FlexLM解析の参考資料とOSS・名称・ライセンス検討メモ](docs/FLEXLM_REFERENCE_AND_LICENSE_NOTES.md)に、公式仕様とOSS資料の使い分け、独立実装とコード流用の違い、MIT等の条件、候補名称の重複調査をまとめています。

## 名称変更

2026-10-13に、表示名をLicense Log View、作業フォルダを`license-log-view`、プロジェクト・実行ファイル名を`LicenseLogView`へ変更しました。既存の利用モデル・ログデータ・解析範囲は同じです。

## ソース配置の整理（2026-10-13）

実行用ソースは`src/LicenseLogView`、解析テストは`tests/LicenseLogView.Tests`、説明資料は`docs`へ移動しました。従来の本体コマンド`selftest`の代わりに、Test-Mac.command／Test-Windows.cmdまたはテストプロジェクトを実行してください。Windows publishの生成先は`publish/win-x64`です。Start-Windows.cmdは上位フォルダのEXEを起動するため、再ビルド後は生成EXEを上位フォルダへコピーします。

## GitHub公開版について

GitHubにはソースコード、テスト、説明資料、既知の合成サンプルデータを保存します。Windows EXEと配布ZIPはGit管理に含めません。クローンしたソースからWindows版を使用する場合は、上記のpublish手順でビルドし、`publish/win-x64/LicenseLogView.exe`をリポジトリ直下へコピーしてから`Start-Windows.cmd`を実行してください。

`reports`は集計時に生成されます。新たに追加するログや`data`内のファイルは原則Git対象外です。顧客の実ログや個別設定を、強制追加で公開しないでください。

このプロジェクト自身の配布ライセンスは未決定です。Public公開だけでMIT等の再利用許諾を設定するものではありません。
