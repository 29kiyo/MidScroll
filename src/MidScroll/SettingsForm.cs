using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
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

        private CheckBox _antiCheatEnabledCheckBox = null!;
        private ListView _antiCheatListView = null!;

        private CheckBox _customBlockEnabledCheckBox = null!;
        private ListView _customBlockedListView = null!;

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
            Icon = AppIconProvider.GetIcon();
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(420, 462);

            var tabControl = new TabControl
            {
                Location = new Point(12, 12),
                Size = new Size(396, 400)
            };

            tabControl.TabPages.Add(BuildGeneralTab());
            tabControl.TabPages.Add(BuildProcessListTab(
                "アンチチート検知",
                "アンチチート検知を有効にする",
                out _antiCheatEnabledCheckBox,
                out _antiCheatListView));
            tabControl.TabPages.Add(BuildProcessListTab(
                "カスタム無効化アプリ",
                "指定したアプリで無効化する",
                out _customBlockEnabledCheckBox,
                out _customBlockedListView));

            _okButton = new Button
            {
                Text = "保存",
                Location = new Point(210, 422),
                DialogResult = DialogResult.OK
            };
            _okButton.Click += OnSaveClicked;

            _cancelButton = new Button
            {
                Text = "キャンセル",
                Location = new Point(305, 422),
                DialogResult = DialogResult.Cancel
            };

            Controls.Add(tabControl);
            Controls.Add(_okButton);
            Controls.Add(_cancelButton);

            AcceptButton = _okButton;
            CancelButton = _cancelButton;
        }

        private TabPage BuildGeneralTab()
        {
            var page = new TabPage("全般");

            _enabledCheckBox = new CheckBox
            {
                Text = "機能を有効にする",
                Location = new Point(12, 16),
                AutoSize = true
            };

            var thresholdLabel = new Label
            {
                Text = "長押し判定時間:",
                Location = new Point(12, 54),
                AutoSize = true
            };
            _thresholdNumeric = new NumericUpDown
            {
                Location = new Point(146, 50),
                Width = 80,
                Minimum = 50,
                Maximum = 2000,
                Increment = 50,
                DecimalPlaces = 0
            };
            _thresholdUnitComboBox = new ComboBox
            {
                Location = new Point(236, 50),
                Width = 100,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _thresholdUnitComboBox.Items.Add("ミリ秒 (ms)");
            _thresholdUnitComboBox.Items.Add("秒 (s)");
            _thresholdUnitComboBox.SelectedIndexChanged += OnThresholdUnitChanged;

            var speedLabel = new Label
            {
                Text = "ホイール速度倍率:",
                Location = new Point(12, 92),
                AutoSize = true
            };
            _speedNumeric = new NumericUpDown
            {
                Location = new Point(186, 88),
                Width = 100,
                Minimum = 0.1m,
                Maximum = 5.0m,
                Increment = 0.1m,
                DecimalPlaces = 1
            };

            _autoStartCheckBox = new CheckBox
            {
                Text = "PC起動時に自動起動する",
                Location = new Point(12, 130),
                AutoSize = true
            };

            page.Controls.Add(_enabledCheckBox);
            page.Controls.Add(thresholdLabel);
            page.Controls.Add(_thresholdNumeric);
            page.Controls.Add(_thresholdUnitComboBox);
            page.Controls.Add(speedLabel);
            page.Controls.Add(_speedNumeric);
            page.Controls.Add(_autoStartCheckBox);

            return page;
        }

        // アンチチート検知タブ・カスタム無効化アプリタブは構成が同じため共通化。
        // ListView(チェックボックス付き・1行1項目)で、チェックの有無がそのまま
        // 個別ON/OFFになる。「参照...」ボタンでExplorerから.exeを直接選んで
        // 追加することもできる。
        private TabPage BuildProcessListTab(
            string tabTitle,
            string enableCheckboxText,
            out CheckBox enabledCheckBox,
            out ListView listView)
        {
            var page = new TabPage(tabTitle);

            var checkBox = new CheckBox
            {
                Text = enableCheckboxText,
                Location = new Point(12, 12),
                AutoSize = true
            };

            var listLabel = new Label
            {
                Text = "登録済みプロセス(チェックで有効/無効を切替):",
                Location = new Point(12, 40),
                AutoSize = true
            };

            var list = new ListView
            {
                Location = new Point(12, 62),
                Size = new Size(350, 178),
                View = View.Details,
                CheckBoxes = true,
                HeaderStyle = ColumnHeaderStyle.None,
                FullRowSelect = true,
                MultiSelect = true
            };
            list.Columns.Add("プロセス名", 340);

            void AddProcessName(string rawName)
            {
                string name = rawName.Trim();
                if (name.Length == 0) return;

                bool exists = list.Items.Cast<ListViewItem>()
                    .Any(item => string.Equals(item.Text, name, StringComparison.OrdinalIgnoreCase));
                if (!exists)
                {
                    list.Items.Add(new ListViewItem(name) { Checked = true });
                }
            }

            var addTextBox = new TextBox
            {
                Location = new Point(12, 250),
                Width = 178,
                PlaceholderText = "例: game.exe"
            };

            var addButton = new Button
            {
                Text = "追加",
                Location = new Point(196, 248),
                Width = 64
            };
            addButton.Click += (s, e) =>
            {
                AddProcessName(addTextBox.Text);
                addTextBox.Clear();
                addTextBox.Focus();
            };
            addTextBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    addButton.PerformClick();
                }
            };

            var browseButton = new Button
            {
                Text = "参照...",
                Location = new Point(266, 248),
                Width = 96
            };
            browseButton.Click += (s, e) =>
            {
                using var dialog = new OpenFileDialog
                {
                    Title = "無効化対象の実行ファイルを選択",
                    Filter = "実行ファイル (*.exe)|*.exe|すべてのファイル (*.*)|*.*",
                    CheckFileExists = true
                };
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    AddProcessName(Path.GetFileName(dialog.FileName));
                }
            };

            var removeButton = new Button
            {
                Text = "選択を削除",
                Location = new Point(12, 280),
                Width = 120
            };
            removeButton.Click += (s, e) =>
            {
                foreach (var item in list.SelectedItems.Cast<ListViewItem>().ToList())
                {
                    list.Items.Remove(item);
                }
            };

            page.Controls.Add(checkBox);
            page.Controls.Add(listLabel);
            page.Controls.Add(list);
            page.Controls.Add(addTextBox);
            page.Controls.Add(addButton);
            page.Controls.Add(browseButton);
            page.Controls.Add(removeButton);

            enabledCheckBox = checkBox;
            listView = list;
            return page;
        }

        private void LoadValues()
        {
            _enabledCheckBox.Checked = _settings.Enabled;
            _autoStartCheckBox.Checked = _settings.AutoStart;
            _speedNumeric.Value = (decimal)_settings.WheelSpeedMultiplier;

            _antiCheatEnabledCheckBox.Checked = _settings.AntiCheatDetectionEnabled;
            PopulateListView(_antiCheatListView, _settings.AntiCheatProcessNames);

            _customBlockEnabledCheckBox.Checked = _settings.CustomBlockEnabled;
            PopulateListView(_customBlockedListView, _settings.CustomBlockedProcessNames);

            _suppressUnitConversion = true;
            _thresholdUnitComboBox.SelectedIndex = UnitIndexMs;
            ApplyNumericRangeForUnit(UnitIndexMs);
            _thresholdNumeric.Value = Math.Min(_thresholdNumeric.Maximum,
                Math.Max(_thresholdNumeric.Minimum, _settings.LongPressThresholdMs));
            _suppressUnitConversion = false;
        }

        private static void PopulateListView(ListView listView, List<ProcessRule> rules)
        {
            listView.Items.Clear();
            foreach (var rule in rules)
            {
                if (string.IsNullOrWhiteSpace(rule.Name)) continue;
                listView.Items.Add(new ListViewItem(rule.Name) { Checked = rule.Enabled });
            }
        }

        private static List<ProcessRule> ExtractRules(ListView listView)
        {
            var result = new List<ProcessRule>();
            foreach (ListViewItem item in listView.Items)
            {
                string name = item.Text.Trim();
                if (name.Length == 0) continue;
                result.Add(new ProcessRule { Name = name, Enabled = item.Checked });
            }
            return result;
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

            _settings.AntiCheatDetectionEnabled = _antiCheatEnabledCheckBox.Checked;
            _settings.AntiCheatProcessNames = ExtractRules(_antiCheatListView);

            _settings.CustomBlockEnabled = _customBlockEnabledCheckBox.Checked;
            _settings.CustomBlockedProcessNames = ExtractRules(_customBlockedListView);

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
