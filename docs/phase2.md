# フェーズ2: コア機能実装(マウスフック・スクロールエンジン)

## 目的
中クリック長押し検出、オフセットに応じたホイール送信ロジックの中核部分を実装する。
(この段階ではトレイ・設定UIは未実装。コンソールログや簡易フォームで動作確認する。)

## 作業内容
- NativeMethods.cs: SetWindowsHookEx, SendInput, GetCursorPos等のP/Invoke宣言
- MouseHookService.cs: グローバル低レベルマウスフックの実装
- ScrollEngine.cs:
  - MButtonDown/Up検知、長押し判定タイマー
  - 短押し時は通常クリックを合成して送信(抑制分の補填)
  - 長押し確定でスクロールモード開始(起点記録)
  - モード中のポーリング(一定間隔でのオフセット計算・Wheel送信)
  - デッドゾーン・速度カーブ・最大速度の実装
  - モード中の再クリックで終了
- AppSettings.cs: ひとまず既定値のみ(設定画面はフェーズ3)
- 手動動作確認(実際にブラウザ等でスクロールされることを確認)

## 完了条件
- 中クリック長押しでスクロールモードに入り、上下移動でスクロールが実際に動作する
- 起点から離れるほどスクロール速度が上がることが体感で確認できる
- 短い中クリックは他アプリに通常通り伝わる
- 再度中クリックでモードが終了する

## 成果物
- src/MidScroll/NativeMethods.cs
- src/MidScroll/MouseHookService.cs
- src/MidScroll/ScrollEngine.cs
- src/MidScroll/AppSettings.cs (仮)
