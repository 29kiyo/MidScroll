using System;
using static MidScroll.NativeMethods;

namespace MidScroll
{
    internal sealed class ScrollEngine : IDisposable
    {
        private enum State
        {
            Idle,
            PressPending,
            ScrollActive
        }

        private readonly MouseHookService _hookService;
        private readonly AppSettings _settings;

        private readonly System.Windows.Forms.Timer _pressTimer;
        private readonly System.Windows.Forms.Timer _pollTimer;

        private State _state = State.Idle;
        private POINT _origin;
        private double _wheelAccumulator;
        private bool _suppressNextUp;
        private readonly ScrollCursorManager _cursorManager = new ScrollCursorManager();
        private bool _externallyBlocked;

        public event EventHandler<string>? StatusChanged;

        public ScrollEngine(MouseHookService hookService, AppSettings settings)
        {
            _hookService = hookService;
            _settings = settings;

            _pressTimer = new System.Windows.Forms.Timer { Interval = _settings.LongPressThresholdMs };
            _pressTimer.Tick += OnPressTimerTick;

            _pollTimer = new System.Windows.Forms.Timer { Interval = _settings.PollingIntervalMs };
            _pollTimer.Tick += OnPollTimerTick;

            _hookService.MiddleButtonDown += OnMiddleButtonDown;
            _hookService.MiddleButtonUp += OnMiddleButtonUp;
        }

        private void OnMiddleButtonDown(object? sender, MouseHookEventArgs e)
        {
            if (_externallyBlocked) return; // アンチチート対象検知中は素通り

            switch (_state)
            {
                case State.Idle:
                    _origin = e.Point;
                    _state = State.PressPending;
                    _pressTimer.Start();
                    e.Suppress = true;
                    Log("押下検知。長押し判定タイマー開始。");
                    break;

                case State.PressPending:
                    e.Suppress = true; // 念のための防御
                    break;

                case State.ScrollActive:
                    EndScrollMode();
                    _suppressNextUp = true;
                    e.Suppress = true;
                    Log("再クリック検知。スクロールモード終了。");
                    break;
            }
        }

        private void OnMiddleButtonUp(object? sender, MouseHookEventArgs e)
        {
            if (_externallyBlocked) return; // アンチチート対象検知中は素通り

            switch (_state)
            {
                case State.Idle:
                    break; // 通常は発生しない

                case State.PressPending:
                    // 閾値未満での解放 → 通常クリックとして補填送信
                    _pressTimer.Stop();
                    _state = State.Idle;
                    e.Suppress = true;
                    SendSyntheticMiddleClick();
                    Log("短押しと判定。通常クリックを合成送信。");
                    break;

                case State.ScrollActive:
                    if (_suppressNextUp)
                    {
                        _suppressNextUp = false;
                    }
                    // 長押し確定後の自然な指離しも含め、モード中のUpは常に抑制
                    e.Suppress = true;
                    break;
            }
        }

        private void OnPressTimerTick(object? sender, EventArgs e)
        {
            _pressTimer.Stop();
            if (_state != State.PressPending) return;

            _state = State.ScrollActive;
            _wheelAccumulator = 0;
            _cursorManager.Begin();
            _pollTimer.Start();
            Log("長押し確定。スクロールモード開始。");
        }

        private void OnPollTimerTick(object? sender, EventArgs e)
        {
            if (_state != State.ScrollActive) return;

            GetCursorPos(out POINT current);
            int dy = current.Y - _origin.Y;
            int absDy = Math.Abs(dy);

            if (absDy <= _settings.DeadZonePixels)
            {
                _cursorManager.Update(ScrollCursorState.Neutral);
                return;
            }

            _cursorManager.Update(dy < 0 ? ScrollCursorState.Up : ScrollCursorState.Down);

            int effective = absDy - _settings.DeadZonePixels;
            double t = Math.Min(1.0, (double)effective / _settings.MaxSpeedRangePixels);
            double curved = t * t; // 二次カーブで加速感を出す

            double step = _settings.MinWheelStepPerTick
                + (_settings.MaxWheelStepPerTick - _settings.MinWheelStepPerTick) * curved;
            step *= _settings.WheelSpeedMultiplier;

            int sign = dy > 0 ? -1 : 1; // カーソルが下 → 下スクロール(負のWheelDelta)
            _wheelAccumulator += step * sign;

            SendAccumulatedWheel();
        }

        private void SendAccumulatedWheel()
        {
            const int wheelDelta = 120;
            while (Math.Abs(_wheelAccumulator) >= wheelDelta)
            {
                int notch = _wheelAccumulator > 0 ? wheelDelta : -wheelDelta;
                SendWheel(notch);
                _wheelAccumulator -= notch;
            }
        }

        // アンチチート対象ゲーム/サービスの検知状態が変わったときに呼ばれる。
        // ブロック開始時は進行中のモードを強制的に終了し、以後の入力を素通りさせる。
        // フェーズ3: 設定画面でLongPressThresholdMsを変更した際、
        // 稼働中の_pressTimerへ即座に反映するための公開メソッド。
        public void ApplyLongPressThreshold()
        {
            _pressTimer.Interval = _settings.LongPressThresholdMs;
        }

        public void SetExternalBlock(bool blocked)
        {
            if (_externallyBlocked == blocked) return;
            _externallyBlocked = blocked;

            if (blocked)
            {
                if (_state == State.ScrollActive)
                {
                    EndScrollMode();
                }
                else if (_state == State.PressPending)
                {
                    _pressTimer.Stop();
                    _state = State.Idle;
                }
                Log("アンチチート対象ゲーム/サービスを検知。MidScrollを一時停止します。");
            }
            else
            {
                Log("アンチチート対象ゲーム/サービスの終了を検知。MidScrollを再開します。");
            }
        }

        private void EndScrollMode()
        {
            _pollTimer.Stop();
            _wheelAccumulator = 0;
            _cursorManager.End();
            _state = State.Idle;
        }

        private static void SendWheel(int delta)
        {
            var input = new INPUT
            {
                type = INPUT_MOUSE,
                mi = new MOUSEINPUT
                {
                    mouseData = unchecked((uint)delta),
                    dwFlags = MOUSEEVENTF_WHEEL
                }
            };
            SendInput(1, new[] { input }, System.Runtime.InteropServices.Marshal.SizeOf<INPUT>());
        }

        private static void SendSyntheticMiddleClick()
        {
            var down = new INPUT { type = INPUT_MOUSE, mi = new MOUSEINPUT { dwFlags = MOUSEEVENTF_MIDDLEDOWN } };
            var up = new INPUT { type = INPUT_MOUSE, mi = new MOUSEINPUT { dwFlags = MOUSEEVENTF_MIDDLEUP } };
            SendInput(2, new[] { down, up }, System.Runtime.InteropServices.Marshal.SizeOf<INPUT>());
        }

        private void Log(string message) => StatusChanged?.Invoke(this, message);

        public void Dispose()
        {
            _cursorManager.End();
            _pressTimer.Stop();
            _pressTimer.Dispose();
            _pollTimer.Stop();
            _pollTimer.Dispose();
            _hookService.MiddleButtonDown -= OnMiddleButtonDown;
            _hookService.MiddleButtonUp -= OnMiddleButtonUp;
        }
    }
}
