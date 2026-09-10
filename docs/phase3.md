# フェーズ3: トレイ常駐・設定画面・自動起動

## 目的
バックグラウンド常駐化、タスクトレイからの操作、設定画面、
設定の永続化、PC起動時自動起動を実装する。

## 作業内容
- Program.cs: 多重起動防止(Mutex)、メインウィンドウを持たないApplicationContext化
- TrayApplicationContext.cs:
  - NotifyIcon表示、右クリックメニュー
    - 設定を開く
    - 機能の有効/無効(チェック付きメニュー、設定画面を開かず切替可)
    - 自動起動の有効/無効(チェック付きメニュー、設定画面を開かず切替可)
    - 終了
- SettingsForm.cs: シンプルなWinFormsフォーム(コード生成、デザイナー未使用)
  - ホイール速度倍率
  - 長押し判定時間(ms)
  - 自動起動 ON/OFF
  - 機能全体の有効/無効
  - 保存/キャンセル、即時反映
- AppSettings.cs: JSON読み書き(%AppData%\MidScroll\settings.json)を完成させる
- AutoStartManager.cs: レジストリRunキーの登録/解除

## 完了条件
- アプリ起動でウィンドウが表示されずトレイにアイコンのみ表示される
- トレイメニューから設定画面を開き、変更が即座に反映される
- トレイメニューから設定画面を開かずに有効/無効・自動起動を切り替えられる
- 自動起動ONでサインイン時にアプリが起動する
- 設定がアプリ再起動後も保持される

## 成果物
- src/MidScroll/Program.cs
- src/MidScroll/TrayApplicationContext.cs
- src/MidScroll/SettingsForm.cs
- src/MidScroll/AutoStartManager.cs
- src/MidScroll/AppSettings.cs (完成版)
