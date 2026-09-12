using System;
using System.Drawing;
using System.Drawing.Imaging;
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
        private readonly ToolStripMenuItem _autoStartMenuItem;
        private readonly ToolStripMenuItem _statusMenuItem;

        private readonly Icon _normalIcon;
        private readonly Icon _pausedIcon;

        private bool _antiCheatBlocked;

        public TrayApplicationContext(AppSettings settings, ScrollEngine scrollEngine, AntiCheatGuard antiCheatGuard)
        {
            _settings = settings;
            _scrollEngine = scrollEngine;
            _antiCheatGuard = antiCheatGuard;

            _normalIcon = LoadAppIcon();
            _pausedIcon = CreateGrayscaleIcon(_normalIcon);

            var settingsMenuItem = new ToolStripMenuItem("設定を開く...", null, OnOpenSettings);

            _enabledMenuItem = new ToolStripMenuItem("機能を有効にする", null, OnToggleEnabled)
            {
                Checked = _settings.Enabled
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

            ApplyEffectiveBlockState();
        }

        private void OnAntiCheatBlockStateChanged(object? sender, bool blocked)
        {
            _antiCheatBlocked = blocked;
            ApplyEffectiveBlockState();
        }

        private void ApplyEffectiveBlockState()
        {
            bool shouldBlock = !_settings.Enabled || _antiCheatBlocked;
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
            else if (_antiCheatBlocked)
            {
                _statusMenuItem.Text = "状態: 一時停止中(アンチチート検知)";
                _trayIcon.Text = "MidScroll (一時停止中)";
            }
            else
            {
                _statusMenuItem.Text = "状態: 通常";
                _trayIcon.Text = "MidScroll";
            }
        }

        private void OnToggleEnabled(object? sender, EventArgs e)
        {
            _settings.Enabled = !_settings.Enabled;
            _enabledMenuItem.Checked = _settings.Enabled;
            ApplyEffectiveBlockState();
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
                ApplyEffectiveBlockState();
            }
        }

        private void OnExit(object? sender, EventArgs e)
        {
            _trayIcon.Visible = false;
            _antiCheatGuard.BlockStateChanged -= OnAntiCheatBlockStateChanged;
            Application.Exit();
        }

        /// <summary>
        /// exeに埋め込まれたアイコン(ApplicationIconで指定したicon.ico)を読み込む。
        /// 取得に失敗した場合はシステム標準アイコンにフォールバックする。
        /// </summary>
        private static Icon LoadAppIcon()
        {
            try
            {
                using var extracted = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (extracted != null)
                {
                    return (Icon)extracted.Clone();
                }
            }
            catch
            {
                // フォールバックへ
            }

            return SystemIcons.Application;
        }

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
