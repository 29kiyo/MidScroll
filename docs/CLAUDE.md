# MidScroll 全体設計図

## 概要
マウスホイールクリック(中クリック)を長押しすると、その時点のカーソル位置を起点として
オートスクロールモードに入る常駐ユーティリティ。起点から上下にカーソルを動かすと、
その距離に応じた速度でマウスホイール回転イベントを送信する。再度ホイールクリックする
とモードを終了する。Windows専用、タスクトレイ常駐、PC起動時に自動起動。

## 技術スタック
- 言語/フレームワーク: C# / .NET 8 / Windows Forms
- 配布形式: 自己完結型シングルファイル発行 + ReadyToRun (トリミング有効)
  - 備考: WinFormsはNativeAOTを公式サポートしていないため採用しない。
    将来的にNativeAOT化が可能になった場合は再検討する。
- 設定永続化: JSONファイル (%AppData%\MidScroll\settings.json)
- 自動起動: レジストリ HKCU\Software\Microsoft\Windows\CurrentVersion\Run
- インストーラー: Inno Setup
- CI/CD: GitHub Actions (build.yml)

## 機能要件
1. 中ボタン(MButton)を長押し(閾値: 設定可能, 既定300ms)すると
   その時点のカーソル座標を起点にスクロールモードへ移行する。
2. 閾値未満で離した場合は通常の中クリックとして扱う(他アプリへ中クリックを伝える)。
3. スクロールモード中、起点からのY方向オフセットに応じてWheelUp/WheelDownを送信する。
   - デッドゾーン(起点付近の不感帯)を設ける
   - オフセットが大きいほど送信間隔を短く/一度に送る量を多くし、速く回転させる
   - 最大速度の上限を設ける
4. スクロールモード中に中ボタンを(短く)クリックするとモードを終了する。
5. タスクトレイに常駐し、右クリックメニューから
   - 設定画面を開く
   - 機能の有効/無効切り替え(設定画面を開かずに変更可)
   - 自動起動の有効/無効切り替え(設定画面を開かずに変更可)
   - 終了
   を行える。
6. 設定画面(WinForms、シンプルなUI)で以下を編集できる。
   - ホイール回転速度の倍率
   - 長押し判定時間(ミリ秒)
   - 自動起動 ON/OFF
   - 機能全体の有効/無効
7. PC起動時に自動起動する(設定でON/OFFできる)。

## アーキテクチャ構成(クラス設計、暫定)
- Program.cs : エントリポイント。多重起動防止(Mutex)、ApplicationContext起動。
- TrayApplicationContext.cs : NotifyIcon/トレイメニューの管理、アプリのライフサイクル管理。
- MouseHookService.cs : WH_MOUSE_LL によるグローバルマウスフック。
  MButton Down/Up、MouseMove イベントを内部イベントとして発行する。
- ScrollEngine.cs : 長押し判定、起点管理、オフセット→速度変換、
  SendInputでのホイールイベント送信ロジック。
- AppSettings.cs : 設定のロード/セーブ(JSON)、既定値定義。
- AutoStartManager.cs : レジストリ Run キーの登録/解除。
- SettingsForm.cs : 設定画面(コードビハインドでコントロールを構築、デザイナー未使用)。
- NativeMethods.cs : P/Invoke宣言(SetWindowsHookEx, SendInput, GetCursorPos等)。

## 動作フロー(概要)
1. MButtonDown検知 → フックでデフォルト動作を抑制 → タイマー開始、起点座標を記録
2. 閾値時間内にMButtonUpが来た場合 → 抑制した分を補うため、SendInputで
   通常の中クリック(Down+Up)を合成して送信し、フローを終了
3. 閾値経過してもボタンが押されたままの場合 → スクロールモードへ移行
4. スクロールモード中、一定間隔(例: 30ms)でGetCursorPos()し、
   起点とのY差分から速度を計算、閾値を超えた分だけWheelイベントを送信
5. スクロールモード中にMButtonDownを検知 → モード終了、抑制、後続のUpも抑制

## 配布/ビルド方針
- GitHub Actions (workflow_dispatch) でタグ(例: v1.0.0)を入力
- 発行: dotnet publish (self-contained, win-x64, PublishReadyToRun=true, PublishSingleFile=true)
- zip化: 発行フォルダをzip圧縮、ファイル名に入力タグを含める
  例: MidScroll-v1.0.0-win-x64.zip
- インストーラー: Inno Setup (ISCC) でビルド、ファイル名にタグを含める
  例: MidScroll-Setup-v1.0.0.exe
- Artifactは作成せず、softprops/action-gh-release 等で直接GitHub Releaseを作成し、
  zip・インストーラーの両方を添付する。

## フェーズ一覧
- フェーズ1: 環境構築・リポジトリ作成・ドキュメント整備 (docs/phase1.md)
- フェーズ2: コア機能実装(マウスフック・スクロールエンジン) (docs/phase2.md)
- フェーズ3: トレイ常駐・設定画面・自動起動 (docs/phase3.md)
- フェーズ4: GitHub Actions CI/CD (ビルド・パッケージング・自動リリース) (docs/phase4.md)
- フェーズ5: 統合テスト・調整・仕上げ (docs/phase5.md)

進捗管理は docs/development_status.md を参照。
