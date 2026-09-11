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
        private ComboBox _thresholdUnitComboBox = null!;
        private NumericUpDown _speedNumeric = null!;
        private CheckBox _autoStartCheckBox = null!;
        private Button _okButton = null!;
        private Button _cancelButton = null!;

        private bool _suppressUnitConversion;

        private const int UnitIndexMs = 0;
        private const int UnitIndexSeconds = 1;

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
            ClientSize = new Size(360, 220);

            _enabledCheckBox = new CheckBox
            {
                Text = "機能を有効にする",
                Location = new Point(16, 16),
                AutoSize = true
            };

            var thresholdLabel = new Label
            {
                Text = "長押し判定時間:",
                Location = new Point(16, 54),
                AutoSize = true
            };
            _thresholdNumeric = new NumericUpDown
            {
                Location = new Point(150, 50),
                Width = 80,
                Minimum = 50,
                Maximum = 2000,
                Increment = 50,
                DecimalPlaces = 0
            };
            _thresholdUnitComboBox = new ComboBox
            {
                Location = new Point(240, 50),
                Width = 100,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _thresholdUnitComboBox.Items.Add("ミリ秒 (ms)");
            _thresholdUnitComboBox.Items.Add("秒 (s)");
            _thresholdUnitComboBox.SelectedIndexChanged += OnThresholdUnitChanged;

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
                Location = new Point(150, 170),
                DialogResult = DialogResult.OK
            };
            _okButton.Click += OnSaveClicked;

            _cancelButton = new Button
            {
                Text = "キャンセル",
                Location = new Point(245, 170),
                DialogResult = DialogResult.Cancel
            };

            Controls.Add(_enabledCheckBox);
            Controls.Add(thresholdLabel);
            Controls.Add(_thresholdNumeric);
            Controls.Add(_thresholdUnitComboBox);
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
            _autoStartCheckBox.Checked = _settings.AutoStart;
            _speedNumeric.Value = (decimal)_settings.WheelSpeedMultiplier;

            _suppressUnitConversion = true;
            _thresholdUnitComboBox.SelectedIndex = UnitIndexMs;
            ApplyNumericRangeForUnit(UnitIndexMs);
            _thresholdNumeric.Value = Math.Min(_thresholdNumeric.Maximum,
                Math.Max(_thresholdNumeric.Minimum, _settings.LongPressThresholdMs));
            _suppressUnitConversion = false;
        }

        private void ApplyNumericRangeForUnit(int unitIndex)
        {
            if (unitIndex == UnitIndexSeconds)
            {
                _thresholdNumeric.DecimalPlaces = 2;
                _thresholdNumeric.Increment = 0.05m;
                _thresholdNumeric.Minimum = 0.05m;
                _thresholdNumeric.Maximum = 2.00m;
            }
            else
            {
                _thresholdNumeric.DecimalPlaces = 0;
                _thresholdNumeric.Increment = 50m;
                _thresholdNumeric.Minimum = 50m;
                _thresholdNumeric.Maximum = 2000m;
            }
        }

        private void OnThresholdUnitChanged(object? sender, EventArgs e)
        {
            if (_suppressUnitConversion) return;

            // 表示継続性のため、切替前の値を一旦msに換算してから
            // 新しい単位の表示値へ変換する
            int currentMs = GetCurrentThresholdMs();

            _suppressUnitConversion = true;
            ApplyNumericRangeForUnit(_thresholdUnitComboBox.SelectedIndex);

            decimal newValue = _thresholdUnitComboBox.SelectedIndex == UnitIndexSeconds
                ? currentMs / 1000m
                : currentMs;

            _thresholdNumeric.Value = Math.Min(_thresholdNumeric.Maximum,
                Math.Max(_thresholdNumeric.Minimum, newValue));
            _suppressUnitConversion = false;
        }

        private int GetCurrentThresholdMs()
        {
            // 直前まで選択されていた単位を基準に、現在の表示値をmsへ変換する。
            // SelectedIndexChangedの発火タイミングでは既にSelectedIndexが
            // 新しい値になっているため、NumericUpDownの桁数(DecimalPlaces)で
            // 旧単位を判定する(秒モードは小数2桁、msモードは整数)。
            bool wasSeconds = _thresholdNumeric.DecimalPlaces == 2;
            return wasSeconds
                ? (int)Math.Round(_thresholdNumeric.Value * 1000m)
                : (int)_thresholdNumeric.Value;
        }

        private void OnSaveClicked(object? sender, EventArgs e)
        {
            _settings.Enabled = _enabledCheckBox.Checked;
            _settings.WheelSpeedMultiplier = (double)_speedNumeric.Value;

            _settings.LongPressThresholdMs = _thresholdUnitComboBox.SelectedIndex == UnitIndexSeconds
                ? (int)Math.Round(_thresholdNumeric.Value * 1000m)
                : (int)_thresholdNumeric.Value;

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
