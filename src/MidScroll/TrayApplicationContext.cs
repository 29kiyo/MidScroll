using System;
using System.Drawing;
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

            _normalIcon = CreateDotIcon(Color.DodgerBlue);
            _pausedIcon = CreateDotIcon(Color.Gray);

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

        private static Icon CreateDotIcon(Color color)
        {
            using var bitmap = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.Transparent);
                using var brush = new SolidBrush(color);
                g.FillEllipse(brush, 1, 1, 14, 14);
                using var pen = new Pen(Color.White, 1);
                g.DrawEllipse(pen, 1, 1, 14, 14);
            }
            return Icon.FromHandle(bitmap.GetHicon());
        }
    }
}
