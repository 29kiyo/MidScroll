using System;
using System.Drawing;
using System.Windows.Forms;

namespace MidScroll
{
    internal sealed class SettingsForm : Form
    {
        private readonly AppSettings _settings;

        private CheckBox _enabledCheckBox = null!;
        private NumericUpDown _thresholdNumeric = null!;
        private NumericUpDown _speedNumeric = null!;
        private CheckBox _autoStartCheckBox = null!;
        private Button _okButton = null!;
        private Button _cancelButton = null!;

        public SettingsForm(AppSettings settings)
        {
            _settings = settings;
            BuildUi();
            LoadValues();
        }

        private void BuildUi()
        {
            Text = "MidScroll 設定";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(340, 220);

            _enabledCheckBox = new CheckBox
            {
                Text = "機能を有効にする",
                Location = new Point(16, 16),
                AutoSize = true
            };

            var thresholdLabel = new Label
            {
                Text = "長押し判定時間 (ms):",
                Location = new Point(16, 54),
                AutoSize = true
            };
            _thresholdNumeric = new NumericUpDown
            {
                Location = new Point(190, 50),
                Width = 100,
                Minimum = 50,
                Maximum = 2000,
                Increment = 50
            };

            var speedLabel = new Label
            {
                Text = "ホイール速度倍率:",
                Location = new Point(16, 92),
                AutoSize = true
            };
            _speedNumeric = new NumericUpDown
            {
                Location = new Point(190, 88),
                Width = 100,
                Minimum = 0.1m,
                Maximum = 5.0m,
                Increment = 0.1m,
                DecimalPlaces = 1
            };

            _autoStartCheckBox = new CheckBox
            {
                Text = "PC起動時に自動起動する",
                Location = new Point(16, 130),
                AutoSize = true
            };

            _okButton = new Button
            {
                Text = "保存",
                Location = new Point(130, 170),
                DialogResult = DialogResult.OK
            };
            _okButton.Click += OnSaveClicked;

            _cancelButton = new Button
            {
                Text = "キャンセル",
                Location = new Point(225, 170),
                DialogResult = DialogResult.Cancel
            };

            Controls.Add(_enabledCheckBox);
            Controls.Add(thresholdLabel);
            Controls.Add(_thresholdNumeric);
            Controls.Add(speedLabel);
            Controls.Add(_speedNumeric);
            Controls.Add(_autoStartCheckBox);
            Controls.Add(_okButton);
            Controls.Add(_cancelButton);

            AcceptButton = _okButton;
            CancelButton = _cancelButton;
        }

        private void LoadValues()
        {
            _enabledCheckBox.Checked = _settings.Enabled;
            _thresholdNumeric.Value = _settings.LongPressThresholdMs;
            _speedNumeric.Value = (decimal)_settings.WheelSpeedMultiplier;
            _autoStartCheckBox.Checked = _settings.AutoStart;
        }

        private void OnSaveClicked(object? sender, EventArgs e)
        {
            _settings.Enabled = _enabledCheckBox.Checked;
            _settings.LongPressThresholdMs = (int)_thresholdNumeric.Value;
            _settings.WheelSpeedMultiplier = (double)_speedNumeric.Value;

            bool autoStartChanged = _settings.AutoStart != _autoStartCheckBox.Checked;
            _settings.AutoStart = _autoStartCheckBox.Checked;

            if (autoStartChanged)
            {
                if (_settings.AutoStart) AutoStartManager.Enable();
                else AutoStartManager.Disable();
            }

            _settings.Save();
        }
    }
}
