初回リリース。FlexLMログの集計・ブラウザ表示・CSV/HTML保存と、NX / Solid Edgeの合成検証ログ生成を提供します。

## ダウンロード

- Windows x64: `win-x64-setup.exe` はInno Setupインストーラー。`win-x64-portable.zip` は展開して使う版。
- Apple Silicon Mac: `osx-arm64.pkg` または `osx-arm64.zip`。
- Intel Mac: `osx-x64.pkg` または `osx-x64.zip`。
- .NETの追加インストールは不要です。

MacのPKGはApplicationsにアプリを配置します。アプリを開くとTerminalとローカルブラウザが開き、終了はTerminalでCtrl+Cです。設定・ログは利用者の `~/Library/Application Support/License Log View` に保存します。

Windowsは利用者のLocalAppData配下へインストールします。設定・サンプルデータは更新時に上書きせず、アンインストール後も残します。

配布物はWindowsの発行元署名、Apple Developer ID署名・公証を行っていません。OSの保護機能による確認が表示される場合があります。Mac版はmacOS 14以降を対象とします。

同梱の3か月ログは合成データです。Siemens実ログとの完全互換性は未検証です。

`SHA256SUMS.txt` に配布物のSHA-256を掲載しています。プロジェクト自身の配布ライセンスは未設定です。
