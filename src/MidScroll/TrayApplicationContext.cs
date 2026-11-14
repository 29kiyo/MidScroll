using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;

namespace MidScroll
{
    internal sealed class TrayApplicationContext : ApplicationContext
    {
        private readonly NotifyIcon _trayIcon;
        private readonly AppSettings _settings;
        private readonly ScrollEngine _scrollEngine;
        private readonly AntiCheatGuard _antiCheatGuard;

        private readonly ToolStripMenuItem _enabledMenuItem;
        private readonly ToolStripMenuItem _holdScrollMenuItem;
        private readonly ToolStripMenuItem _pageKeyMenuItem;
        private readonly ToolStripMenuItem _autoStartMenuItem;
        private readonly ToolStripMenuItem _statusMenuItem;

        private readonly Icon _normalIcon;
        private readonly Icon _pausedIcon;

        private BlockCause? _blockCause;
        private readonly ForegroundAllowGuard _allowGuard;

        public TrayApplicationContext(AppSettings settings, ScrollEngine scrollEngine, AntiCheatGuard antiCheatGuard)
        {
            _settings = settings;
            _scrollEngine = scrollEngine;
            _antiCheatGuard = antiCheatGuard;
            _allowGuard = new ForegroundAllowGuard(_settings);
            _allowGuard.AllowedStateChanged += OnAllowedStateChanged;

            _normalIcon = LoadAppIcon();
            _pausedIcon = CreateGrayscaleIcon(_normalIcon);

            var settingsMenuItem = new ToolStripMenuItem("設定を開く...", null, OnOpenSettings);

            _enabledMenuItem = new ToolStripMenuItem("機能を有効にする", null, OnToggleEnabled)
            {
                Checked = _settings.Enabled
            };

            _holdScrollMenuItem = new ToolStripMenuItem("中クリック長押しスクロール", null, OnToggleHoldScroll)
            {
                Checked = _settings.MiddleHoldScrollEnabled
            };

            _pageKeyMenuItem = new ToolStripMenuItem("PageUp/PageDownでホイールスクロール", null, OnTogglePageKey)
            {
                Checked = _settings.PageKeyScrollEnabled
            };

            _autoStartMenuItem = new ToolStripMenuItem("PC起動時に自動起動", null, OnToggleAutoStart)
            {
                Checked = _settings.AutoStart
            };

            _statusMenuItem = new ToolStripMenuItem("状態: 通常")
            {
                Enabled = false
            };

            var exitMenuItem = new ToolStripMenuItem("終了", null, OnExit);

            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add(settingsMenuItem);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(_enabledMenuItem);
            contextMenu.Items.Add(_holdScrollMenuItem);
            contextMenu.Items.Add(_pageKeyMenuItem);
            contextMenu.Items.Add(_autoStartMenuItem);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(_statusMenuItem);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(exitMenuItem);

            _trayIcon = new NotifyIcon
            {
                Icon = _normalIcon,
                ContextMenuStrip = contextMenu,
                Visible = true,
                Text = "MidScroll"
            };
            _trayIcon.DoubleClick += OnOpenSettings;

            // AntiCheatGuardはScrollEngineを直接知らないため、
            // ここでBlockStateChangedを購読し、ユーザーのEnabled設定と合成して
            // SetExternalBlockに渡す(どちらか一方でもtrueなら一時停止)。
            _antiCheatGuard.BlockStateChanged += OnAntiCheatBlockStateChanged;
            _allowGuard.Start();

            ApplyEffectiveBlockState();
        }

        // 一時停止の原因があるか。次のどちらか:
        //   - アンチチート検知(または、起動中のカスタム無効化アプリ)のBlockCause
        //   - 前面にあるカスタム無効化アプリ(前面限定がONのとき)
        private bool HasBlockCause =>
            _blockCause != null || _allowGuard.ForegroundBlockedName.Length > 0;

        private string DescribeCause()
        {
            var parts = new List<string>();
            if (_blockCause is { } cause)
            {
                parts.Add(FormatCause(cause));
            }
            if (_allowGuard.ForegroundBlockedName.Length > 0)
            {
                parts.Add("指定アプリ(前面): " + _allowGuard.ForegroundBlockedName);
            }
            return string.Join(" / ", parts);
        }

        private void OnAllowedStateChanged(object? sender, bool allowed)
        {
            ApplyEffectiveBlockState();
        }

        private void OnAntiCheatBlockStateChanged(object? sender, BlockCause? cause)
        {
            _blockCause = cause;
            ApplyEffectiveBlockState();
        }

        private void ApplyEffectiveBlockState()
        {
            bool paused = HasBlockCause && !_allowGuard.IsAllowed;
            bool shouldBlock = !_settings.Enabled || paused;
            string causeText = HasBlockCause ? DescribeCause() : "-";
            DebugLog.Write($"[block] 有効={_settings.Enabled} 原因={causeText} 許可={_allowGuard.IsAllowed} 許可アプリ={_allowGuard.CurrentName} 停止={shouldBlock}");
            _scrollEngine.SetExternalBlock(shouldBlock);
            UpdateStatusDisplay(shouldBlock);
        }

        private void UpdateStatusDisplay(bool blocked)
        {
            _trayIcon.Icon = blocked ? _pausedIcon : _normalIcon;

            if (!_settings.Enabled)
            {
                _statusMenuItem.Text = "状態: 無効(手動)";
                _trayIcon.Text = "MidScroll (無効)";
            }
            else if (HasBlockCause && _allowGuard.IsAllowed)
            {
                _statusMenuItem.Text = "状態: 動作中(許可アプリ: " + _allowGuard.CurrentName + " が前面)";
                _trayIcon.Text = "MidScroll (許可アプリで動作中)";
            }
            else if (HasBlockCause)
            {
                _statusMenuItem.Text = "状態: 一時停止中(" + DescribeCause() + ")";
                _trayIcon.Text = "MidScroll (一時停止中)";
            }
            else
            {
                _statusMenuItem.Text = "状態: 通常";
                _trayIcon.Text = "MidScroll";
            }
        }

        // 一時停止の原因を「アンチチート: vgc, vgtray / 指定アプリ: notepad」の形式に整形する。
        // 種類ごとに2件まで表示し、超える分は「ほかN件」にまとめる。
        private static string FormatCause(BlockCause cause)
        {
            var parts = new List<string>();
            if (cause.AntiCheat.Count > 0)
            {
                parts.Add("アンチチート: " + Summarize(cause.AntiCheat));
            }
            if (cause.Custom.Count > 0)
            {
                parts.Add("指定アプリ: " + Summarize(cause.Custom));
            }
            return string.Join(" / ", parts);
        }

        private static string Summarize(IReadOnlyList<string> names)
        {
            const int maxShown = 2;
            string head = string.Join(", ", names.Take(maxShown));
            return names.Count > maxShown
                ? $"{head} ほか{names.Count - maxShown}件"
                : head;
        }

        private void OnToggleEnabled(object? sender, EventArgs e)
        {
            _settings.Enabled = !_settings.Enabled;
            _enabledMenuItem.Checked = _settings.Enabled;
            ApplyEffectiveBlockState();
            _settings.Save();
        }

        private void OnTogglePageKey(object? sender, EventArgs e)
        {
            _settings.PageKeyScrollEnabled = !_settings.PageKeyScrollEnabled;
            _pageKeyMenuItem.Checked = _settings.PageKeyScrollEnabled;
            _scrollEngine.ApplyPageKeySettings();
            _settings.Save();
        }

        private void OnToggleHoldScroll(object? sender, EventArgs e)
        {
            _settings.MiddleHoldScrollEnabled = !_settings.MiddleHoldScrollEnabled;
            _holdScrollMenuItem.Checked = _settings.MiddleHoldScrollEnabled;
            _scrollEngine.ApplyHoldScrollSetting();
            _settings.Save();
        }

        private void OnToggleAutoStart(object? sender, EventArgs e)
        {
            _settings.AutoStart = !_settings.AutoStart;
            _autoStartMenuItem.Checked = _settings.AutoStart;
            if (_settings.AutoStart) AutoStartManager.Enable();
            else AutoStartManager.Disable();
            _settings.Save();
        }

        private void OnOpenSettings(object? sender, EventArgs e)
        {
            using var form = new SettingsForm(_settings);
            if (form.ShowDialog() == DialogResult.OK)
            {
                _enabledMenuItem.Checked = _settings.Enabled;
                _autoStartMenuItem.Checked = _settings.AutoStart;

                // LongPressThresholdMs以外(DeadZone等)は_settingsを毎ティック直接
                // 参照しているため自動で反映される。閾値だけは明示的な反映が必要。
                _scrollEngine.ApplyLongPressThreshold();
                _scrollEngine.ApplyPageKeySettings();
                _pageKeyMenuItem.Checked = _settings.PageKeyScrollEnabled;
                _holdScrollMenuItem.Checked = _settings.MiddleHoldScrollEnabled;
                _scrollEngine.ApplyHoldScrollSetting();

                // アンチチート/カスタム無効化リストの変更(トグルON→OFFなど)を、
                // 次の定期ポーリング(既定3秒間隔)を待たずに即座に反映する。
                _antiCheatGuard.ForceRecheck();
                _allowGuard.ForceRecheck();

                ApplyEffectiveBlockState();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _allowGuard.Dispose();
            }
            base.Dispose(disposing);
        }

        private void OnExit(object? sender, EventArgs e)
        {
            _trayIcon.Visible = false;
            _antiCheatGuard.BlockStateChanged -= OnAntiCheatBlockStateChanged;
            Application.Exit();
        }

        /// <summary>
        /// exeに埋め込まれたアイコン(ApplicationIconで指定したicon.ico)を読み込む。
        /// 実体はAppIconProviderに共通化(SettingsFormのタイトルバー/タスクバー
        /// アイコンにも同じ処理を使う)。
        /// </summary>
        private static Icon LoadAppIcon() => AppIconProvider.GetIcon();

        /// <summary>
        /// 一時停止中/無効時に表示する、元アイコンのグレースケール版を生成する。
        /// </summary>
        private static Icon CreateGrayscaleIcon(Icon source)
        {
            using var original = source.ToBitmap();
            using var grayscale = new Bitmap(original.Width, original.Height);

            using (var g = Graphics.FromImage(grayscale))
            {
                var colorMatrix = new ColorMatrix(new float[][]
                {
                    new float[] { 0.3f, 0.3f, 0.3f, 0, 0 },
                    new float[] { 0.59f, 0.59f, 0.59f, 0, 0 },
                    new float[] { 0.11f, 0.11f, 0.11f, 0, 0 },
                    new float[] { 0, 0, 0, 1, 0 },
                    new float[] { 0, 0, 0, 0, 1 }
                });

                using var attributes = new ImageAttributes();
                attributes.SetColorMatrix(colorMatrix);

                g.DrawImage(
                    original,
                    new Rectangle(0, 0, original.Width, original.Height),
                    0, 0, original.Width, original.Height,
                    GraphicsUnit.Pixel,
                    attributes);
            }

            return Icon.FromHandle(grayscale.GetHicon());
        }
    }
}
