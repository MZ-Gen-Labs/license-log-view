# Siemens SMD3・Solid Edge VBLのデバッグログ調査

調査日：2026-10-13（Asia/Tokyo）

## 結論

公開のSiemens公式資料と、製品提供元が作成した公開資料の範囲で調査できる。ただし、今回の検索ではSMD3・Solid Edge VBLについて、実際のOUT／IN／DENIED行、内部機能名、数量の記録方法を確定できる公開サンプルは発見できなかった。

基本的な製品の仕組みと、ライセンスサーバーのログ保存場所は確認できた。これを「Siemens固有の実ログ仕様が確認できた」とは扱わない。現在の合成ログのSYNTH機能名、独自sessionタグ、VBLの仮の消費数を、今回の調査を根拠に正式値へ変更しない。

## 公開資料で確認した内容

|確認事項|資料と確認範囲|
|---|---|
|Siemens Mechanical Designの構成|NX Mach 1とSolid Edge Classicのフローティングライセンス等を組み合わせる製品。内部Feature名・ログ例の説明は確認できなかった|
|Solid Edge VBLの基本製品への追加|2022年資料ではClassic／Premiumと、SMD等の派生座席ライセンス上で使用するフローティングトークンと説明。対象バージョンの条件は別途確認が必要|
|VBLの取得・返却|2026年製品紹介記事で、オプション利用時に必要トークンを取得し、終了時にプールへ戻すと説明。数量・イベント行の出力方法は不明|
|ライセンスサーバーのログ|Siemens作成2024.2インストール資料にWindowsのsaltd.logとLinuxの保存場所を記載|
|vendor daemonの変更|同資料で、v2.0更新時にugslmd等をsaltdへ移行すると説明。顧客環境のサーバー版・daemonを確認する必要がある|
|詳細サポート情報へのアクセス|Siemens Customer Centerの製品サポートは認証が必要。今回、認証が必要な記事の本文は閲覧していない|

## 参照元

1. [Siemens Mechanical Design bundles（Siemens公式PDF、2021年）](https://www.plm.automation.siemens.com/media/global/en/Siemens-Mechnical-Product-Bundles-FS-83767-D2_tcm27-99696.pdf)
2. [Solid Edge 3D design subscription（Siemens公式PDF、2022年）](https://solidedge.siemens.com/wp-content/uploads/2022/10/Siemens-SW-Solid-Edge-3D-design-subscription-FL-40256-D38.pdf)
3. [New in Designcenter Solid Edge 2026: Value-Based Licensing Enhancements（Siemens公式記事）](https://blogs.sw.siemens.com/solidedge/new-in-designcenter-solid-edge-2026-value-based-licensing-enhancements/)
4. [Siemens License Server Installation Instructions 2024.2（Siemens作成、Microchip公式サイト公開PDF）](https://ww1.microchip.com/downloads/aemDocuments/documents/FPGA/swdocs/questasim/sw_siemens_license_server_install_2024_2.pdf)
5. [Siemens Customer Center](https://customer.sw.siemens.com/en-US)
6. [FlexNet Publisher 2026 R1 License Administration Guide（Revenera）](https://docs.revenera.com/fnp/2026r1/pdf/fnp_LicAdmin.pdf)

資料の発行年とWeb検索結果のクロール日を区別する。2021年・2022年・2024.2の資料を、現行の全製品・全契約に適用できるとは判断しない。NX CAM／NX XのVBLとSolid Edge VBLの機能名・消費値を混同しない。

## まだ確定していない点

### SMD3

- SMD3の商品コードと、ログ中のFeature名がどのように対応するか。
- NXとSEの起動で、同じ消費Featureに異なる数量を要求するのか、別のFeature・パッケージを取得するのか。
- 1 SMD3＝3 SE／1 NXという利用権の換算が、ログ上でどの整数単位として表現されるのか。
- 親パッケージ・子機能・基本製品の行が同時に出る場合、どの行を消費量として集計するのか。
- 数量、重複グループ、handle等を通常形式・verbose形式でどこまで取得できるのか。

SMD3を1単位としてSEのログ数量に1/3が記録されると仮定しない。現ツールの30 SE換算単位は集計側のモデルであり、実ログの消費Feature・数量が同じ表現かは未確認。

### Solid Edge VBL

- トークン総量を表すFeature名と、オプションのFeature名。
- トークンのOUTが数量付き1行なのか、別形式・複数行なのか。
- オプションFeatureとトークンFeatureが両方記録されるか。
- オプション終了時に直ちにINとなるか、保持・再利用等があるか。
- 複数オプション利用時に単純加算か、共通機能の重複・割引・統合等があるか。
- トークン不足のDENIEDがどのFeatureに、どの理由・数量で出るか。

「SimulationのOUT1件＝40トークン」等の現設定は仮定。公開資料からその値や出力方式を裏付けられたものではない。

## 正規の情報入手経路

- 公開された公式マニュアル・製品紹介・公開サポート記事を参照する。
- 会社が保有するアクセス権と契約条件の範囲で、Siemens Customer Centerのライセンス関連資料・記事を確認する。
- Siemensまたは契約先販売店へ、対象版のFeature対応表・消費数・デバッグログ例・二重計上防止方法を問い合わせる。回答は参照・テスト利用・顧客配布の可否も区別する。
- 管理者と会社の許可を得た環境で、正規ライセンスによる操作を行い、自社サーバーのログを取得する。契約・社内規程・個人情報・機密情報の扱いを確認する。

公開検索では、認証回避や他社の非公開ログ取得を行っていない。ライセンスファイル全体や署名・顧客識別情報は、実ログの形式確認だけのために外部へ提供する必要はない。匿名化してもFeature名・イベント順序・数量・相対時刻・同一利用者を対応付ける情報は保持する。

## 少量の実ログで確認する実験計画

以下は実施案であり、実機で実施済みではない。

|操作|記録・比較する内容|
|---|---|
|利用前|製品版、サーバー版、daemon、機能別の保有／使用／空き数量、ログ出力設定|
|SEを1人で起動・終了|増減したFeature、数量、OUT／INの対応|
|NXを1人で起動・終了|SEとのFeature・数量の違い|
|SEを複数人で起動|共有消費量の増え方、SMD3換算との一致|
|SEとNXを同時利用|共有プールの残量と両製品の関係|
|VBLオプションを1つ起動・終了|トークンFeatureとオプションFeature、使用量の差分、返却時刻|
|VBLオプションを併用・切替|加算・共通取得・保持・返却順序|
|SEを終了|オプション・トークン・基本製品の返却順序|
|容量不足を再現できる許可済みテスト環境|失敗Feature、要求量、DENIED理由、拒否後の消費量|

各操作の前後に、管理者が利用できるライセンス状態表示（例：許可されたlmutil／lmstat）と、取得できる場合は製品のトークン表示を記録する。ログのイベント差分だけでなく、実際の残量の差分と照合する。稼働中の本番環境を故意に枯渇させることは、この計画の実施条件ではない。

## Generator・検証ロジックへの反映方針

1. 実ログ形式が未確認の現モデルは「業務条件に従う合成ログ」として維持する。
2. 公式一般形式の適合テストと、Siemens固有の消費規則のテストを分ける。
3. 実験結果・正式回答を根拠に、Featureマッピングと出力方式を設定可能にする。
4. 独自sessionタグがないログでも対応付けを検証し、確定できない場合は警告する。
5. 消費量の集計対象を特定して、親子Featureの二重計上を防ぐ。
6. 営業時間内80%の監査は、Generatorと同じ関数を使う確認に加え、独立した手計算の期待値でも検証する。

今回の調査ではGenerator・Analyzerのコード、機能マッピング、同梱ログを変更していない。
