using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using WzComparerR2;
using WzComparerR2.CharaSim;
using WzComparerR2.CharaSimControl;
using WzComparerR2.Common;
using WzComparerR2.Config;
using WzComparerR2.PluginBase;
using WzComparerR2.WzLib;

namespace AvatarGifTool
{
    internal sealed class MainForm : Form
    {
        private const int SearchPageSize = 50;
        private const int BaseMinimumWindowWidth = 1100;
        private const int BaseMinimumWindowHeight = 720;
        private const int PreferredWindowWidth = 1320;
        private const int PreferredWindowHeight = 860;
        private const int ParamsGearRowIndex = 3;
        private const int ParamsDyeRowIndex = 4;
        private const string DefaultTemplateText = "53065,64460,12015";
        private readonly TextBox txtBaseWz;
        private readonly AlignedInputBox txtTemplate;
        private readonly ComboBox cboMode;
        private readonly Label lblGearCaption;
        private readonly Panel pnlGear;
        private readonly AlignedInputBox txtGear;
        private readonly Label lblDyeAdjustmentsCaption;
        private readonly Panel pnlDyeAdjustments;
        private readonly TrackBar trkDyeSaturation;
        private readonly TrackBar trkDyeBrightness;
        private readonly NumericUpDown nudDyeSaturation;
        private readonly NumericUpDown nudDyeBrightness;
        private readonly TextBox txtPreview;
        private readonly AlignedInputBox txtSearch;
        private readonly CheckBox chkSearchAppearanceOnly;
        private readonly Panel pnlBackgroundColor;
        private readonly Label lblBackgroundColorValue;
        private readonly Label lblBackgroundImageValue;
        private readonly FlowLayoutPanel pnlHistoryButtons;
        private readonly CheckBox chkTransparentBackground;
        private readonly Button btnBrowseBase;
        private readonly Button btnPickBackgroundColor;
        private readonly Button btnResetBackgroundColor;
        private readonly Button btnBrowseBackgroundImage;
        private readonly Button btnClearBackgroundImage;
        private readonly Button btnValidatePreview;
        private readonly Button btnExportSettings;
        private readonly Button btnExport;
        private readonly Button btnSearch;
        private readonly Button btnPrevPage;
        private readonly Button btnNextPage;
        private readonly Button btnJumpPage;
        private readonly Label lblBaseStatus;
        private readonly Label lblSearchPager;
        private readonly Label lblStatus;
        private readonly ListView lvSearchResults;
        private readonly NumericUpDown nudSearchPage;
        private readonly ContextMenuStrip cmsSearchResults;
        private readonly ToolStripMenuItem miAddSearchResultToTemplate;
        private AfrmTooltip searchPreviewTooltip;
        private readonly TableLayoutPanel rootLayout;
        private TableLayoutPanel paramsLayout;
        private readonly System.Windows.Forms.Timer previewTimer;
        private readonly ColorDialog backgroundColorDialog;
        private readonly MetadataResolver metadataResolver;
        private readonly AppConfigStore configStore;
        private readonly AppConfig config;
        private readonly List<Button> backgroundPaletteButtons;
        private readonly List<HistoryTemplateButton> historyButtons;
        private readonly List<TemplateHistoryItem> templateHistory;
        private readonly Dictionary<string, int> templateDisplayIdMap;
        private readonly Dictionary<string, int> gearDisplayIdMap;
        private string[] normalExportActions;
        private string dyeExportAction;
        private List<AppearanceSearchResult> searchResults;
        private bool isBusy;
        private bool isSearching;
        private bool startupConfigApplied;
        private int previewVersion;
        private int currentSearchPage;
        private BaseLoadState baseLoadState;
        private bool syncingDyeAdjustmentInputs;
        private Color currentBackgroundColor;
        private Color lastOpaqueBackgroundColor;
        private string currentBackgroundImagePath;

        public MainForm()
        {
            this.Text = GetWindowTitle();
            this.AutoScaleDimensions = new SizeF(96f, 96f);
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = CreateUiFont();

            this.configStore = AppConfigStore.CreateDefault();
            this.config = this.configStore.Load();
            this.metadataResolver = new MetadataResolver();
            this.previewTimer = new System.Windows.Forms.Timer { Interval = 450 };
            this.previewTimer.Tick += this.PreviewTimer_Tick;
            this.backgroundPaletteButtons = new List<Button>();
            this.historyButtons = new List<HistoryTemplateButton>();
            this.searchResults = new List<AppearanceSearchResult>();
            this.templateHistory = NormalizeTemplateHistory(this.config.TemplateHistory);
            this.config.TemplateHistory = this.templateHistory;
            this.templateDisplayIdMap = new Dictionary<string, int>(StringComparer.Ordinal);
            this.gearDisplayIdMap = new Dictionary<string, int>(StringComparer.Ordinal);
            this.normalExportActions = Program.NormalizeNormalActionSelection(this.config.NormalExportActions);
            this.dyeExportAction = Program.NormalizeDyeActionSelection(this.config.DyeExportAction);

            this.txtBaseWz = new TextBox { Dock = DockStyle.Fill };
            this.txtTemplate = new AlignedInputBox
            {
                Dock = DockStyle.Fill,
                PlaceholderText = "在此填入外观/物品ID，使用英文逗号隔开",
                Text = DefaultTemplateText,
            };
            this.cboMode = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            this.cboMode.Items.AddRange(new object[] { "普通模式", "染色模式" });
            this.cboMode.SelectedIndex = 0;
            this.lblGearCaption = new Label { Text = "Gear", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 7, 14, 4) };
            this.pnlGear = new Panel { Dock = DockStyle.Fill, AutoSize = true };
            this.txtGear = new AlignedInputBox { Dock = DockStyle.Fill };
            this.lblDyeAdjustmentsCaption = new Label { Text = "染色微调", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 7, 14, 4) };
            this.pnlDyeAdjustments = new Panel { Dock = DockStyle.Fill, AutoSize = true };
            this.trkDyeSaturation = CreateAdjustmentTrackBar();
            this.trkDyeBrightness = CreateAdjustmentTrackBar();
            this.nudDyeSaturation = CreateAdjustmentNumericUpDown();
            this.nudDyeBrightness = CreateAdjustmentNumericUpDown();
            this.txtPreview = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Text = "请选择 Base.wz，然后填写模板。",
            };
            this.txtSearch = new AlignedInputBox
            {
                Dock = DockStyle.Fill,
                PlaceholderText = "输入中文关键字或 ID 片段",
            };
            this.chkSearchAppearanceOnly = new CheckBox
            {
                Text = "仅搜索外观道具",
                AutoSize = true,
                Checked = this.config.SearchAppearanceOnly,
                Anchor = AnchorStyles.Left,
            };
            this.btnBrowseBase = new Button { Text = "选择...", AutoSize = true };
            this.btnPickBackgroundColor = new Button { Text = "调色盘...", AutoSize = true };
            this.btnResetBackgroundColor = new Button { Text = "恢复白底", AutoSize = true };
            this.btnBrowseBackgroundImage = new Button { Text = "选择图片...", AutoSize = true };
            this.btnClearBackgroundImage = new Button { Text = "清除图片", AutoSize = true };
            this.chkTransparentBackground = new CheckBox { Text = "透明背景", AutoSize = true, Margin = new Padding(10, 7, 0, 0) };
            this.btnValidatePreview = new Button { Text = "校验", AutoSize = true };
            this.btnExportSettings = new Button { Text = "导出设置", AutoSize = true };
            this.btnExport = new Button { Text = "导出", AutoSize = true };
            this.btnSearch = new Button { Text = "搜索", AutoSize = true };
            this.btnPrevPage = new Button { Text = "上一页", AutoSize = true };
            this.btnNextPage = new Button { Text = "下一页", AutoSize = true };
            this.btnJumpPage = new Button { Text = "跳转", AutoSize = true };
            this.lblBaseStatus = new Label { AutoSize = true, Text = "未读取", Anchor = AnchorStyles.Left };
            this.lblSearchPager = new Label { AutoSize = true, Text = "0 / 0，共 0 条" };
            this.lblStatus = new Label { AutoSize = true, Text = "就绪" };
            this.lvSearchResults = new ListView
            {
                Dock = DockStyle.Fill,
                FullRowSelect = true,
                GridLines = true,
                HideSelection = false,
                MultiSelect = false,
                View = View.Details,
            };
            this.nudSearchPage = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 1,
                DecimalPlaces = 0,
                Increment = 1,
                ThousandsSeparator = false,
                TextAlign = HorizontalAlignment.Center,
                Width = this.ScaleForLogicalPixels(70),
                Margin = new Padding(0),
            };
            this.pnlBackgroundColor = new Panel
            {
                Size = new Size(this.ScaleForLogicalPixels(32), this.ScaleForLogicalPixels(20)),
                Margin = new Padding(0, 6, 8, 0),
                BorderStyle = BorderStyle.FixedSingle,
            };
            this.lblBackgroundColorValue = new Label { AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 8, 10, 0) };
            this.lblBackgroundImageValue = new Label { AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 8, 10, 0) };
            this.pnlHistoryButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Margin = new Padding(0),
                Padding = new Padding(0),
            };
            this.lvSearchResults.Columns.Add("类型", 96);
            this.lvSearchResults.Columns.Add("ID", 110);
            this.lvSearchResults.Columns.Add("名称", 420);
            this.cmsSearchResults = new ContextMenuStrip();
            this.miAddSearchResultToTemplate = new ToolStripMenuItem("添加到模板", null, this.MiAddSearchResultToTemplate_Click);
            this.cmsSearchResults.Items.Add(this.miAddSearchResultToTemplate);
            this.lvSearchResults.ContextMenuStrip = this.cmsSearchResults;
            this.backgroundColorDialog = new ColorDialog
            {
                AnyColor = true,
                FullOpen = true,
                SolidColorOnly = false,
            };
            this.InitializeHistoryButtons();

            this.trkDyeSaturation.Value = ClampAdjustmentValue(this.config.DyeSaturationOffset);
            this.trkDyeBrightness.Value = ClampAdjustmentValue(this.config.DyeBrightnessOffset);
            this.lastOpaqueBackgroundColor = Color.White;
            this.SetBackgroundColor(GetConfiguredBackgroundColor(), saveConfig: false, markPreviewDirty: false);
            this.SetBackgroundImagePath(this.config.BackgroundImagePath, saveConfig: false, markPreviewDirty: false);
            this.SyncDyeAdjustmentEditorsFromTrackBars();
            this.ApplySharedControlSizing();

            this.rootLayout = this.BuildLayout();
            this.BindEvents();
            this.UpdateBaseLoadState(BaseLoadState.NotLoaded);
            this.UpdateModeUi();
            this.RefreshHistoryButtons();
            this.UpdateSearchNavigationState();
            this.Size = new Size(PreferredWindowWidth, PreferredWindowHeight);
            this.MinimumSize = new Size(BaseMinimumWindowWidth, BaseMinimumWindowHeight);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            if (!this.startupConfigApplied)
            {
                this.ApplyStartupConfig();
                this.startupConfigApplied = true;
            }

            this.UpdateSearchColumnWidths();

            if (!string.IsNullOrWhiteSpace(this.txtBaseWz.Text))
            {
                this.BeginInvoke(new Action(() => _ = this.LoadBaseAsync(this.txtBaseWz.Text.Trim(), showFailureDialog: false)));
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            this.BeginInvoke(new Action(this.ClearInitialTemplateSelection));
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            this.UpdateSearchColumnWidths();
        }

        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            base.OnDpiChanged(e);
            this.BeginInvoke(new Action(() =>
            {
                this.UpdateMinimumWindowSize();
                this.UpdateSearchColumnWidths();
            }));
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            this.SaveConfig();
            this.previewTimer.Stop();
            this.previewTimer.Dispose();
            this.CloseSearchPreviewTooltip();
            this.metadataResolver.Dispose();
            base.OnFormClosed(e);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape && this.searchPreviewTooltip != null && !this.searchPreviewTooltip.IsDisposed && this.searchPreviewTooltip.Visible)
            {
                this.CloseSearchPreviewTooltip();
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private bool IsDyeMode => this.cboMode.SelectedIndex == 1;

        private IReadOnlyList<string> SelectedNormalExportActions => Program.NormalizeNormalActionSelection(this.normalExportActions);

        private string SelectedDyeExportAction => Program.NormalizeDyeActionSelection(this.dyeExportAction);

        private void ClearInitialTemplateSelection()
        {
            if (!string.Equals(this.txtTemplate.Text, DefaultTemplateText, StringComparison.Ordinal))
            {
                return;
            }

            this.txtTemplate.SelectionStart = 0;
            this.txtTemplate.SelectionLength = 0;
            if (this.btnBrowseBase.Enabled)
            {
                this.ActiveControl = this.btnBrowseBase;
                if (this.btnBrowseBase.CanFocus)
                {
                    this.btnBrowseBase.Focus();
                }
            }
        }

        private TrackBar CreateAdjustmentTrackBar()
        {
            return new TrackBar
            {
                Minimum = -99,
                Maximum = 99,
                TickFrequency = 11,
                SmallChange = 1,
                LargeChange = 10,
                AutoSize = false,
                Width = this.ScaleForLogicalPixels(260),
                Height = this.ScaleForLogicalPixels(36),
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0),
            };
        }

        private NumericUpDown CreateAdjustmentNumericUpDown()
        {
            return new NumericUpDown
            {
                Minimum = -99,
                Maximum = 99,
                DecimalPlaces = 0,
                Increment = 1,
                ThousandsSeparator = false,
                TextAlign = HorizontalAlignment.Center,
                Width = this.ScaleForLogicalPixels(70),
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0),
            };
        }

        private Button CreateBackgroundPaletteButton(Color color, string text = null)
        {
            var button = new Button
            {
                Width = this.ScaleForLogicalPixels(28),
                Height = this.ScaleForLogicalPixels(28),
                Margin = new Padding(0, 0, 6, 6),
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                BackColor = color.A == 0 ? Color.White : color,
                ForeColor = color.A == 0 ? SystemColors.ControlText : ControlPaint.Dark(color),
                Text = text ?? string.Empty,
                Tag = color,
            };

            button.FlatAppearance.BorderColor = SystemColors.ControlDark;
            button.FlatAppearance.MouseOverBackColor = color.A == 0 ? Color.Gainsboro : color;
            button.FlatAppearance.MouseDownBackColor = color.A == 0 ? Color.Silver : ControlPaint.Dark(color);
            button.Click += this.BackgroundPaletteButton_Click;
            this.backgroundPaletteButtons.Add(button);
            return button;
        }

        private int ClampAdjustmentValue(int value)
        {
            return Math.Max(-99, Math.Min(99, value));
        }

        private static string GetWindowTitle()
        {
            Assembly assembly = typeof(MainForm).Assembly;
            string version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? assembly.GetName().Version?.ToString()
                ?? "unknown";
            return $"AvatarGifTool v{version}";
        }

        private static Font CreateUiFont()
        {
            Font baseFont = SystemFonts.MessageBoxFont;
            float size = Math.Max(baseFont.Size + 1f, 10.5f);
            return new Font(baseFont.FontFamily, size, FontStyle.Regular, GraphicsUnit.Point);
        }

        private void ApplySharedControlSizing()
        {
            int inputHeight = this.ScaleForLogicalPixels(34);
            int buttonHeight = this.ScaleForLogicalPixels(34);
            int primaryButtonHeight = this.ScaleForLogicalPixels(38);
            int historyLineHeight = TextRenderer.MeasureText("肤", this.Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Height;
            int historyButtonWidth = this.ScaleForLogicalPixels(168);
            int historyButtonHeight = Math.Max(this.ScaleForLogicalPixels(88), (historyLineHeight * 3) + this.ScaleForLogicalPixels(20));

            ConfigureTextInput(this.txtTemplate, inputHeight);
            ConfigureTextInput(this.txtGear, inputHeight);
            ConfigureTextInput(this.txtSearch, inputHeight);

            this.cboMode.Margin = new Padding(0);
            this.cboMode.MinimumSize = new Size(0, inputHeight);
            this.cboMode.IntegralHeight = false;

            ConfigureButton(this.btnBrowseBase, this.ScaleForLogicalPixels(96), buttonHeight);
            ConfigureButton(this.btnPickBackgroundColor, this.ScaleForLogicalPixels(96), buttonHeight);
            ConfigureButton(this.btnResetBackgroundColor, this.ScaleForLogicalPixels(96), buttonHeight);
            ConfigureButton(this.btnBrowseBackgroundImage, this.ScaleForLogicalPixels(110), buttonHeight);
            ConfigureButton(this.btnClearBackgroundImage, this.ScaleForLogicalPixels(96), buttonHeight);
            ConfigureButton(this.btnValidatePreview, this.ScaleForLogicalPixels(90), buttonHeight);
            ConfigureButton(this.btnSearch, this.ScaleForLogicalPixels(90), buttonHeight);
            ConfigureButton(this.btnPrevPage, this.ScaleForLogicalPixels(92), buttonHeight);
            ConfigureButton(this.btnNextPage, this.ScaleForLogicalPixels(92), buttonHeight);
            ConfigureButton(this.btnJumpPage, this.ScaleForLogicalPixels(80), buttonHeight);
            ConfigureButton(this.btnExportSettings, this.ScaleForLogicalPixels(120), primaryButtonHeight);
            ConfigureButton(this.btnExport, this.ScaleForLogicalPixels(120), primaryButtonHeight);

            this.btnValidatePreview.Margin = new Padding(8, 0, 0, 0);
            this.btnSearch.Margin = new Padding(8, 0, 0, 0);
            this.btnPickBackgroundColor.Margin = new Padding(8, 0, 0, 0);
            this.btnResetBackgroundColor.Margin = new Padding(8, 0, 0, 0);
            this.btnBrowseBackgroundImage.Margin = new Padding(8, 0, 0, 0);
            this.btnClearBackgroundImage.Margin = new Padding(8, 0, 0, 0);
            this.btnNextPage.Margin = new Padding(8, 0, 0, 0);
            this.btnExportSettings.Margin = new Padding(0);
            this.btnExport.Margin = new Padding(0);
            this.chkSearchAppearanceOnly.Margin = new Padding(12, 7, 0, 0);

            this.nudDyeSaturation.MinimumSize = new Size(this.ScaleForLogicalPixels(76), inputHeight);
            this.nudDyeSaturation.Margin = new Padding(0, 0, 10, 0);
            this.nudDyeBrightness.MinimumSize = new Size(this.ScaleForLogicalPixels(76), inputHeight);
            this.nudDyeBrightness.Margin = new Padding(0, 0, 10, 0);
            this.nudSearchPage.MinimumSize = new Size(this.ScaleForLogicalPixels(76), inputHeight);
            this.nudSearchPage.Margin = new Padding(0);

            foreach (HistoryTemplateButton button in this.historyButtons)
            {
                button.MinimumSize = new Size(historyButtonWidth, historyButtonHeight);
                button.MaximumSize = new Size(historyButtonWidth, historyButtonHeight);
                button.Size = new Size(historyButtonWidth, historyButtonHeight);
                button.Font = this.Font;
            }
        }

        private static void ConfigureTextInput(AlignedInputBox textBox, int height)
        {
            textBox.Margin = new Padding(0);
            textBox.AutoSize = false;
            textBox.MinimumSize = new Size(0, height);
            textBox.MaximumSize = new Size(0, height);
            textBox.Height = height;
            textBox.TextAlign = HorizontalAlignment.Left;
        }

        private static void ConfigureButton(Button button, int minWidth, int minHeight)
        {
            button.Margin = new Padding(0);
            button.MinimumSize = new Size(minWidth, minHeight);
            button.AutoSize = true;
        }

        private void InitializeHistoryButtons()
        {
            for (int i = 0; i < 9; i++)
            {
                var button = new HistoryTemplateButton
                {
                    AutoSize = false,
                    Margin = new Padding(0, 0, 4, 4),
                    Padding = new Padding(8, 6, 8, 6),
                    UseMnemonic = false,
                    Visible = false,
                    Tag = null,
                };

                button.Click += this.HistoryButton_Click;
                this.historyButtons.Add(button);
                this.pnlHistoryButtons.Controls.Add(button);
            }
        }

        private static List<TemplateHistoryItem> NormalizeTemplateHistory(IEnumerable<TemplateHistoryItem> items)
        {
            var normalized = new List<TemplateHistoryItem>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (TemplateHistoryItem item in items ?? Enumerable.Empty<TemplateHistoryItem>())
            {
                if (item == null || item.Skin <= 0 || item.Face <= 0 || item.Hair <= 0)
                {
                    continue;
                }

                string key = BuildTemplateHistoryKey(item.Skin, item.Face, item.Hair);
                if (!seen.Add(key))
                {
                    continue;
                }

                normalized.Add(new TemplateHistoryItem
                {
                    Skin = item.Skin,
                    SkinName = item.SkinName,
                    Face = item.Face,
                    FaceName = item.FaceName,
                    Hair = item.Hair,
                    HairName = item.HairName,
                });

                if (normalized.Count >= 9)
                {
                    break;
                }
            }

            return normalized;
        }

        private static string BuildTemplateHistoryKey(int skin, int face, int hair)
        {
            return $"{skin}:{face}:{hair}";
        }

        private Color GetConfiguredBackgroundColor()
        {
            try
            {
                return Color.FromArgb(this.config.BackgroundColorArgb);
            }
            catch
            {
                return Color.White;
            }
        }

        private void SetBackgroundImagePath(string path, bool saveConfig, bool markPreviewDirty)
        {
            string normalizedPath = string.IsNullOrWhiteSpace(path) ? null : path.Trim();
            if (!string.IsNullOrWhiteSpace(normalizedPath) && !File.Exists(normalizedPath))
            {
                normalizedPath = null;
            }

            this.currentBackgroundImagePath = normalizedPath;

            if (this.lblBackgroundImageValue != null)
            {
                this.lblBackgroundImageValue.Text = string.IsNullOrWhiteSpace(normalizedPath)
                    ? "未使用"
                    : Path.GetFileName(normalizedPath);
            }

            this.UpdateSearchNavigationState();

            if (saveConfig)
            {
                this.SaveConfig();
            }

            if (markPreviewDirty)
            {
                this.MarkPreviewDirty();
            }
        }

        private void SetBackgroundColor(Color color, bool saveConfig, bool markPreviewDirty)
        {
            this.currentBackgroundColor = color;
            if (color.A > 0)
            {
                this.lastOpaqueBackgroundColor = Color.FromArgb(255, color);
            }

            if (this.chkTransparentBackground != null)
            {
                this.chkTransparentBackground.Checked = color.A == 0;
            }

            if (this.lblBackgroundColorValue != null)
            {
                this.lblBackgroundColorValue.Text = color.A == 0 ? "透明" : this.FormatColorHex(color);
            }

            this.pnlBackgroundColor?.Invalidate();

            if (saveConfig)
            {
                this.SaveConfig();
            }

            if (markPreviewDirty)
            {
                this.MarkPreviewDirty();
            }
        }

        private void SyncDyeAdjustmentEditorsFromTrackBars()
        {
            this.syncingDyeAdjustmentInputs = true;
            try
            {
                this.nudDyeSaturation.Value = this.trkDyeSaturation.Value;
                this.nudDyeBrightness.Value = this.trkDyeBrightness.Value;
            }
            finally
            {
                this.syncingDyeAdjustmentInputs = false;
            }
        }

        private string FormatColorHex(Color color)
        {
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        private int ScaleForLogicalPixels(int logicalPixels)
        {
            int dpi = this.DeviceDpi > 0 ? this.DeviceDpi : 96;
            return (int)Math.Round(logicalPixels * dpi / 96f);
        }

        private TableLayoutPanel BuildLayout()
        {
            var outer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(8),
            };
            outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            var paramsGroup = new GroupBox
            {
                Text = "参数",
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
            };

            var searchGroup = new GroupBox { Text = "ID 搜索", Dock = DockStyle.Fill };

            outer.Controls.Add(paramsGroup, 0, 0);
            outer.Controls.Add(searchGroup, 0, 1);

            this.paramsLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 8,
                Padding = new Padding(12, 8, 12, 8),
                Margin = new Padding(0),
            };
            this.paramsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            this.paramsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            for (int i = 0; i < 8; i++)
            {
                this.paramsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            }
            paramsGroup.Controls.Add(this.paramsLayout);

            var baseRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 6),
            };
            baseRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            baseRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            baseRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            this.lblBaseStatus.Margin = new Padding(12, 7, 12, 0);
            baseRow.Controls.Add(this.btnBrowseBase, 0, 0);
            baseRow.Controls.Add(this.lblBaseStatus, 1, 0);
            baseRow.Controls.Add(new Label
            {
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                ForeColor = SystemColors.GrayText,
                Margin = new Padding(0, 7, 0, 0),
                Text = "需要点选游戏目录下Data/Base/Base.wz",
            }, 2, 0);

            this.paramsLayout.Controls.Add(new Label { Text = "Base 文件", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 7, 14, 8) }, 0, 0);
            this.paramsLayout.Controls.Add(baseRow, 1, 0);

            var templateInputRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 8),
            };
            templateInputRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            templateInputRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            templateInputRow.Controls.Add(this.txtTemplate, 0, 0);
            templateInputRow.Controls.Add(this.btnValidatePreview, 1, 0);

            this.paramsLayout.Controls.Add(new Label { Text = "模板", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 7, 14, 4) }, 0, 1);
            this.paramsLayout.Controls.Add(templateInputRow, 1, 1);

            this.cboMode.Margin = new Padding(0, 0, 0, 6);
            this.paramsLayout.Controls.Add(new Label { Text = "模式", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 7, 14, 4) }, 0, 2);
            this.paramsLayout.Controls.Add(this.cboMode, 1, 2);

            var gearLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                AutoSize = true,
                Margin = new Padding(0),
            };
            gearLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            gearLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            gearLayout.Controls.Add(this.txtGear, 0, 0);
            gearLayout.Controls.Add(new Label { Text = "染色时必填", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(10, 0, 0, 0) }, 1, 0);
            this.pnlGear.Controls.Add(gearLayout);
            this.pnlGear.Margin = new Padding(0, 0, 0, 6);
            this.paramsLayout.Controls.Add(this.lblGearCaption, 0, 3);
            this.paramsLayout.Controls.Add(this.pnlGear, 1, 3);

            var dyeLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 2,
                AutoSize = true,
                Margin = new Padding(0),
            };
            dyeLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            dyeLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            dyeLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            dyeLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            dyeLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            dyeLayout.Controls.Add(new Label { Text = "饱和度", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 0, 8, 0) }, 0, 0);
            dyeLayout.Controls.Add(this.nudDyeSaturation, 1, 0);
            dyeLayout.Controls.Add(this.trkDyeSaturation, 2, 0);
            dyeLayout.Controls.Add(new Label { Text = "亮度", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 0, 8, 0) }, 0, 1);
            dyeLayout.Controls.Add(this.nudDyeBrightness, 1, 1);
            dyeLayout.Controls.Add(this.trkDyeBrightness, 2, 1);
            this.pnlDyeAdjustments.Controls.Add(dyeLayout);
            this.pnlDyeAdjustments.Margin = new Padding(0, 0, 0, 6);
            this.paramsLayout.Controls.Add(this.lblDyeAdjustmentsCaption, 0, 4);
            this.paramsLayout.Controls.Add(this.pnlDyeAdjustments, 1, 4);

            var backgroundTopRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0),
            };
            backgroundTopRow.Controls.Add(this.pnlBackgroundColor);
            backgroundTopRow.Controls.Add(this.lblBackgroundColorValue);
            backgroundTopRow.Controls.Add(this.btnPickBackgroundColor);
            backgroundTopRow.Controls.Add(this.btnResetBackgroundColor);
            backgroundTopRow.Controls.Add(this.chkTransparentBackground);

            var paletteRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Margin = new Padding(0, 6, 0, 0),
            };
            foreach (Button button in new[]
            {
                this.CreateBackgroundPaletteButton(Color.White),
                this.CreateBackgroundPaletteButton(Color.Gainsboro),
                this.CreateBackgroundPaletteButton(Color.Silver),
                this.CreateBackgroundPaletteButton(Color.Gray),
                this.CreateBackgroundPaletteButton(Color.DimGray),
                this.CreateBackgroundPaletteButton(Color.Black),
                this.CreateBackgroundPaletteButton(Color.FromArgb(255, 235, 235)),
                this.CreateBackgroundPaletteButton(Color.FromArgb(255, 232, 204)),
                this.CreateBackgroundPaletteButton(Color.FromArgb(255, 248, 200)),
                this.CreateBackgroundPaletteButton(Color.FromArgb(226, 244, 214)),
                this.CreateBackgroundPaletteButton(Color.FromArgb(214, 240, 244)),
                this.CreateBackgroundPaletteButton(Color.FromArgb(219, 227, 255)),
                this.CreateBackgroundPaletteButton(Color.FromArgb(243, 221, 255)),
                this.CreateBackgroundPaletteButton(Color.FromArgb(255, 220, 236)),
            })
            {
                paletteRow.Controls.Add(button);
            }

            var backgroundLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                AutoSize = true,
                Margin = new Padding(0),
            };
            backgroundLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            backgroundLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            backgroundLayout.Controls.Add(backgroundTopRow, 0, 0);
            backgroundLayout.Controls.Add(paletteRow, 0, 1);
            backgroundLayout.Margin = new Padding(0, 0, 0, 8);
            this.paramsLayout.Controls.Add(new Label { Text = "GIF 背景", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 7, 14, 8) }, 0, 5);
            this.paramsLayout.Controls.Add(backgroundLayout, 1, 5);

            var backgroundImageRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0),
            };
            backgroundImageRow.Controls.Add(this.lblBackgroundImageValue);
            backgroundImageRow.Controls.Add(this.btnBrowseBackgroundImage);
            backgroundImageRow.Controls.Add(this.btnClearBackgroundImage);
            backgroundImageRow.Controls.Add(new Label
            {
                Text = "PNG/JPG，分辨率不足时自动放大裁剪",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                ForeColor = SystemColors.GrayText,
                Margin = new Padding(10, 8, 0, 0),
            });

            var historyGroup = new GroupBox
            {
                Text = "历史搭配",
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(10, 0, 0, 0),
                Padding = new Padding(8, 16, 8, 6),
                MinimumSize = new Size(this.ScaleForLogicalPixels(220), 0),
            };
            historyGroup.Controls.Add(this.pnlHistoryButtons);

            var backgroundImageLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                AutoSize = true,
                Margin = new Padding(0),
            };
            backgroundImageLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            backgroundImageLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            backgroundImageLayout.Controls.Add(backgroundImageRow, 0, 0);
            backgroundImageLayout.Controls.Add(historyGroup, 1, 0);

            backgroundImageLayout.Margin = new Padding(0, 0, 0, 8);
            this.paramsLayout.Controls.Add(new Label { Text = "背景图片", AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Top, Margin = new Padding(0, 8, 14, 0) }, 0, 6);
            this.paramsLayout.Controls.Add(backgroundImageLayout, 1, 6);

            var actionRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, 4, 0, 0),
            };
            actionRow.Controls.Add(this.btnExportSettings);
            actionRow.Controls.Add(new Panel
            {
                Width = this.ScaleForLogicalPixels(8),
                Height = this.btnExport.MinimumSize.Height,
                Margin = new Padding(0),
            });
            actionRow.Controls.Add(this.btnExport);
            this.paramsLayout.Controls.Add(actionRow, 1, 7);

            var searchLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(12, 8, 12, 10),
            };
            searchLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            searchLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            searchLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            searchGroup.Controls.Add(searchLayout);

            var searchBar = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                AutoSize = true,
                Margin = new Padding(0),
            };
            searchBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            searchBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            searchBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            searchBar.Controls.Add(this.txtSearch, 0, 0);
            searchBar.Controls.Add(this.btnSearch, 1, 0);
            searchBar.Controls.Add(this.chkSearchAppearanceOnly, 2, 0);

            searchLayout.Controls.Add(searchBar, 0, 0);

            searchLayout.Controls.Add(this.lvSearchResults, 0, 1);

            int pagerButtonHeight = Math.Max(
                this.btnPrevPage.GetPreferredSize(Size.Empty).Height,
                this.btnNextPage.GetPreferredSize(Size.Empty).Height);
            int pagerInputHeight = Math.Max(this.nudSearchPage.PreferredHeight, this.nudSearchPage.MinimumSize.Height);
            int pagerRowHeight = Math.Max(pagerButtonHeight, pagerInputHeight);

            var pagerLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 3,
                RowCount = 1,
                AutoSize = true,
                Margin = new Padding(0, 8, 0, 0),
            };
            pagerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            pagerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pagerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pagerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, pagerRowHeight));

            var pagerLeft = new TableLayoutPanel
            {
                AutoSize = true,
                ColumnCount = 3,
                RowCount = 1,
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
            };
            pagerLeft.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pagerLeft.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pagerLeft.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pagerLeft.RowStyles.Add(new RowStyle(SizeType.Absolute, pagerRowHeight));
            this.lblSearchPager.Margin = new Padding(12, 0, 0, 0);
            this.lblSearchPager.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
            this.lblSearchPager.TextAlign = ContentAlignment.MiddleLeft;
            this.btnPrevPage.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
            this.btnNextPage.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
            pagerLeft.Controls.Add(this.btnPrevPage, 0, 0);
            pagerLeft.Controls.Add(this.btnNextPage, 1, 0);
            pagerLeft.Controls.Add(this.lblSearchPager, 2, 0);

            var pagerRight = new TableLayoutPanel
            {
                AutoSize = true,
                ColumnCount = 4,
                RowCount = 1,
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
            };
            pagerRight.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pagerRight.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pagerRight.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pagerRight.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pagerRight.RowStyles.Add(new RowStyle(SizeType.Absolute, pagerRowHeight));
            var lblJumpTo = new Label
            {
                Text = "跳到",
                AutoSize = true,
                Anchor = AnchorStyles.Left | AnchorStyles.Bottom,
                Margin = new Padding(0, 0, 6, 0),
                TextAlign = ContentAlignment.MiddleLeft,
            };
            this.nudSearchPage.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
            var lblPageUnit = new Label
            {
                Text = "页",
                AutoSize = true,
                Anchor = AnchorStyles.Left | AnchorStyles.Bottom,
                Margin = new Padding(6, 0, 6, 0),
                TextAlign = ContentAlignment.MiddleLeft,
            };
            this.btnJumpPage.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
            pagerRight.Controls.Add(lblJumpTo, 0, 0);
            pagerRight.Controls.Add(this.nudSearchPage, 1, 0);
            pagerRight.Controls.Add(lblPageUnit, 2, 0);
            pagerRight.Controls.Add(this.btnJumpPage, 3, 0);

            pagerLayout.Controls.Add(pagerLeft, 0, 0);
            pagerLayout.Controls.Add(new Panel { Dock = DockStyle.Fill, Margin = new Padding(0), Width = 1 }, 1, 0);
            pagerLayout.Controls.Add(pagerRight, 2, 0);
            searchLayout.Controls.Add(pagerLayout, 0, 2);

            this.AcceptButton = this.btnValidatePreview;
            this.Controls.Add(outer);
            return outer;
        }

        private void BindEvents()
        {
            this.txtBaseWz.TextChanged += this.InputControlChanged;
            this.txtTemplate.TextChanged += this.InputControlChanged;
            this.txtGear.TextChanged += this.InputControlChanged;
            this.trkDyeSaturation.ValueChanged += this.DyeAdjustmentControl_ValueChanged;
            this.trkDyeBrightness.ValueChanged += this.DyeAdjustmentControl_ValueChanged;
            this.nudDyeSaturation.ValueChanged += this.NudDyeAdjustment_ValueChanged;
            this.nudDyeBrightness.ValueChanged += this.NudDyeAdjustment_ValueChanged;
            this.cboMode.SelectedIndexChanged += this.CboMode_SelectedIndexChanged;
            this.btnBrowseBase.Click += this.BtnBrowseBase_Click;
            this.btnPickBackgroundColor.Click += this.BtnPickBackgroundColor_Click;
            this.btnResetBackgroundColor.Click += this.BtnResetBackgroundColor_Click;
            this.btnBrowseBackgroundImage.Click += this.BtnBrowseBackgroundImage_Click;
            this.btnClearBackgroundImage.Click += this.BtnClearBackgroundImage_Click;
            this.btnValidatePreview.Click += this.BtnValidatePreview_Click;
            this.btnExportSettings.Click += this.BtnExportSettings_Click;
            this.btnExport.Click += this.BtnExport_Click;
            this.btnSearch.Click += this.BtnSearch_Click;
            this.chkSearchAppearanceOnly.CheckedChanged += this.ChkSearchAppearanceOnly_CheckedChanged;
            this.btnPrevPage.Click += this.BtnPrevPage_Click;
            this.btnNextPage.Click += this.BtnNextPage_Click;
            this.btnJumpPage.Click += this.BtnJumpPage_Click;
            this.chkTransparentBackground.CheckedChanged += this.ChkTransparentBackground_CheckedChanged;
            this.pnlBackgroundColor.Paint += this.PnlBackgroundColor_Paint;
            this.txtSearch.KeyDown += this.TxtSearch_KeyDown;
            this.txtSearch.Enter += this.TxtSearch_Enter;
            this.txtSearch.Leave += this.TxtSearch_Leave;
            this.nudSearchPage.KeyDown += this.NudSearchPage_KeyDown;
            this.lvSearchResults.MouseDown += this.LvSearchResults_MouseDown;
            this.lvSearchResults.DoubleClick += this.LvSearchResults_DoubleClick;
            this.cmsSearchResults.Opening += this.CmsSearchResults_Opening;
            this.ResizeEnd += this.MainForm_ResizeEnd;
        }

        private void ApplyStartupConfig()
        {
            this.UpdateMinimumWindowSize();

            Rectangle workingArea = Screen.FromHandle(this.Handle).WorkingArea;
            this.Size = this.GetStartupSize(workingArea);
            this.CenterToWorkingArea(workingArea);

            if (!string.IsNullOrWhiteSpace(this.config.BaseWzPath))
            {
                this.txtBaseWz.Text = this.config.BaseWzPath;
            }
        }

        private void UpdateMinimumWindowSize()
        {
            if (!this.IsHandleCreated)
            {
                return;
            }

            this.rootLayout.PerformLayout();
            this.PerformLayout();

            Size frameSize = new Size(this.Width - this.ClientSize.Width, this.Height - this.ClientSize.Height);
            Size preferredClient = this.rootLayout.GetPreferredSize(Size.Empty);
            this.MinimumSize = new Size(
                Math.Max(this.ScaleForDpi(BaseMinimumWindowWidth), preferredClient.Width + frameSize.Width + 24),
                Math.Max(this.ScaleForDpi(BaseMinimumWindowHeight), preferredClient.Height + frameSize.Height + 24));
        }

        private Size GetStartupSize(Rectangle workingArea)
        {
            int maxWidth = Math.Max(this.MinimumSize.Width, workingArea.Width - 40);
            int maxHeight = Math.Max(this.MinimumSize.Height, workingArea.Height - 40);

            if (this.config.WindowWidth > 0 && this.config.WindowHeight > 0)
            {
                return new Size(
                    Math.Min(Math.Max(this.MinimumSize.Width, this.config.WindowWidth), maxWidth),
                    Math.Min(Math.Max(this.MinimumSize.Height, this.config.WindowHeight), maxHeight));
            }

            int defaultWidth = Math.Min(maxWidth, Math.Max(this.MinimumSize.Width, (int)(workingArea.Width * 0.82)));
            int defaultHeight = Math.Min(maxHeight, Math.Max(this.MinimumSize.Height, (int)(workingArea.Height * 0.84)));
            return new Size(defaultWidth, defaultHeight);
        }

        private void CenterToWorkingArea(Rectangle workingArea)
        {
            int x = workingArea.Left + Math.Max(0, (workingArea.Width - this.Width) / 2);
            int y = workingArea.Top + Math.Max(0, (workingArea.Height - this.Height) / 2);
            this.Location = new Point(x, y);
        }

        private void UpdateSearchColumnWidths()
        {
            if (this.lvSearchResults == null
                || this.lvSearchResults.IsDisposed
                || this.lvSearchResults.Columns == null
                || this.lvSearchResults.Columns.Count < 3)
            {
                return;
            }

            int width = this.lvSearchResults.ClientSize.Width;
            if (width <= 0)
            {
                return;
            }

            int kindWidth = this.ScaleForDpi(90);
            int idWidth = this.ScaleForDpi(110);
            int nameWidth = Math.Max(this.ScaleForDpi(220), width - kindWidth - idWidth - this.ScaleForDpi(8));

            this.lvSearchResults.Columns[0].Width = kindWidth;
            this.lvSearchResults.Columns[1].Width = idWidth;
            this.lvSearchResults.Columns[2].Width = nameWidth;
        }

        private int ScaleForDpi(int logicalPixels)
        {
            return (int)Math.Round(logicalPixels * this.DeviceDpi / 96f);
        }

        private async void BtnBrowseBase_Click(object sender, EventArgs e)
        {
            try
            {
                if (this.baseLoadState == BaseLoadState.Loading)
                {
                    return;
                }

                if (this.baseLoadState == BaseLoadState.Loaded)
                {
                    DialogResult result = MessageBox.Show(
                        this,
                        "目前已经加载到一个 Base，是否确定再次选择其他 Base？",
                        "确认",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (result != DialogResult.Yes)
                    {
                        return;
                    }
                }

                using var dialog = new OpenFileDialog
                {
                    Title = "选择 Base.wz",
                    Filter = "Base.wz|Base.wz|WZ 文件|*.wz|所有文件|*.*",
                    CheckFileExists = true,
                    RestoreDirectory = true,
                    Multiselect = false,
                };

                this.ConfigureBaseDialogPath(dialog, this.txtBaseWz.Text);

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    await this.LoadBaseAsync(dialog.FileName, showFailureDialog: true);
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Write(ex, "MainForm.BtnBrowseBase_Click");
                MessageBox.Show(this, $"打开文件选择框失败：\r\n{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ConfigureBaseDialogPath(OpenFileDialog dialog, string currentPath)
        {
            string path = currentPath?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(path))
            {
                dialog.FileName = "Base.wz";
                return;
            }

            if (File.Exists(path))
            {
                dialog.InitialDirectory = Path.GetDirectoryName(path);
                dialog.FileName = Path.GetFileName(path);
                return;
            }

            if (Directory.Exists(path))
            {
                dialog.InitialDirectory = path;
                dialog.FileName = "Base.wz";
                return;
            }

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                dialog.InitialDirectory = directory;
                dialog.FileName = Path.GetFileName(path);
                return;
            }

            dialog.FileName = "Base.wz";
        }

        private void CboMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.UpdateModeUi();
            this.MarkPreviewDirty();
        }

        private void UpdateModeUi()
        {
            bool dyeMode = this.IsDyeMode;

            this.lblGearCaption.Visible = dyeMode;
            this.pnlGear.Visible = dyeMode;
            this.lblDyeAdjustmentsCaption.Visible = dyeMode;
            this.pnlDyeAdjustments.Visible = dyeMode;
            this.txtGear.Enabled = dyeMode && !this.isBusy;

            if (this.paramsLayout != null && this.paramsLayout.RowStyles.Count > ParamsDyeRowIndex)
            {
                this.paramsLayout.RowStyles[ParamsGearRowIndex].SizeType = dyeMode ? SizeType.AutoSize : SizeType.Absolute;
                this.paramsLayout.RowStyles[ParamsGearRowIndex].Height = dyeMode ? 0f : 0f;
                this.paramsLayout.RowStyles[ParamsDyeRowIndex].SizeType = dyeMode ? SizeType.AutoSize : SizeType.Absolute;
                this.paramsLayout.RowStyles[ParamsDyeRowIndex].Height = dyeMode ? 0f : 0f;
                this.paramsLayout.PerformLayout();
            }

            this.rootLayout?.PerformLayout();
        }

        private void InputControlChanged(object sender, EventArgs e)
        {
            if (ReferenceEquals(sender, this.txtBaseWz))
            {
                this.SaveConfig();
            }

            this.MarkPreviewDirty();
        }

        private void MainForm_ResizeEnd(object sender, EventArgs e)
        {
            this.SaveConfig();
        }

        private void DyeAdjustmentControl_ValueChanged(object sender, EventArgs e)
        {
            this.SyncDyeAdjustmentEditorsFromTrackBars();
            this.SaveConfig();
            this.MarkPreviewDirty();
        }

        private void NudDyeAdjustment_ValueChanged(object sender, EventArgs e)
        {
            if (this.syncingDyeAdjustmentInputs)
            {
                return;
            }

            this.syncingDyeAdjustmentInputs = true;
            try
            {
                if (ReferenceEquals(sender, this.nudDyeSaturation))
                {
                    this.trkDyeSaturation.Value = (int)this.nudDyeSaturation.Value;
                }
                else if (ReferenceEquals(sender, this.nudDyeBrightness))
                {
                    this.trkDyeBrightness.Value = (int)this.nudDyeBrightness.Value;
                }
            }
            finally
            {
                this.syncingDyeAdjustmentInputs = false;
            }

            this.SaveConfig();
            this.MarkPreviewDirty();
        }

        private void BtnPickBackgroundColor_Click(object sender, EventArgs e)
        {
            this.backgroundColorDialog.Color = this.currentBackgroundColor.A == 0
                ? this.lastOpaqueBackgroundColor
                : Color.FromArgb(255, this.currentBackgroundColor);

            if (this.backgroundColorDialog.ShowDialog(this) == DialogResult.OK)
            {
                this.SetBackgroundColor(Color.FromArgb(255, this.backgroundColorDialog.Color), saveConfig: true, markPreviewDirty: true);
            }
        }

        private void BtnResetBackgroundColor_Click(object sender, EventArgs e)
        {
            this.SetBackgroundColor(Color.White, saveConfig: true, markPreviewDirty: true);
        }

        private void BtnBrowseBackgroundImage_Click(object sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Title = "选择背景图片",
                Filter = "图片文件|*.png;*.jpg;*.jpeg|PNG 图片|*.png|JPEG 图片|*.jpg;*.jpeg|所有文件|*.*",
                CheckFileExists = true,
                RestoreDirectory = true,
                Multiselect = false,
            };

            string currentPath = this.currentBackgroundImagePath;
            if (!string.IsNullOrWhiteSpace(currentPath) && File.Exists(currentPath))
            {
                dialog.InitialDirectory = Path.GetDirectoryName(currentPath);
                dialog.FileName = Path.GetFileName(currentPath);
            }

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                this.SetBackgroundImagePath(dialog.FileName, saveConfig: true, markPreviewDirty: true);
            }
        }

        private void BtnClearBackgroundImage_Click(object sender, EventArgs e)
        {
            this.SetBackgroundImagePath(null, saveConfig: true, markPreviewDirty: true);
        }

        private void ChkTransparentBackground_CheckedChanged(object sender, EventArgs e)
        {
            if (this.chkTransparentBackground.Checked)
            {
                this.SetBackgroundColor(Color.FromArgb(0, this.lastOpaqueBackgroundColor), saveConfig: true, markPreviewDirty: true);
            }
            else if (this.currentBackgroundColor.A == 0)
            {
                this.SetBackgroundColor(this.lastOpaqueBackgroundColor, saveConfig: true, markPreviewDirty: true);
            }
        }

        private void BackgroundPaletteButton_Click(object sender, EventArgs e)
        {
            if (sender is Button button && button.Tag is Color color)
            {
                this.SetBackgroundColor(color.A == 0 ? Color.Transparent : Color.FromArgb(255, color), saveConfig: true, markPreviewDirty: true);
            }
        }

        private void PnlBackgroundColor_Paint(object sender, PaintEventArgs e)
        {
            Rectangle rect = this.pnlBackgroundColor.ClientRectangle;
            if (rect.Width <= 0 || rect.Height <= 0)
            {
                return;
            }

            using (var hatch = new HatchBrush(HatchStyle.LargeCheckerBoard, Color.LightGray, Color.White))
            {
                e.Graphics.FillRectangle(hatch, rect);
            }

            if (this.currentBackgroundColor.A > 0)
            {
                using var fill = new SolidBrush(this.currentBackgroundColor);
                e.Graphics.FillRectangle(fill, rect);
            }

            if (this.currentBackgroundColor.A == 0)
            {
                using var pen = new Pen(Color.Firebrick, this.ScaleForLogicalPixels(2));
                e.Graphics.DrawLine(pen, rect.Left + 2, rect.Bottom - 3, rect.Right - 3, rect.Top + 2);
            }
        }

        private void MarkPreviewDirty()
        {
            if (this.isBusy || this.isSearching || this.baseLoadState == BaseLoadState.Loading)
            {
                return;
            }

            if (this.baseLoadState != BaseLoadState.Loaded)
            {
                this.txtPreview.Text = string.IsNullOrWhiteSpace(this.txtBaseWz.Text)
                    ? "请选择 Base.wz。"
                    : "Base.wz 尚未加载。";
                return;
            }

            if (string.IsNullOrWhiteSpace(this.txtTemplate.Text))
            {
                this.txtPreview.Text = "请输入模板。";
                return;
            }

            this.txtPreview.Text = "参数已变更，请点击校验。";
        }

        private async void BtnValidatePreview_Click(object sender, EventArgs e)
        {
            if (this.isBusy || this.isSearching || !this.EnsureBaseReady("校验"))
            {
                return;
            }

            int requestVersion = ++this.previewVersion;
            await this.RefreshPreviewAsync(requestVersion);
        }

        private async void PreviewTimer_Tick(object sender, EventArgs e)
        {
            this.previewTimer.Stop();
            if (this.isBusy || this.isSearching || !this.IsBaseLoaded)
            {
                return;
            }

            int requestVersion = this.previewVersion;
            await this.RefreshPreviewAsync(requestVersion);
        }

        private async Task RefreshPreviewAsync(int requestVersion)
        {
            string baseWzPath = this.txtBaseWz.Text.Trim();
            string rawTemplateText = this.txtTemplate.Text.Trim();

            if (!this.IsBaseLoaded)
            {
                this.txtPreview.Text = "请先加载 Base.wz。";
                return;
            }

            if (string.IsNullOrWhiteSpace(rawTemplateText))
            {
                this.txtPreview.Text = "请输入模板。";
                return;
            }

            this.txtPreview.Text = "正在解析名称...";

            try
            {
                string template = this.BuildTemplateIdText();
                string gearText = this.BuildGearIdText();
                ResolvedAppearancePreview preview = await Task.Run(() =>
                    this.metadataResolver.Resolve(baseWzPath, template, gearText, this.IsDyeMode, this.SelectedNormalExportActions, this.SelectedDyeExportAction));

                if (requestVersion != this.previewVersion || this.isBusy || this.isSearching)
                {
                    return;
                }

                this.txtPreview.Text = preview.PreviewText;
                this.RememberTemplateHistory(preview);
            }
            catch (Exception ex)
            {
                if (requestVersion != this.previewVersion || this.isBusy || this.isSearching)
                {
                    return;
                }

                ErrorLog.Write(ex, "MainForm.RefreshPreviewAsync");
                this.txtPreview.Text = ex.Message;
            }
        }

        private void BtnExportSettings_Click(object sender, EventArgs e)
        {
            using var dialog = new ExportSettingsForm(this.SelectedNormalExportActions, this.SelectedDyeExportAction);
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            this.normalExportActions = Program.NormalizeNormalActionSelection(dialog.SelectedNormalActions);
            this.dyeExportAction = Program.NormalizeDyeActionSelection(dialog.SelectedDyeAction);
            this.SaveConfig();
            this.MarkPreviewDirty();
        }

        private async void BtnExport_Click(object sender, EventArgs e)
        {
            if (this.isBusy || this.isSearching || !this.EnsureBaseReady("导出"))
            {
                return;
            }

            try
            {
                this.SetBusy(true);
                this.lblStatus.Text = "正在准备导出...";

                string baseWzPath = this.txtBaseWz.Text.Trim();
                string template = this.BuildTemplateIdText();
                string gearText = this.BuildGearIdText();

                ResolvedAppearancePreview preview = await Task.Run(() =>
                    this.metadataResolver.Resolve(baseWzPath, template, gearText, this.IsDyeMode, this.SelectedNormalExportActions, this.SelectedDyeExportAction));

                this.txtPreview.Text = preview.PreviewText;
                this.RememberTemplateHistory(preview);

                if (!preview.CanExport)
                {
                    throw new InvalidOperationException(preview.ExportBlockReason ?? "当前模板还不能导出。");
                }

                Program.CommandOptions options = Program.CommandOptions.Create(
                    baseWzPath,
                    template,
                    preview.OutputPath,
                    this.IsDyeMode,
                    gearText,
                    saturationOffset: this.trkDyeSaturation.Value,
                    brightnessOffset: this.trkDyeBrightness.Value,
                    backgroundColor: this.currentBackgroundColor,
                    backgroundImagePath: this.currentBackgroundImagePath,
                    normalActions: this.SelectedNormalExportActions,
                    dyeAction: this.SelectedDyeExportAction);

                this.lblStatus.Text = "正在导出 GIF...";
                string outputPath = await Task.Run(() => Program.Execute(options, manageWzContext: false));

                this.lblStatus.Text = $"导出完成：{outputPath}";
                MessageBox.Show(this, $"导出完成：\r\n{outputPath}", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                ErrorLog.Write(ex, "MainForm.BtnExport_Click");
                this.lblStatus.Text = "导出失败";
                MessageBox.Show(this, ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                this.SetBusy(false);
            }
        }

        private async void BtnSearch_Click(object sender, EventArgs e)
        {
            if (this.isBusy || this.isSearching || !this.EnsureBaseReady("搜索"))
            {
                return;
            }

            string query = this.txtSearch.Text.Trim();
            if (string.IsNullOrWhiteSpace(query))
            {
                this.searchResults = new List<AppearanceSearchResult>();
                this.currentSearchPage = 0;
                this.BindSearchResults();
                this.lblStatus.Text = "请输入搜索关键字。";
                return;
            }

            try
            {
                this.isSearching = true;
                this.previewTimer.Stop();
                this.UpdateSearchNavigationState();
                this.lblStatus.Text = "正在搜索...";

                string baseWzPath = this.txtBaseWz.Text.Trim();
                bool appearanceOnly = this.chkSearchAppearanceOnly.Checked;
                List<AppearanceSearchResult> results = await Task.Run(() =>
                    this.metadataResolver.Search(baseWzPath, query, appearanceOnly));

                this.searchResults = results;
                this.currentSearchPage = 0;
                this.BindSearchResults();
                this.lblStatus.Text = $"搜索完成，共 {results.Count} 条。";
            }
            catch (Exception ex)
            {
                ErrorLog.Write(ex, "MainForm.BtnSearch_Click");
                this.lblStatus.Text = "搜索失败";
                MessageBox.Show(this, ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                this.isSearching = false;
                this.UpdateSearchNavigationState();
            }
        }

        private void ChkSearchAppearanceOnly_CheckedChanged(object sender, EventArgs e)
        {
            this.config.SearchAppearanceOnly = this.chkSearchAppearanceOnly.Checked;
            this.SaveConfig();
        }

        private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                this.BtnSearch_Click(sender, EventArgs.Empty);
            }
        }

        private void TxtSearch_Enter(object sender, EventArgs e)
        {
            this.AcceptButton = this.btnSearch;
        }

        private void TxtSearch_Leave(object sender, EventArgs e)
        {
            this.AcceptButton = this.btnValidatePreview;
        }

        private void NudSearchPage_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                this.BtnJumpPage_Click(sender, EventArgs.Empty);
            }
        }

        private void BtnPrevPage_Click(object sender, EventArgs e)
        {
            if (this.currentSearchPage <= 0)
            {
                return;
            }

            this.currentSearchPage--;
            this.BindSearchResults();
        }

        private void BtnNextPage_Click(object sender, EventArgs e)
        {
            int pageCount = this.GetSearchPageCount();
            if (this.currentSearchPage + 1 >= pageCount)
            {
                return;
            }

            this.currentSearchPage++;
            this.BindSearchResults();
        }

        private void BtnJumpPage_Click(object sender, EventArgs e)
        {
            int pageCount = this.GetSearchPageCount();
            if (pageCount <= 0)
            {
                return;
            }

            int requestedPage = (int)this.nudSearchPage.Value;
            int zeroBasedPage = Math.Max(0, Math.Min(pageCount - 1, requestedPage - 1));
            if (zeroBasedPage == this.currentSearchPage)
            {
                return;
            }

            this.currentSearchPage = zeroBasedPage;
            this.BindSearchResults();
        }

        private async void LvSearchResults_DoubleClick(object sender, EventArgs e)
        {
            if (this.isBusy || this.isSearching || !this.IsBaseLoaded)
            {
                return;
            }

            if (!this.TryGetSelectedSearchResult(out AppearanceSearchResult result))
            {
                return;
            }

            try
            {
                this.UseWaitCursor = true;
                this.CloseSearchPreviewTooltip();
                if (result.Kind == Program.AppearanceIdKind.Gear)
                {
                    Gear gear = await Task.Run(() =>
                        this.metadataResolver.LoadTooltipGear(this.txtBaseWz.Text.Trim(), result));
                    if (gear != null)
                    {
                        this.ShowOriginalTooltip(result, gear);
                        return;
                    }
                }
                else if (result.Kind == Program.AppearanceIdKind.Item)
                {
                    Item item = await Task.Run(() =>
                        this.metadataResolver.LoadTooltipItem(this.txtBaseWz.Text.Trim(), result));
                    if (item != null)
                    {
                        this.ShowOriginalTooltip(result, item);
                        return;
                    }
                }

                SearchPreviewImage previewImage = await Task.Run(() =>
                    this.metadataResolver.LoadPreviewImage(this.txtBaseWz.Text.Trim(), result));
                if (previewImage?.Bitmap == null)
                {
                    MessageBox.Show(this, "该结果没有可用的预览。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using (DpiAwarenessScope.EnterReferencePreviewMode())
                using (var form = new PreviewImageForm(result, previewImage))
                {
                    form.ShowDialog(this);
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Write(ex, "MainForm.LvSearchResults_DoubleClick");
                MessageBox.Show(this, ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                this.UseWaitCursor = false;
            }
        }

        private void ShowOriginalTooltip(AppearanceSearchResult result, object targetItem)
        {
            this.CloseSearchPreviewTooltip();
            int itemId = targetItem switch
            {
                Gear gear => gear.ItemID,
                Item item => item.ItemID,
                _ => result.Id,
            };

            using var dpiScope = DpiAwarenessScope.EnterReferencePreviewMode();
            var tooltip = (AfrmTooltip)null;
            try
            {
                tooltip = new AfrmTooltip
                {
                    Visible = false,
                    HideOnHover = false,
                    ShowMenu = true,
                    TargetItem = targetItem,
                    NodeID = itemId,
                    ImageFileName = $"{itemId}.png",
                };

                this.metadataResolver.ConfigureTooltip(tooltip);
                tooltip.KeyPreview = true;
                tooltip.KeyDown += this.SearchPreviewTooltip_KeyDown;
                tooltip.FormClosed += this.SearchPreviewTooltip_FormClosed;
                tooltip.Refresh();

                if (tooltip.Bitmap == null)
                {
                    throw new InvalidOperationException($"无法生成 {result.Id} 的道具说明预览。");
                }

                Rectangle workingArea = Screen.FromControl(this).WorkingArea;
                int x = workingArea.Left + Math.Max(0, (workingArea.Width - tooltip.Bitmap.Width) / 2);
                int y = workingArea.Top + Math.Max(0, (workingArea.Height - tooltip.Bitmap.Height) / 2);
                tooltip.Location = new Point(
                    Math.Max(workingArea.Left, Math.Min(x, workingArea.Right - tooltip.Bitmap.Width)),
                    Math.Max(workingArea.Top, Math.Min(y, workingArea.Bottom - tooltip.Bitmap.Height)));

                tooltip.QuickRefresh();
                tooltip.Show(this);
                tooltip.BringToFront();
                this.searchPreviewTooltip = tooltip;
                tooltip = null;
            }
            finally
            {
                tooltip?.Dispose();
            }
        }

        private void LvSearchResults_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
            {
                return;
            }

            ListViewHitTestInfo hit = this.lvSearchResults.HitTest(e.Location);
            this.lvSearchResults.SelectedItems.Clear();
            if (hit?.Item != null)
            {
                hit.Item.Selected = true;
                this.lvSearchResults.FocusedItem = hit.Item;
            }
        }

        private void CmsSearchResults_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            bool canAdd = this.TryGetSelectedSearchResult(out AppearanceSearchResult result)
                && CanAddSearchResultToTemplate(result);
            this.miAddSearchResultToTemplate.Enabled = canAdd;
            e.Cancel = !canAdd;
        }

        private void MiAddSearchResultToTemplate_Click(object sender, EventArgs e)
        {
            if (!this.TryGetSelectedSearchResult(out AppearanceSearchResult result))
            {
                return;
            }

            if (!CanAddSearchResultToTemplate(result))
            {
                return;
            }

            if (this.AppendIdToTemplate(result.Id))
            {
                this.lblStatus.Text = $"已添加 {result.Id} 到模板。";
            }
            else
            {
                this.lblStatus.Text = $"{result.Id} 已存在于模板中。";
            }
        }

        private bool TryGetSelectedSearchResult(out AppearanceSearchResult result)
        {
            result = null;
            if (this.lvSearchResults.SelectedItems.Count == 0)
            {
                return false;
            }

            result = this.lvSearchResults.SelectedItems[0].Tag as AppearanceSearchResult;
            return result != null;
        }

        private static bool CanAddSearchResultToTemplate(AppearanceSearchResult result)
        {
            return result?.Kind == Program.AppearanceIdKind.Skin
                || result?.Kind == Program.AppearanceIdKind.Face
                || result?.Kind == Program.AppearanceIdKind.Hair
                || result?.Kind == Program.AppearanceIdKind.Gear;
        }

        private bool AppendIdToTemplate(int id)
        {
            foreach (string token in SplitAppearanceTokens(this.txtTemplate.Text))
            {
                if (this.TryResolveTemplateToken(token, out AppearanceSearchResult existingResult)
                    && existingResult.Id == id)
                {
                    return false;
                }
            }

            if (!this.IsBaseLoaded || !this.metadataResolver.TryResolveToken(this.txtBaseWz.Text.Trim(), id.ToString(), out AppearanceSearchResult result))
            {
                return false;
            }

            string displayToken = this.CreateUniqueTemplateDisplayToken(result.Name, result.Id);
            string original = this.txtTemplate.Text.Trim();
            this.txtTemplate.Text = string.IsNullOrWhiteSpace(original)
                ? displayToken
                : original.TrimEnd(',', '，') + "," + displayToken;
            this.templateDisplayIdMap[displayToken] = id;
            this.txtTemplate.SelectionStart = this.txtTemplate.TextLength;
            this.txtTemplate.SelectionLength = 0;
            this.txtTemplate.Focus();
            return true;
        }

        private static string[] SplitAppearanceTokens(string text)
        {
            return (text ?? string.Empty)
                .Split(new[] { ',', '，', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(token => token.Trim())
                .Where(token => !string.IsNullOrWhiteSpace(token))
                .ToArray();
        }

        private bool TryResolveTemplateToken(string token, out AppearanceSearchResult result)
        {
            return this.TryResolveMappedToken(token, this.templateDisplayIdMap, out result);
        }

        private string BuildTemplateIdText()
        {
            string[] tokens = SplitAppearanceTokens(this.txtTemplate.Text);
            if (tokens.Length == 0)
            {
                return this.txtTemplate.Text.Trim();
            }

            if (!this.IsBaseLoaded)
            {
                return string.Join(",", tokens);
            }

            var resolvedResults = new List<AppearanceSearchResult>(tokens.Length);
            foreach (string token in tokens)
            {
                if (!this.TryResolveTemplateToken(token, out AppearanceSearchResult result))
                {
                    throw new FormatException($"模板里有无法识别的名称或 ID：{token}");
                }

                resolvedResults.Add(result);
            }

            this.SetTemplateDisplayTokens(resolvedResults.Select(result => (result.Id, result.Name)));
            return string.Join(",", resolvedResults.Select(result => result.Id.ToString()));
        }

        private bool TryResolveGearToken(string token, out AppearanceSearchResult result)
        {
            if (!this.TryResolveMappedToken(token, this.gearDisplayIdMap, out result))
            {
                return false;
            }

            return result.Kind == Program.AppearanceIdKind.Gear;
        }

        private string BuildGearIdText()
        {
            if (!this.IsDyeMode)
            {
                return null;
            }

            string[] tokens = SplitAppearanceTokens(this.txtGear.Text);
            if (tokens.Length == 0)
            {
                return null;
            }

            if (!this.IsBaseLoaded)
            {
                return string.Join(",", tokens);
            }

            var resolvedResults = new List<AppearanceSearchResult>(tokens.Length);
            foreach (string token in tokens)
            {
                if (!this.TryResolveGearToken(token, out AppearanceSearchResult result))
                {
                    throw new FormatException($"Gear 里有无法识别的名称或 ID，或它不是可装备外观：{token}");
                }

                resolvedResults.Add(result);
            }

            this.SetGearDisplayTokens(resolvedResults.Select(result => (result.Id, result.Name)));
            return string.Join(",", resolvedResults.Select(result => result.Id.ToString()));
        }

        private void SetTemplateDisplayTokens(IEnumerable<(int Id, string Name)> tokens)
        {
            this.SetDisplayTokens(this.txtTemplate, this.templateDisplayIdMap, tokens);
        }

        private void SetGearDisplayTokens(IEnumerable<(int Id, string Name)> tokens)
        {
            this.SetDisplayTokens(this.txtGear, this.gearDisplayIdMap, tokens);
        }

        private string CreateUniqueTemplateDisplayToken(string baseName, int id)
        {
            return CreateUniqueDisplayToken(baseName, id, this.templateDisplayIdMap);
        }

        private bool TryResolveMappedToken(string token, IDictionary<string, int> displayIdMap, out AppearanceSearchResult result)
        {
            result = null;
            string trimmedToken = token?.Trim();
            if (string.IsNullOrWhiteSpace(trimmedToken) || !this.IsBaseLoaded)
            {
                return false;
            }

            if (displayIdMap != null && displayIdMap.TryGetValue(trimmedToken, out int mappedId))
            {
                return this.metadataResolver.TryResolveToken(this.txtBaseWz.Text.Trim(), mappedId.ToString(), out result);
            }

            return this.metadataResolver.TryResolveToken(this.txtBaseWz.Text.Trim(), trimmedToken, out result);
        }

        private void SetDisplayTokens(AlignedInputBox targetTextBox, IDictionary<string, int> targetMap, IEnumerable<(int Id, string Name)> tokens)
        {
            var displayTokens = new List<string>();
            var newMap = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach ((int id, string name) in tokens)
            {
                string baseName = string.IsNullOrWhiteSpace(name) ? id.ToString() : name.Trim();
                string displayToken = CreateUniqueDisplayToken(baseName, id, newMap);
                displayTokens.Add(displayToken);
                newMap[displayToken] = id;
            }

            targetMap.Clear();
            foreach (KeyValuePair<string, int> pair in newMap)
            {
                targetMap[pair.Key] = pair.Value;
            }

            string normalizedText = string.Join(",", displayTokens);
            if (!string.Equals(targetTextBox.Text, normalizedText, StringComparison.Ordinal))
            {
                targetTextBox.Text = normalizedText;
            }
        }

        private static string CreateUniqueDisplayToken(string baseName, int id, IDictionary<string, int> existingMap)
        {
            string candidate = string.IsNullOrWhiteSpace(baseName) ? id.ToString() : baseName.Trim();
            if (!existingMap.TryGetValue(candidate, out int existingId) || existingId == id)
            {
                return candidate;
            }

            int suffix = 2;
            while (true)
            {
                string numberedCandidate = $"{candidate}#{suffix}";
                if (!existingMap.TryGetValue(numberedCandidate, out existingId) || existingId == id)
                {
                    return numberedCandidate;
                }

                suffix++;
            }
        }

        private string ResolveHistoryDisplayName(string storedName, int id, Program.AppearanceIdKind kind)
        {
            if (!string.IsNullOrWhiteSpace(storedName))
            {
                return storedName;
            }

            if (this.IsBaseLoaded)
            {
                return this.metadataResolver.ResolveName(id, kind);
            }

            return id.ToString();
        }

        private void HistoryButton_Click(object sender, EventArgs e)
        {
            if (sender is not Button button || button.Tag is not TemplateHistoryItem item)
            {
                return;
            }

            this.SetTemplateDisplayTokens(new[]
            {
                (item.Hair, item.HairName),
                (item.Face, item.FaceName),
                (item.Skin, item.SkinName),
            });
            this.txtTemplate.SelectionStart = this.txtTemplate.TextLength;
            this.txtTemplate.SelectionLength = 0;
            this.txtTemplate.Focus();
            this.lblStatus.Text = "已从历史搭配回填模板。";
            this.MarkPreviewDirty();
        }

        private void RefreshHistoryButtons()
        {
            for (int i = 0; i < this.historyButtons.Count; i++)
            {
                HistoryTemplateButton button = this.historyButtons[i];
                if (i >= this.templateHistory.Count)
                {
                    button.Tag = null;
                    button.SetLines(Array.Empty<string>());
                    button.Visible = false;
                    continue;
                }

                TemplateHistoryItem item = this.templateHistory[i];
                button.Tag = item;
                button.SetLines(this.BuildHistoryButtonLines(item));
                button.Visible = true;
            }
        }

        private string[] BuildHistoryButtonLines(TemplateHistoryItem item)
        {
            if (item == null)
            {
                return Array.Empty<string>();
            }

            return new[]
            {
                $"发 {this.ResolveHistoryDisplayName(item.HairName, item.Hair, Program.AppearanceIdKind.Hair)}",
                $"脸 {this.ResolveHistoryDisplayName(item.FaceName, item.Face, Program.AppearanceIdKind.Face)}",
                $"肤 {this.ResolveHistoryDisplayName(item.SkinName, item.Skin, Program.AppearanceIdKind.Skin)}",
            };
        }

        private void RememberTemplateHistory(ResolvedAppearancePreview preview)
        {
            if (preview == null || !preview.CanExport || !preview.SkinId.HasValue || !preview.FaceId.HasValue || !preview.HairId.HasValue)
            {
                return;
            }

            string key = BuildTemplateHistoryKey(preview.SkinId.Value, preview.FaceId.Value, preview.HairId.Value);
            this.templateHistory.RemoveAll(item => BuildTemplateHistoryKey(item.Skin, item.Face, item.Hair) == key);
            this.templateHistory.Insert(0, new TemplateHistoryItem
            {
                Skin = preview.SkinId.Value,
                SkinName = this.metadataResolver.ResolveName(preview.SkinId.Value, Program.AppearanceIdKind.Skin),
                Face = preview.FaceId.Value,
                FaceName = this.metadataResolver.ResolveName(preview.FaceId.Value, Program.AppearanceIdKind.Face),
                Hair = preview.HairId.Value,
                HairName = this.metadataResolver.ResolveName(preview.HairId.Value, Program.AppearanceIdKind.Hair),
            });

            while (this.templateHistory.Count > 9)
            {
                this.templateHistory.RemoveAt(this.templateHistory.Count - 1);
            }

            this.config.TemplateHistory = this.templateHistory;
            this.RefreshHistoryButtons();
            this.SaveConfig();
        }

        private void SearchPreviewTooltip_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                e.Handled = true;
                this.CloseSearchPreviewTooltip();
            }
        }

        private void SearchPreviewTooltip_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (ReferenceEquals(sender, this.searchPreviewTooltip))
            {
                this.searchPreviewTooltip = null;
            }
        }

        private void CloseSearchPreviewTooltip()
        {
            if (this.searchPreviewTooltip == null)
            {
                return;
            }

            try
            {
                this.searchPreviewTooltip.KeyDown -= this.SearchPreviewTooltip_KeyDown;
                this.searchPreviewTooltip.FormClosed -= this.SearchPreviewTooltip_FormClosed;
                this.searchPreviewTooltip.Close();
                this.searchPreviewTooltip.Dispose();
            }
            finally
            {
                this.searchPreviewTooltip = null;
            }
        }

        private void BindSearchResults()
        {
            this.lvSearchResults.BeginUpdate();
            this.lvSearchResults.Items.Clear();

            int startIndex = this.currentSearchPage * SearchPageSize;
            foreach (AppearanceSearchResult result in this.searchResults.Skip(startIndex).Take(SearchPageSize))
            {
                var item = new ListViewItem(result.KindLabel) { Tag = result };
                item.SubItems.Add(result.Id.ToString());
                item.SubItems.Add(result.Name);
                this.lvSearchResults.Items.Add(item);
            }

            this.lvSearchResults.EndUpdate();
            this.UpdateSearchNavigationState();
        }

        private int GetSearchPageCount()
        {
            if (this.searchResults.Count == 0)
            {
                return 0;
            }

            return (int)Math.Ceiling(this.searchResults.Count / (double)SearchPageSize);
        }

        private void UpdateSearchNavigationState()
        {
            int pageCount = this.GetSearchPageCount();
            bool controlsEnabled = !this.isBusy && !this.isSearching;
            bool baseReady = this.IsBaseLoaded;
            bool hasSearchPages = pageCount > 0;

            this.txtBaseWz.Enabled = false;
            this.txtTemplate.Enabled = controlsEnabled;
            this.cboMode.Enabled = controlsEnabled;
            this.txtGear.Enabled = controlsEnabled && this.IsDyeMode;
            this.trkDyeSaturation.Enabled = controlsEnabled && this.IsDyeMode;
            this.trkDyeBrightness.Enabled = controlsEnabled && this.IsDyeMode;
            this.nudDyeSaturation.Enabled = controlsEnabled && this.IsDyeMode;
            this.nudDyeBrightness.Enabled = controlsEnabled && this.IsDyeMode;
            this.btnBrowseBase.Enabled = controlsEnabled && this.baseLoadState != BaseLoadState.Loading;
            this.btnPickBackgroundColor.Enabled = controlsEnabled;
            this.btnResetBackgroundColor.Enabled = controlsEnabled;
            this.btnBrowseBackgroundImage.Enabled = controlsEnabled;
            this.btnClearBackgroundImage.Enabled = controlsEnabled && !string.IsNullOrWhiteSpace(this.currentBackgroundImagePath);
            this.chkTransparentBackground.Enabled = controlsEnabled;
            this.btnValidatePreview.Enabled = controlsEnabled && baseReady;
            this.btnExportSettings.Enabled = controlsEnabled;
            this.btnExport.Enabled = controlsEnabled && baseReady;
            this.txtSearch.Enabled = controlsEnabled && baseReady;
            this.btnSearch.Enabled = controlsEnabled && baseReady;
            this.chkSearchAppearanceOnly.Enabled = controlsEnabled && baseReady;
            this.btnPrevPage.Enabled = controlsEnabled && baseReady && this.currentSearchPage > 0;
            this.btnNextPage.Enabled = controlsEnabled && baseReady && this.currentSearchPage + 1 < pageCount;
            this.btnJumpPage.Enabled = controlsEnabled && baseReady && hasSearchPages;
            this.nudSearchPage.Enabled = controlsEnabled && baseReady && hasSearchPages;
            this.lvSearchResults.Enabled = controlsEnabled && baseReady;
            foreach (Button button in this.backgroundPaletteButtons)
            {
                button.Enabled = controlsEnabled;
            }
            foreach (HistoryTemplateButton button in this.historyButtons)
            {
                button.Enabled = controlsEnabled;
            }

            decimal maxPage = Math.Max(1, pageCount);
            if (this.nudSearchPage.Maximum != maxPage)
            {
                this.nudSearchPage.Maximum = maxPage;
            }

            if (this.nudSearchPage.Minimum != 1)
            {
                this.nudSearchPage.Minimum = 1;
            }

            decimal currentPageValue = hasSearchPages ? this.currentSearchPage + 1 : 1;
            currentPageValue = Math.Min(this.nudSearchPage.Maximum, Math.Max(this.nudSearchPage.Minimum, currentPageValue));
            if (this.nudSearchPage.Value != currentPageValue)
            {
                this.nudSearchPage.Value = currentPageValue;
            }

            if (!hasSearchPages)
            {
                this.lblSearchPager.Text = $"0 / 0，共 {this.searchResults.Count} 条";
            }
            else
            {
                this.lblSearchPager.Text = $"{this.currentSearchPage + 1} / {pageCount}，共 {this.searchResults.Count} 条";
            }
        }

        private void SetBusy(bool busy)
        {
            this.isBusy = busy;
            this.previewTimer.Stop();
            this.UpdateSearchNavigationState();
        }

        private void SaveConfig()
        {
            Size size = this.WindowState == FormWindowState.Normal ? this.Size : this.RestoreBounds.Size;

            this.config.BaseWzPath = this.txtBaseWz.Text.Trim();
            this.config.WindowWidth = Math.Max(this.MinimumSize.Width, size.Width);
            this.config.WindowHeight = Math.Max(this.MinimumSize.Height, size.Height);
            this.config.DyeSaturationOffset = this.ClampAdjustmentValue(this.trkDyeSaturation.Value);
            this.config.DyeBrightnessOffset = this.ClampAdjustmentValue(this.trkDyeBrightness.Value);
            this.config.BackgroundColorArgb = this.currentBackgroundColor.ToArgb();
            this.config.BackgroundImagePath = this.currentBackgroundImagePath;
            this.config.SearchAppearanceOnly = this.chkSearchAppearanceOnly.Checked;
            this.config.NormalExportActions = this.SelectedNormalExportActions.ToList();
            this.config.DyeExportAction = this.SelectedDyeExportAction;

            this.configStore.Save(this.config);
        }

        private bool IsBaseLoaded => this.baseLoadState == BaseLoadState.Loaded;

        private void UpdateBaseLoadState(BaseLoadState state)
        {
            this.baseLoadState = state;

            switch (state)
            {
                case BaseLoadState.Loading:
                    this.lblBaseStatus.Text = "加载中";
                    this.lblBaseStatus.ForeColor = Color.DarkGoldenrod;
                    break;

                case BaseLoadState.Loaded:
                    this.lblBaseStatus.Text = "已加载";
                    this.lblBaseStatus.ForeColor = Color.DarkGreen;
                    break;

                default:
                    this.lblBaseStatus.Text = "未读取";
                    this.lblBaseStatus.ForeColor = SystemColors.ControlText;
                    break;
            }

            this.UpdateSearchNavigationState();
        }

        private bool EnsureBaseReady(string actionName)
        {
            if (this.baseLoadState == BaseLoadState.Loading)
            {
                MessageBox.Show(this, "Base.wz 仍在加载中，请稍后再试。", actionName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            if (!this.IsBaseLoaded)
            {
                MessageBox.Show(this, "请先选择并加载 Base.wz。", actionName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            return true;
        }

        private async Task LoadBaseAsync(string baseWzPath, bool showFailureDialog)
        {
            baseWzPath = baseWzPath?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(baseWzPath))
            {
                this.UpdateBaseLoadState(BaseLoadState.NotLoaded);
                this.lblStatus.Text = "未选择 Base.wz";
                this.MarkPreviewDirty();
                return;
            }

            if (this.IsBaseLoaded && string.Equals(this.txtBaseWz.Text.Trim(), baseWzPath, StringComparison.OrdinalIgnoreCase))
            {
                this.lblStatus.Text = "Base.wz 已加载";
                return;
            }

            this.txtBaseWz.Text = baseWzPath;
            this.UpdateBaseLoadState(BaseLoadState.Loading);
            this.lblStatus.Text = "正在加载 Base.wz...";
            this.txtPreview.Text = "Base.wz 加载中...";
            this.previewTimer.Stop();

            try
            {
                await Task.Run(() => this.metadataResolver.LoadBase(baseWzPath));
                this.UpdateBaseLoadState(BaseLoadState.Loaded);
                this.lblStatus.Text = "Base.wz 已加载";
                this.RefreshHistoryButtons();
                this.SaveConfig();
                this.MarkPreviewDirty();
            }
            catch (Exception ex)
            {
                ErrorLog.Write(ex, "MainForm.LoadBaseAsync");
                this.UpdateBaseLoadState(BaseLoadState.NotLoaded);
                this.lblStatus.Text = "Base.wz 加载失败";
                this.txtPreview.Text = $"Base.wz 加载失败：{ex.Message}";

                if (showFailureDialog)
                {
                    MessageBox.Show(this, $"Base.wz 加载失败：\r\n{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private enum BaseLoadState
        {
            NotLoaded = 0,
            Loading,
            Loaded,
        }
    }

    internal sealed class MetadataResolver : IDisposable
    {
        private Program.WzSearchContext wzContext;
        private StringLinker stringLinker;
        private string loadedBaseWzPath;
        private readonly Dictionary<int, Program.AppearanceIdKind> idKindCache = new Dictionary<int, Program.AppearanceIdKind>();
        private List<AppearanceSearchResult> searchIndex;
        private Dictionary<int, AppearanceSearchResult> searchIndexById;
        private List<AppearanceSearchResult> allItemSearchIndex;

        public void LoadBase(string baseWzPath)
        {
            this.EnsureLoaded(baseWzPath);
        }

        public ResolvedAppearancePreview Resolve(
            string baseWzPath,
            string templateText,
            string gearText,
            bool dyeMode,
            IEnumerable<string> normalActions,
            string dyeAction)
        {
            this.EnsureLoaded(baseWzPath);

            Program.TemplateAnalysis analysis = Program.AnalyzeTemplate(templateText, this.ClassifyAppearanceId);
            string[] selectedNormalActions = Program.NormalizeNormalActionSelection(normalActions);
            string selectedDyeAction = Program.NormalizeDyeActionSelection(dyeAction);
            int defaultSkinId = 0;
            bool usingDefaultSkin = !analysis.Skin.HasValue && Program.TryResolveDefaultSkinId(out defaultSkinId);
            int[] gearIds = string.IsNullOrWhiteSpace(gearText) ? Array.Empty<int>() : ParseGearIds(gearText);
            var dyeGearIdSet = new HashSet<int>(gearIds);
            var templateEntries = new List<ResolvedAppearanceEntry>(analysis.Items.Count);
            foreach (Program.TemplateItemInfo item in analysis.Items)
            {
                string label = item.Kind == Program.AppearanceIdKind.Unknown ? "未识别" : this.GetKindLabel(item.Id, item.Kind);
                string name = item.Kind == Program.AppearanceIdKind.Unknown ? item.Id.ToString() : this.ResolveDisplayName(item.Id, item.Kind);
                templateEntries.Add(new ResolvedAppearanceEntry(label, name, item.Id));
            }

            string dyeTargetText = null;
            bool canExport = false;
            string exportBlockReason = this.BuildExportBlockReason(analysis, gearIds, dyeMode);
            List<ResolvedAppearanceEntry> effectiveEntries = null;
            int? resolvedSkinId = analysis.Skin ?? (usingDefaultSkin ? defaultSkinId : null);
            int? resolvedFaceId = analysis.Face;
            int? resolvedHairId = analysis.Hair;

            if (string.IsNullOrWhiteSpace(exportBlockReason))
            {
                Program.AvatarTemplate template = Program.AvatarTemplate.Parse(templateText, this.ClassifyAppearanceId);
                Program.EffectiveAppearance effective = Program.BuildEffectiveAppearance(template, gearIds);
                resolvedSkinId = effective.Skin;
                resolvedFaceId = effective.Face;
                resolvedHairId = effective.Hair;
                effectiveEntries = new List<ResolvedAppearanceEntry>
                {
                    new ResolvedAppearanceEntry("肤色", this.ResolveDisplayName(effective.Skin, Program.AppearanceIdKind.Skin), effective.Skin),
                    new ResolvedAppearanceEntry("脸型", this.ResolveDisplayName(effective.Face, Program.AppearanceIdKind.Face), effective.Face),
                    new ResolvedAppearanceEntry("发型", this.ResolveDisplayName(effective.Hair, Program.AppearanceIdKind.Hair), effective.Hair),
                };

                foreach (Program.EffectiveGear gear in effective.Gears)
                {
                    bool isDyeTarget = dyeMode && dyeGearIdSet.Contains(gear.Id);
                    effectiveEntries.Add(new ResolvedAppearanceEntry(
                        isDyeTarget ? $"{gear.SlotLabel} [染色]" : gear.SlotLabel,
                        this.ResolveDisplayName(gear.Id, Program.AppearanceIdKind.Gear),
                        gear.Id));
                }

                canExport = true;
            }

            if (dyeMode && gearIds.Length > 0)
            {
                dyeTargetText = string.Join(" / ", gearIds.Select(id =>
                {
                    Program.AppearanceIdKind kind = this.ClassifyAppearanceId(id);
                    return kind == Program.AppearanceIdKind.Unknown
                        ? id.ToString()
                        : $"{this.GetKindLabel(id, kind)} - {this.ResolveDisplayName(id, kind)}";
                }));
            }
            else if (dyeMode)
            {
                dyeTargetText = "未填写";
            }

            IReadOnlyList<ResolvedAppearanceEntry> fileNameEntries = (IReadOnlyList<ResolvedAppearanceEntry>) (effectiveEntries ?? templateEntries);
            string[] selectedActionsForFileName = dyeMode
                ? new[] { selectedDyeAction }
                : selectedNormalActions;
            string fileBaseName = BuildFileBaseName(
                fileNameEntries.Where(entry => entry.Id.HasValue).Select(entry => entry.Name),
                dyeMode,
                selectedActionsForFileName);
            string outputPath = Path.Combine(AppContext.BaseDirectory, fileBaseName + ".gif");

            var lines = new List<string>();
            lines.Add("模板识别结果:");
            foreach (ResolvedAppearanceEntry entry in templateEntries)
            {
                lines.Add($"{entry.Label}: {entry.Name} ({entry.Id})");
            }

            if (usingDefaultSkin)
            {
                lines.Add($"肤色: 未填写，默认使用 {this.ResolveDisplayName(defaultSkinId, Program.AppearanceIdKind.Skin)} ({defaultSkinId})");
            }

            if (effectiveEntries != null)
            {
                lines.Add(string.Empty);
                lines.Add("当前生效外观:");
                foreach (ResolvedAppearanceEntry entry in effectiveEntries)
                {
                    lines.Add($"{entry.Label}: {entry.Name}");
                }
            }
            else if (!string.IsNullOrWhiteSpace(exportBlockReason))
            {
                lines.Add(string.Empty);
                lines.Add($"当前状态: {exportBlockReason}");
            }

            if (dyeMode)
            {
                lines.Add($"染色对象: {dyeTargetText}");
            }
            lines.Add($"导出动作: {string.Join(" / ", selectedActionsForFileName)}");
            lines.Add(string.Empty);
            lines.Add($"导出文件名: {fileBaseName}.gif");
            lines.Add($"导出目录: {AppContext.BaseDirectory}");

            return new ResolvedAppearancePreview
            {
                Entries = effectiveEntries ?? templateEntries,
                FileBaseName = fileBaseName,
                OutputPath = outputPath,
                PreviewText = string.Join(Environment.NewLine, lines),
                CanExport = canExport,
                ExportBlockReason = exportBlockReason,
                SkinId = resolvedSkinId,
                FaceId = resolvedFaceId,
                HairId = resolvedHairId,
            };
        }

        public string ResolveName(int id, Program.AppearanceIdKind kind)
        {
            return this.ResolveDisplayName(id, kind);
        }

        public bool TryResolveToken(string baseWzPath, string token, out AppearanceSearchResult result)
        {
            result = null;
            this.EnsureLoaded(baseWzPath);

            string trimmedToken = token?.Trim();
            if (string.IsNullOrWhiteSpace(trimmedToken))
            {
                return false;
            }

            if (int.TryParse(trimmedToken, out int id))
            {
                Program.AppearanceIdKind kind = this.ClassifyAppearanceId(id);
                if (kind == Program.AppearanceIdKind.Unknown)
                {
                    return false;
                }

                result = this.CreateSearchResult(id, kind);
                return true;
            }

            AppearanceSearchResult[] exactMatches = this.searchIndex
                .Where(entry => string.Equals(entry.Name, trimmedToken, StringComparison.CurrentCultureIgnoreCase))
                .ToArray();
            if (exactMatches.Length != 1)
            {
                return false;
            }

            result = exactMatches[0];
            return true;
        }

        public List<AppearanceSearchResult> Search(string baseWzPath, string query, bool appearanceOnly)
        {
            this.EnsureLoaded(baseWzPath);

            string trimmedQuery = query?.Trim();
            if (string.IsNullOrWhiteSpace(trimmedQuery))
            {
                return new List<AppearanceSearchResult>();
            }

            string[] tokens = trimmedQuery
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            IReadOnlyList<AppearanceSearchResult> source = appearanceOnly
                ? this.searchIndex
                : this.EnsureAllItemSearchIndex();

            var results = new List<AppearanceSearchResult>();
            foreach (AppearanceSearchResult result in source)
            {
                if (!MatchesSearch(result.Id.ToString(), result.Name, result.KindLabel, GetSearchAlias(result), tokens))
                {
                    continue;
                }

                results.Add(result);
            }

            return results;
        }

        public Bitmap LoadIcon(string baseWzPath, AppearanceSearchResult result)
        {
            this.EnsureLoaded(baseWzPath);

            if (result == null)
            {
                return null;
            }

            Wz_Node iconNode = result.Kind == Program.AppearanceIdKind.Item
                ? this.FindItemIconNode(result.Id)
                : Program.FindAppearanceIconNode(result.Id);
            if (iconNode == null)
            {
                return null;
            }

            BitmapOrigin icon = BitmapOrigin.CreateFromNode(iconNode, PluginManager.FindWz);
            if (icon.Bitmap == null)
            {
                return null;
            }

            return new Bitmap(icon.Bitmap);
        }

        public SearchPreviewImage LoadPreviewImage(string baseWzPath, AppearanceSearchResult result)
        {
            this.EnsureLoaded(baseWzPath);

            Bitmap tooltipBitmap = this.RenderTooltipBitmap(result);
            if (tooltipBitmap != null)
            {
                return new SearchPreviewImage(tooltipBitmap, true);
            }

            Bitmap iconBitmap = this.LoadIcon(baseWzPath, result);
            return iconBitmap == null ? null : new SearchPreviewImage(iconBitmap, false);
        }

        public Gear LoadTooltipGear(string baseWzPath, AppearanceSearchResult result)
        {
            this.EnsureLoaded(baseWzPath);

            if (result == null || result.Kind != Program.AppearanceIdKind.Gear)
            {
                return null;
            }

            Wz_Node node = this.FindAppearanceNode(result);
            if (node == null)
            {
                return null;
            }

            try
            {
                return Gear.CreateFromNode(node, PluginManager.FindWz);
            }
            catch (Exception ex)
            {
                ErrorLog.Write(ex, $"MetadataResolver.LoadTooltipGear({result.Id})");
                return null;
            }
        }

        public Item LoadTooltipItem(string baseWzPath, AppearanceSearchResult result)
        {
            this.EnsureLoaded(baseWzPath);

            if (result == null || result.Kind != Program.AppearanceIdKind.Item)
            {
                return null;
            }

            Wz_Node node = this.FindItemNode(result.Id);
            if (node == null)
            {
                return null;
            }

            try
            {
                PrepareItemTooltipData(result.Id);
                return Item.CreateFromNode(node, PluginManager.FindWz);
            }
            catch (Exception ex)
            {
                ErrorLog.Write(ex, $"MetadataResolver.LoadTooltipItem({result.Id})");
                return null;
            }
        }

        public void ConfigureTooltip(AfrmTooltip tooltip)
        {
            if (tooltip == null)
            {
                throw new ArgumentNullException(nameof(tooltip));
            }

            TooltipRenderSettings settings = GetTooltipRenderSettings();
            tooltip.StringLinker = this.stringLinker;
            tooltip.ShowID = settings.ShowObjectId;
            tooltip.ShowMenu = true;
            tooltip.Enable22AniStyle = settings.Use22Style;
            tooltip.HideOnHover = false;

            tooltip.GearRender.ShowObjectID = settings.ShowObjectId;
            tooltip.GearRender.ShowLevelOrSealed = settings.ShowLevelOrSealed;
            tooltip.GearRender.MaxStar25 = settings.MaxStar25;
            tooltip.GearRender.ShowCosmetic = settings.ShowCosmetic;
            tooltip.GearRender.ShowCashPurchasePrice = settings.ShowCashPurchasePrice;

            tooltip.GearRender22.ShowObjectID = settings.ShowObjectId;
            tooltip.GearRender22.ShowLevelOrSealed = settings.ShowLevelOrSealed;
            tooltip.GearRender22.MaxStar25 = settings.MaxStar25;
            tooltip.GearRender22.ShowCosmetic = settings.ShowCosmetic;
            tooltip.GearRender22.ShowCashPurchasePrice = settings.ShowCashPurchasePrice;

            tooltip.ItemRender.ShowObjectID = settings.ShowObjectId;
            tooltip.ItemRender.ShowLevelOrSealed = settings.ShowLevelOrSealed;
            tooltip.ItemRender.Enable22AniStyle = settings.Use22Style;
            tooltip.ItemRender.ShowCashPurchasePrice = settings.ShowCashPurchasePrice;

            tooltip.ItemRender3.ShowObjectID = settings.ShowObjectId;
            tooltip.ItemRender3.ShowLevelOrSealed = settings.ShowLevelOrSealed;
            tooltip.ItemRender3.ShowCashPurchasePrice = settings.ShowCashPurchasePrice;

            tooltip.Enable22AniStyle = settings.Use22Style;
            GearGraphics.is22aniStyle = settings.Use22Style;
        }

        public void Dispose()
        {
            this.stringLinker = null;
            this.loadedBaseWzPath = null;
            this.idKindCache.Clear();
            this.searchIndex = null;
            this.searchIndexById = null;
            this.allItemSearchIndex = null;
            this.wzContext?.Dispose();
            this.wzContext = null;
        }

        private void EnsureLoaded(string baseWzPath)
        {
            if (string.IsNullOrWhiteSpace(baseWzPath))
            {
                throw new ArgumentException("请选择 Base.wz。");
            }

            if (string.Equals(this.loadedBaseWzPath, baseWzPath, StringComparison.OrdinalIgnoreCase)
                && this.wzContext != null
                && this.stringLinker != null
                && this.stringLinker.HasValues)
            {
                return;
            }

            this.Dispose();

            this.wzContext = new Program.WzSearchContext(baseWzPath);

            Wz_Node stringNode = PluginManager.FindWz(Wz_Type.String);
            Wz_Node itemNode = PluginManager.FindWz(Wz_Type.Item);
            Wz_Node etcNode = PluginManager.FindWz(Wz_Type.Etc);
            Wz_Node questNode = PluginManager.FindWz(Wz_Type.Quest);

            if (stringNode == null || itemNode == null || etcNode == null)
            {
                throw new InvalidOperationException("无法加载 String/Item/Etc 数据。");
            }

            this.stringLinker = new StringLinker();
            if (!this.stringLinker.Load(stringNode, itemNode, etcNode, questNode))
            {
                throw new InvalidOperationException("StringLinker 初始化失败。");
            }

            this.BuildSearchIndex();
            this.loadedBaseWzPath = baseWzPath;
        }

        private Program.AppearanceIdKind ClassifyAppearanceId(int id)
        {
            if (this.searchIndexById != null && this.searchIndexById.TryGetValue(id, out AppearanceSearchResult indexedResult))
            {
                return indexedResult.Kind;
            }

            if (!this.idKindCache.TryGetValue(id, out Program.AppearanceIdKind kind))
            {
                kind = Program.DetectAppearanceIdKind(id);
                this.idKindCache[id] = kind;
            }

            return kind;
        }

        private string ResolveDisplayName(int id, Program.AppearanceIdKind kind)
        {
            switch (kind)
            {
                case Program.AppearanceIdKind.Skin:
                    return this.ResolveSkinName(id);

                case Program.AppearanceIdKind.Face:
                    return this.ResolveAppearanceName(id, $"脸型{id}");

                case Program.AppearanceIdKind.Hair:
                    return this.ResolveAppearanceName(id, $"发型{id}");

                case Program.AppearanceIdKind.Gear:
                    return this.ResolveAppearanceName(id, id.ToString());

                case Program.AppearanceIdKind.Item:
                    return this.ResolveAppearanceName(id, id.ToString());

                default:
                    return id.ToString();
            }
        }

        private void BuildSearchIndex()
        {
            var entries = new Dictionary<int, AppearanceSearchResult>();

            foreach (AppearanceSearchResult result in this.EnumerateCharacterAppearanceEntries())
            {
                entries[result.Id] = result;
                this.idKindCache[result.Id] = result.Kind;
            }

            foreach (AppearanceSearchResult result in this.EnumerateSkinEntries())
            {
                entries[result.Id] = result;
                this.idKindCache[result.Id] = result.Kind;
            }

            this.searchIndex = entries.Values
                .OrderBy(result => result.Id)
                .ToList();
            this.searchIndexById = this.searchIndex.ToDictionary(result => result.Id, result => result);
            this.allItemSearchIndex = null;
        }

        private IReadOnlyList<AppearanceSearchResult> EnsureAllItemSearchIndex()
        {
            if (this.allItemSearchIndex != null)
            {
                return this.allItemSearchIndex;
            }

            var entries = new Dictionary<int, AppearanceSearchResult>();
            if (this.searchIndex != null)
            {
                foreach (AppearanceSearchResult result in this.searchIndex)
                {
                    entries[result.Id] = result;
                }
            }

            if (this.stringLinker?.StringEqp != null)
            {
                foreach (KeyValuePair<int, StringResult> pair in this.stringLinker.StringEqp)
                {
                    if (entries.ContainsKey(pair.Key))
                    {
                        continue;
                    }

                    entries[pair.Key] = new AppearanceSearchResult(
                        pair.Key,
                        GetStringResultName(pair.Value, pair.Key),
                        Program.AppearanceIdKind.Gear,
                        this.GetKindLabel(pair.Key, Program.AppearanceIdKind.Gear));
                }
            }

            if (this.stringLinker?.StringItem != null)
            {
                foreach (KeyValuePair<int, StringResult> pair in this.stringLinker.StringItem)
                {
                    if (entries.ContainsKey(pair.Key))
                    {
                        continue;
                    }

                    entries[pair.Key] = new AppearanceSearchResult(
                        pair.Key,
                        GetStringResultName(pair.Value, pair.Key),
                        Program.AppearanceIdKind.Item,
                        GetItemKindLabel(pair.Key));
                }
            }

            this.allItemSearchIndex = entries.Values
                .OrderBy(result => result.Id)
                .ToList();
            return this.allItemSearchIndex;
        }

        private AppearanceSearchResult CreateSearchResult(int id, Program.AppearanceIdKind kind)
        {
            if (this.searchIndexById != null && this.searchIndexById.TryGetValue(id, out AppearanceSearchResult indexedResult))
            {
                return indexedResult;
            }

            return new AppearanceSearchResult(
                id,
                this.ResolveDisplayName(id, kind),
                kind,
                this.GetKindLabel(id, kind));
        }

        private IEnumerable<AppearanceSearchResult> EnumerateCharacterAppearanceEntries()
        {
            Wz_Node characterNode = PluginManager.FindWz(Wz_Type.Character);
            if (characterNode == null)
            {
                yield break;
            }

            var stack = new Stack<Wz_Node>();
            stack.Push(characterNode);

            while (stack.Count > 0)
            {
                Wz_Node node = stack.Pop();
                foreach (Wz_Node child in node.Nodes)
                {
                    stack.Push(child);
                }

                if (node.Text == null
                    || !node.Text.EndsWith(".img", StringComparison.OrdinalIgnoreCase)
                    || node.Text.Contains("_Canvas", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!int.TryParse(Path.GetFileNameWithoutExtension(node.Text), out int id) || id <= 0)
                {
                    continue;
                }

                string fullPath = (node.FullPath ?? string.Empty).Replace('/', '\\');
                Program.AppearanceIdKind kind;

                if (fullPath.Contains("\\Hair\\", StringComparison.OrdinalIgnoreCase))
                {
                    kind = Program.AppearanceIdKind.Hair;
                }
                else if (fullPath.Contains("\\Face\\", StringComparison.OrdinalIgnoreCase))
                {
                    kind = Program.AppearanceIdKind.Face;
                }
                else
                {
                    GearType type = Gear.GetGearType(id);
                    if (type == GearType.body || type == GearType.head)
                    {
                        continue;
                    }

                    if (Gear.IsFace(type))
                    {
                        kind = Program.AppearanceIdKind.Face;
                    }
                    else if (Gear.IsHair(type))
                    {
                        kind = Program.AppearanceIdKind.Hair;
                    }
                    else
                    {
                        kind = Program.AppearanceIdKind.Gear;
                    }
                }

                yield return new AppearanceSearchResult(
                    id,
                    this.ResolveDisplayName(id, kind),
                    kind,
                    this.GetKindLabel(id, kind));
            }
        }

        private IEnumerable<AppearanceSearchResult> EnumerateSkinEntries()
        {
            var indexedIds = new HashSet<int>();

            if (this.stringLinker?.StringEqp != null)
            {
                foreach (int id in this.stringLinker.StringEqp.Keys.OrderBy(id => id))
                {
                    if (!ShouldIndexSkinStringId(id) || !Program.TryGetSkinNodes(id, out _, out _))
                    {
                        continue;
                    }

                    int displayId = GetSkinDisplayId(id);
                    if (!indexedIds.Add(displayId))
                    {
                        continue;
                    }

                    yield return new AppearanceSearchResult(
                        displayId,
                        this.ResolveDisplayName(displayId, Program.AppearanceIdKind.Skin),
                        Program.AppearanceIdKind.Skin,
                        "皮肤");
                }
            }

            foreach (int id in Program.EnumerateAvailableSkinIds())
            {
                int displayId = GetSkinDisplayId(id);
                if (!indexedIds.Add(displayId))
                {
                    continue;
                }

                yield return new AppearanceSearchResult(
                    displayId,
                    this.ResolveDisplayName(displayId, Program.AppearanceIdKind.Skin),
                    Program.AppearanceIdKind.Skin,
                    "皮肤");
            }
        }

        private string ResolveSkinName(int skin)
        {
            if (this.stringLinker?.StringEqp == null)
            {
                return $"肤色{skin}";
            }

            foreach (int candidateId in EnumerateSkinLookupIds(skin))
            {
                if (this.stringLinker.StringEqp.TryGetValue(candidateId, out StringResult sr)
                    && !string.IsNullOrWhiteSpace(sr.Name))
                {
                    return sr.Name;
                }
            }

            return $"肤色{skin}";
        }

        private string ResolveAppearanceName(int id, string fallbackName)
        {
            if (this.stringLinker?.StringEqp.TryGetValue(id, out StringResult eqp) == true
                && !string.IsNullOrWhiteSpace(eqp.Name))
            {
                return eqp.Name;
            }

            if (this.stringLinker?.StringItem.TryGetValue(id, out StringResult item) == true
                && !string.IsNullOrWhiteSpace(item.Name))
            {
                return item.Name;
            }

            return fallbackName;
        }

        private string GetKindLabel(int id, Program.AppearanceIdKind kind)
        {
            switch (kind)
            {
                case Program.AppearanceIdKind.Skin:
                    return "肤色";

                case Program.AppearanceIdKind.Face:
                    return "脸型";

                case Program.AppearanceIdKind.Hair:
                    return "发型";

                case Program.AppearanceIdKind.Gear:
                    return Program.GetAppearanceSlotLabel(id);

                case Program.AppearanceIdKind.Item:
                    return GetItemKindLabel(id);

                default:
                    return "外观";
            }
        }

        private static string GetStringResultName(StringResult sr, int id)
        {
            return string.IsNullOrWhiteSpace(sr?.Name) ? id.ToString() : sr.Name;
        }

        private static string GetItemKindLabel(int id)
        {
            if (id / 10000 == 910)
            {
                return "特殊";
            }

            switch (Item.GetItemType(id))
            {
                case Item.ItemType.Consume:
                    return "消耗";

                case Item.ItemType.Install:
                    return "装饰";

                case Item.ItemType.Etc:
                    return "其它";

                case Item.ItemType.Pet:
                    return "宠物";

                case Item.ItemType.Cash:
                    return "现金";

                default:
                    return "道具";
            }
        }

        private string BuildExportBlockReason(Program.TemplateAnalysis analysis, IReadOnlyList<int> gearIds, bool dyeMode)
        {
            var reasons = new List<string>();

            int[] unknownIds = analysis.Items
                .Where(item => item.Kind == Program.AppearanceIdKind.Unknown)
                .Select(item => item.Id)
                .Distinct()
                .ToArray();
            if (unknownIds.Length > 0)
            {
                reasons.Add($"存在未识别的 ID：{string.Join(", ", unknownIds)}");
            }

            var missingKinds = new List<string>();
            if (!analysis.Skin.HasValue && !Program.TryResolveDefaultSkinId(out _))
            {
                missingKinds.Add("肤色");
            }
            if (!analysis.Face.HasValue)
            {
                missingKinds.Add("脸型");
            }
            if (!analysis.Hair.HasValue)
            {
                missingKinds.Add("发型");
            }
            if (missingKinds.Count > 0)
            {
                reasons.Add($"缺少 {string.Join(" / ", missingKinds)} ID");
            }

            if (dyeMode)
            {
                if (gearIds == null || gearIds.Count == 0)
                {
                    reasons.Add("染色模式必须填写 Gear ID");
                }
                else
                {
                    int[] invalidGearIds = gearIds
                        .Where(id => this.ClassifyAppearanceId(id) != Program.AppearanceIdKind.Gear)
                        .Distinct()
                        .ToArray();
                    if (invalidGearIds.Length > 0)
                    {
                        reasons.Add($"Gear 区域只能填写可装备外观 ID：{string.Join(", ", invalidGearIds)}");
                    }
                }
            }

            return reasons.Count == 0
                ? null
                : string.Join("；", reasons) + "，暂时不能导出。";
        }

        private Bitmap RenderTooltipBitmap(AppearanceSearchResult result)
        {
            if (result == null || result.Kind != Program.AppearanceIdKind.Gear)
            {
                return null;
            }

            Wz_Node node = this.FindAppearanceNode(result);
            if (node == null)
            {
                return null;
            }

            Gear gear;
            try
            {
                gear = Gear.CreateFromNode(node, PluginManager.FindWz);
            }
            catch (Exception ex)
            {
                ErrorLog.Write(ex, $"MetadataResolver.RenderTooltipBitmap.CreateFromNode({result.Id})");
                return null;
            }

            if (gear == null)
            {
                return null;
            }

            TooltipRenderSettings settings = GetTooltipRenderSettings();
            bool use22Style = settings.Use22Style;
            GearGraphics.is22aniStyle = use22Style;

            try
            {
                if (use22Style)
                {
                    var renderer = new GearTooltipRender22
                    {
                        Gear = gear,
                        StringLinker = this.stringLinker,
                        ShowObjectID = settings.ShowObjectId,
                        ShowLevelOrSealed = settings.ShowLevelOrSealed,
                        MaxStar25 = settings.MaxStar25,
                        ShowCosmetic = settings.ShowCosmetic,
                        ShowCashPurchasePrice = settings.ShowCashPurchasePrice,
                    };
                    return renderer.Render();
                }
                else
                {
                    var renderer = new GearTooltipRender2
                    {
                        Gear = gear,
                        StringLinker = this.stringLinker,
                        ShowObjectID = settings.ShowObjectId,
                        ShowLevelOrSealed = settings.ShowLevelOrSealed,
                        MaxStar25 = settings.MaxStar25,
                        ShowCosmetic = settings.ShowCosmetic,
                        ShowCashPurchasePrice = settings.ShowCashPurchasePrice,
                        Enable22AniStyle = settings.Use22Style,
                    };
                    return renderer.Render();
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Write(ex, $"MetadataResolver.RenderTooltipBitmap.Render({result.Id})");
                return null;
            }
        }

        private static TooltipRenderSettings GetTooltipRenderSettings()
        {
            var settings = new TooltipRenderSettings();

            try
            {
                CharaSimConfig config = CharaSimConfig.Default;
                if (config == null)
                {
                    return settings;
                }

                if (config.Enable22AniStyle != null)
                {
                    settings.Use22Style = config.Enable22AniStyle.Value;
                }

                if (config.Gear != null)
                {
                    settings.ShowObjectId = config.Gear.ShowID;
                    settings.ShowLevelOrSealed = config.Gear.ShowLevelOrSealed;
                    settings.MaxStar25 = config.Gear.MaxStar25;
                    settings.ShowCosmetic = config.Gear.ShowCosmetic;
                    settings.ShowCashPurchasePrice = config.Gear.ShowPurchasePrice;
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Write(ex, "MetadataResolver.GetTooltipRenderSettings");
            }

            return settings;
        }

        private Wz_Node FindAppearanceNode(AppearanceSearchResult result)
        {
            switch (result.Kind)
            {
                case Program.AppearanceIdKind.Skin:
                    return Program.TryGetSkinNodes(result.Id, out Wz_Node bodyNode, out _) ? bodyNode : null;

                case Program.AppearanceIdKind.Face:
                    return Program.FindFaceNode(result.Id);

                case Program.AppearanceIdKind.Hair:
                    return Program.FindHairNode(result.Id);

                case Program.AppearanceIdKind.Gear:
                    return Program.FindGearNode(result.Id);

                default:
                    return null;
            }
        }

        private Wz_Node FindItemNode(int id)
        {
            foreach (string path in EnumerateItemNodePaths(id))
            {
                Wz_Node node = EnsureExtractedNode(PluginManager.FindWz(path));
                if (node != null)
                {
                    return node;
                }
            }

            return null;
        }

        private Wz_Node FindItemIconNode(int id)
        {
            Wz_Node node = this.FindItemNode(id);
            return node?.FindNodeByPath("info/icon") ?? node?.FindNodeByPath("info/iconRaw");
        }

        private static IEnumerable<string> EnumerateItemNodePaths(int id)
        {
            var paths = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void Add(string path)
            {
                if (!string.IsNullOrWhiteSpace(path) && seen.Add(path))
                {
                    paths.Add(path);
                }
            }

            Item.ItemType type = Item.GetItemType(id);
            if (type == Item.ItemType.Pet)
            {
                Add($@"Item\Pet\{id:D7}.img");
                Add($@"Item\Pet\{id}.img");
            }
            else if (type != Item.ItemType.Unknown)
            {
                string typeName = type.ToString();
                foreach (string nodePath in EnumerateRegularItemNodePaths(id))
                {
                    Add($@"Item\{typeName}\{nodePath}");
                }
            }

            Add($@"Item\Special\{id / 10000:D4}.img\{id}");
            Add($@"Item\Special\{id / 10000:D4}.img\{id:D8}");

            return paths;
        }

        private static IEnumerable<string> EnumerateRegularItemNodePaths(int id)
        {
            var paths = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void Add(string path)
            {
                if (!string.IsNullOrWhiteSpace(path) && seen.Add(path))
                {
                    paths.Add(path);
                }
            }

            if (id / 1000 == 3015)
            {
                Add($@"{id / 100:D6}.img\{id:D8}");
                Add($@"{id / 100:D6}.img\{id}");
            }
            else if (id / 10000 == 301)
            {
                Add($@"{id / 1000:D5}.img\{id:D8}");
                Add($@"{id / 1000:D5}.img\{id}");
            }
            else
            {
                Add($@"{id / 10000:D4}.img\{id:D8}");
                Add($@"{id / 10000:D4}.img\{id}");
            }

            return paths;
        }

        private static Wz_Node EnsureExtractedNode(Wz_Node node)
        {
            if (node == null)
            {
                return null;
            }

            Wz_Image image = node.GetValueEx<Wz_Image>(null);
            if (image != null)
            {
                return image.TryExtract() ? image.Node : null;
            }

            return node;
        }

        private static void PrepareItemTooltipData(int id)
        {
            CharaSimLoader.LoadCommoditiesIfEmpty();
            CharaSimLoader.LoadMsnMintableItemListIfEmpty();

            if (Item.GetItemType(id) == Item.ItemType.Pet)
            {
                CharaSimLoader.LoadSetItemsIfEmpty();
            }

            try
            {
                if (CharaSimConfig.Default?.Misc?.LocatePetEquip == true)
                {
                    CharaSimLoader.LoadPetEquipInfoIfEmpty();
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Write(ex, "MetadataResolver.PrepareItemTooltipData");
            }
        }

        private static bool MatchesSearch(string idText, string name, string kindLabel, string alias, string[] tokens)
        {
            if (tokens == null || tokens.Length == 0)
            {
                return true;
            }

            foreach (string token in tokens)
            {
                bool hitId = idText?.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
                bool hitName = name?.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
                bool hitKind = kindLabel?.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
                bool hitAlias = alias?.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
                if (!hitId && !hitName && !hitKind && !hitAlias)
                {
                    return false;
                }
            }

            return true;
        }

        private static string GetSearchAlias(AppearanceSearchResult result)
        {
            if (result == null)
            {
                return string.Empty;
            }

            string kindAlias = result.Kind switch
            {
                Program.AppearanceIdKind.Skin => "皮肤 肤色",
                Program.AppearanceIdKind.Face => "脸 脸型 表情",
                Program.AppearanceIdKind.Hair => "头发 发型",
                Program.AppearanceIdKind.Gear => "装备 外观 道具",
                Program.AppearanceIdKind.Item => "道具 物品 消耗 装饰 其它 其他 现金 宠物 特殊",
                _ => string.Empty,
            };

            if (result.Kind != Program.AppearanceIdKind.Skin)
            {
                return kindAlias;
            }

            string skinAliases = string.Join(" ", EnumerateSkinLookupIds(result.Id));
            return string.IsNullOrWhiteSpace(skinAliases)
                ? kindAlias
                : $"{kindAlias} {skinAliases}";
        }

        private static bool ShouldIndexSkinStringId(int id)
        {
            GearType type = Gear.GetGearType(id);
            return type == GearType.head
                || type == GearType.head_n
                || type == GearType.body;
        }

        private static int GetSkinDisplayId(int id)
        {
            GearType type = Gear.GetGearType(id);
            if (type == GearType.head || type == GearType.head_n)
            {
                return id;
            }

            int normalizedSkin = (id % 2000) + 2000;
            return normalizedSkin + 10000;
        }

        private static IEnumerable<int> EnumerateSkinLookupIds(int skin)
        {
            var ids = new HashSet<int>();

            void Add(int candidateId)
            {
                if (candidateId > 0)
                {
                    ids.Add(candidateId);
                }
            }

            Add(skin);

            GearType type = Gear.GetGearType(skin);
            if (type == GearType.head && skin >= 10000)
            {
                Add(skin - 10000);
            }
            else if (type == GearType.body)
            {
                Add(skin + 10000);
            }
            else
            {
                int normalizedSkin = (skin % 2000) + 2000;
                Add(normalizedSkin);
                Add(normalizedSkin + 10000);
            }

            return ids.OrderBy(id => id);
        }

        private static int[] ParseGearIds(string gearText)
        {
            return Program.ParseIdList(gearText, "Gear");
        }

        private static string BuildFileBaseName(IEnumerable<string> names, bool dyeMode, IEnumerable<string> actionNames)
        {
            var parts = names
                .Select(SanitizeFileNamePart)
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .ToList();

            parts.Add(dyeMode ? "染色模式" : "普通模式");
            parts.AddRange((actionNames ?? Enumerable.Empty<string>())
                .Select(SanitizeFileNamePart)
                .Where(part => !string.IsNullOrWhiteSpace(part)));

            if (parts.Count == 0)
            {
                return "avatar";
            }

            return string.Join("_", parts);
        }

        private static string SanitizeFileNamePart(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            char[] invalidChars = Path.GetInvalidFileNameChars();
            char[] cleanedChars = text
                .Trim()
                .Select(ch => invalidChars.Contains(ch) ? '_' : ch)
                .ToArray();

            string cleaned = new string(cleanedChars);
            while (cleaned.Contains("__", StringComparison.Ordinal))
            {
                cleaned = cleaned.Replace("__", "_", StringComparison.Ordinal);
            }

            return cleaned.Trim('_', ' ');
        }
    }

    internal sealed class ResolvedAppearancePreview
    {
        public List<ResolvedAppearanceEntry> Entries { get; set; }
        public string FileBaseName { get; set; }
        public string OutputPath { get; set; }
        public string PreviewText { get; set; }
        public bool CanExport { get; set; }
        public string ExportBlockReason { get; set; }
        public int? SkinId { get; set; }
        public int? FaceId { get; set; }
        public int? HairId { get; set; }
    }

    internal sealed class ResolvedAppearanceEntry
    {
        public ResolvedAppearanceEntry(string label, string name, int? id)
        {
            this.Label = label;
            this.Name = name;
            this.Id = id;
        }

        public string Label { get; }
        public string Name { get; }
        public int? Id { get; }
    }

    internal sealed class AppearanceSearchResult
    {
        public AppearanceSearchResult(int id, string name, Program.AppearanceIdKind kind, string kindLabel)
        {
            this.Id = id;
            this.Name = name;
            this.Kind = kind;
            this.KindLabel = kindLabel;
        }

        public int Id { get; }
        public string Name { get; }
        public Program.AppearanceIdKind Kind { get; }
        public string KindLabel { get; }
    }

    internal sealed class SearchPreviewImage
    {
        public SearchPreviewImage(Bitmap bitmap, bool isTooltip)
        {
            this.Bitmap = bitmap;
            this.IsTooltip = isTooltip;
        }

        public Bitmap Bitmap { get; }
        public bool IsTooltip { get; }
    }

    internal sealed class TooltipRenderSettings
    {
        public bool Use22Style { get; set; } = true;
        public bool ShowObjectId { get; set; } = true;
        public bool ShowLevelOrSealed { get; set; } = true;
        public bool MaxStar25 { get; set; }
        public bool ShowCosmetic { get; set; }
        public bool ShowCashPurchasePrice { get; set; } = true;
    }

    internal sealed class ExportSettingsForm : Form
    {
        private readonly List<CheckBox> normalActionChecks;
        private readonly List<RadioButton> dyeActionRadios;
        private readonly Button btnOk;

        public ExportSettingsForm(IEnumerable<string> selectedNormalActions, string selectedDyeAction)
        {
            string[] normalizedNormalActions = Program.NormalizeNormalActionSelection(selectedNormalActions);
            string normalizedDyeAction = Program.NormalizeDyeActionSelection(selectedDyeAction);

            this.normalActionChecks = new List<CheckBox>();
            this.dyeActionRadios = new List<RadioButton>();
            this.btnOk = new Button
            {
                Text = "确定",
                DialogResult = DialogResult.None,
                AutoSize = true,
                MinimumSize = new Size(96, 34),
                Margin = new Padding(0),
            };

            this.Text = "导出设置";
            this.StartPosition = FormStartPosition.CenterParent;
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.Padding = new Padding(12);
            this.ClientSize = new Size(640, 300);

            var rootLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
            };
            rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var bodyLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0),
            };
            bodyLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            bodyLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));

            bodyLayout.Controls.Add(
                this.BuildNormalActionsGroup(normalizedNormalActions),
                0,
                0);
            bodyLayout.Controls.Add(
                this.BuildDyeActionGroup(normalizedDyeAction),
                1,
                0);

            var footerLayout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                AutoSize = true,
                Margin = new Padding(0, 12, 0, 0),
            };

            var btnCancel = new Button
            {
                Text = "取消",
                DialogResult = DialogResult.Cancel,
                AutoSize = true,
                MinimumSize = new Size(96, 34),
                Margin = new Padding(8, 0, 0, 0),
            };

            this.btnOk.Click += this.BtnOk_Click;
            footerLayout.Controls.Add(btnCancel);
            footerLayout.Controls.Add(this.btnOk);

            rootLayout.Controls.Add(bodyLayout, 0, 0);
            rootLayout.Controls.Add(footerLayout, 0, 1);
            this.Controls.Add(rootLayout);

            this.AcceptButton = this.btnOk;
            this.CancelButton = btnCancel;
            this.UpdateOkButtonEnabled();
        }

        public IReadOnlyList<string> SelectedNormalActions => Program.NormalizeNormalActionSelection(
            this.normalActionChecks.Where(check => check.Checked).Select(check => check.Tag as string));

        public string SelectedDyeAction => Program.NormalizeDyeActionSelection(
            this.dyeActionRadios.FirstOrDefault(radio => radio.Checked)?.Tag as string);

        private GroupBox BuildNormalActionsGroup(IReadOnlyCollection<string> selectedNormalActions)
        {
            var group = new GroupBox
            {
                Text = "普通模式设置",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 8, 0),
                Padding = new Padding(12, 16, 12, 12),
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                AutoSize = true,
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            foreach (string actionName in Program.SupportedExportActions)
            {
                var checkBox = new CheckBox
                {
                    Text = actionName,
                    Tag = actionName,
                    AutoSize = true,
                    Checked = selectedNormalActions.Contains(actionName),
                    Margin = new Padding(0, 0, 0, 8),
                };
                checkBox.CheckedChanged += this.ActionSelectionControl_Changed;
                this.normalActionChecks.Add(checkBox);
                layout.Controls.Add(checkBox);
            }

            layout.Controls.Add(new Label
            {
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Margin = new Padding(0, 8, 0, 0),
                Text = "可多选，导出时会按勾选动作生成。",
            });

            group.Controls.Add(layout);
            return group;
        }

        private GroupBox BuildDyeActionGroup(string selectedDyeAction)
        {
            var group = new GroupBox
            {
                Text = "染色模式设置",
                Dock = DockStyle.Fill,
                Margin = new Padding(8, 0, 0, 0),
                Padding = new Padding(12, 16, 12, 12),
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                AutoSize = true,
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            foreach (string actionName in Program.SupportedExportActions)
            {
                var radioButton = new RadioButton
                {
                    Text = actionName,
                    Tag = actionName,
                    AutoSize = true,
                    Checked = string.Equals(actionName, selectedDyeAction, StringComparison.OrdinalIgnoreCase),
                    Margin = new Padding(0, 0, 0, 8),
                };
                radioButton.CheckedChanged += this.ActionSelectionControl_Changed;
                this.dyeActionRadios.Add(radioButton);
                layout.Controls.Add(radioButton);
            }

            layout.Controls.Add(new Label
            {
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Margin = new Padding(0, 8, 0, 0),
                Text = "染色模式只会使用一个动作。",
            });

            group.Controls.Add(layout);
            return group;
        }

        private void ActionSelectionControl_Changed(object sender, EventArgs e)
        {
            this.UpdateOkButtonEnabled();
        }

        private void UpdateOkButtonEnabled()
        {
            this.btnOk.Enabled = this.normalActionChecks.Any(check => check.Checked)
                && this.dyeActionRadios.Any(radio => radio.Checked);
        }

        private void BtnOk_Click(object sender, EventArgs e)
        {
            if (!this.normalActionChecks.Any(check => check.Checked))
            {
                MessageBox.Show(this, "普通模式至少需要勾选一个动作。", "导出设置", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!this.dyeActionRadios.Any(radio => radio.Checked))
            {
                MessageBox.Show(this, "染色模式需要选择一个动作。", "导出设置", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }

    internal sealed class HistoryTemplateButton : Button
    {
        private bool hovered;
        private bool pressed;
        private string[] lines = Array.Empty<string>();

        public HistoryTemplateButton()
        {
            this.SetStyle(
                ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw
                | ControlStyles.UserPaint,
                true);

            this.FlatStyle = FlatStyle.Flat;
            this.UseVisualStyleBackColor = false;
            this.BackColor = SystemColors.Control;
            this.ForeColor = SystemColors.ControlText;
            this.Cursor = Cursors.Hand;
            this.TabStop = false;
        }

        public void SetLines(IReadOnlyList<string> values)
        {
            this.lines = values?.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray() ?? Array.Empty<string>();
            this.Text = string.Join(Environment.NewLine, this.lines);
            this.Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            this.hovered = true;
            this.Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            this.hovered = false;
            this.pressed = false;
            this.Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            if (mevent.Button == MouseButtons.Left)
            {
                this.pressed = true;
                this.Invalidate();
            }

            base.OnMouseDown(mevent);
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            this.pressed = false;
            this.Invalidate();
            base.OnMouseUp(mevent);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            this.Invalidate();
            base.OnEnabledChanged(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Rectangle bounds = this.ClientRectangle;
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            Color backColor = !this.Enabled
                ? SystemColors.Control
                : this.pressed
                    ? Color.FromArgb(222, 232, 248)
                    : this.hovered
                        ? Color.FromArgb(242, 246, 252)
                        : SystemColors.Control;
            Color borderColor = !this.Enabled
                ? SystemColors.ControlDark
                : this.pressed || this.hovered
                    ? Color.SteelBlue
                    : SystemColors.ControlDark;
            Color textColor = this.Enabled ? this.ForeColor : SystemColors.GrayText;

            using (var brush = new SolidBrush(backColor))
            using (var pen = new Pen(borderColor))
            {
                e.Graphics.FillRectangle(brush, bounds);
                e.Graphics.DrawRectangle(pen, 0, 0, bounds.Width - 1, bounds.Height - 1);
            }

            Rectangle contentBounds = Rectangle.FromLTRB(
                this.Padding.Left,
                this.Padding.Top,
                bounds.Width - this.Padding.Right,
                bounds.Height - this.Padding.Bottom);

            int lineHeight = TextRenderer.MeasureText(e.Graphics, "肤", this.Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Height;
            int spacing = Math.Max(2, (contentBounds.Height - (lineHeight * Math.Max(1, this.lines.Length))) / Math.Max(1, this.lines.Length));
            spacing = Math.Min(spacing, 6);

            int y = contentBounds.Top;
            foreach (string line in this.lines)
            {
                Rectangle lineBounds = new Rectangle(contentBounds.Left, y, Math.Max(0, contentBounds.Width), lineHeight);
                TextRenderer.DrawText(
                    e.Graphics,
                    line,
                    this.Font,
                    lineBounds,
                    textColor,
                    TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
                y += lineHeight + spacing;
                if (y > contentBounds.Bottom)
                {
                    break;
                }
            }
        }
    }

    internal sealed class AlignedInputBox : UserControl
    {
        private readonly TextBox innerTextBox;
        private bool innerControlsReady;

        public AlignedInputBox()
        {
            this.SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer, true);
            this.TabStop = true;

            this.innerTextBox = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Multiline = false,
                AcceptsReturn = false,
                AcceptsTab = false,
                WordWrap = false,
                ScrollBars = ScrollBars.None,
                TabStop = false,
                Margin = Padding.Empty,
                BackColor = SystemColors.Window,
                ForeColor = SystemColors.WindowText,
            };

            this.innerTextBox.TextChanged += this.InnerTextBox_TextChanged;
            this.innerTextBox.GotFocus += this.InnerTextBox_GotFocus;
            this.innerTextBox.LostFocus += this.InnerTextBox_LostFocus;
            this.innerTextBox.KeyDown += this.InnerTextBox_KeyDown;

            this.Controls.Add(this.innerTextBox);
            this.innerControlsReady = true;

            this.BackColor = SystemColors.Window;
            this.ForeColor = SystemColors.WindowText;
            this.UpdateLayoutBounds();
        }

        public override string Text
        {
            get => this.innerTextBox.Text;
            set => this.innerTextBox.Text = value ?? string.Empty;
        }

        public string PlaceholderText
        {
            get => this.innerTextBox.PlaceholderText;
            set => this.innerTextBox.PlaceholderText = value ?? string.Empty;
        }

        public HorizontalAlignment TextAlign
        {
            get => this.innerTextBox.TextAlign;
            set
            {
                this.innerTextBox.TextAlign = value;
            }
        }

        public int SelectionStart
        {
            get => this.innerTextBox.SelectionStart;
            set => this.innerTextBox.SelectionStart = value;
        }

        public int SelectionLength
        {
            get => this.innerTextBox.SelectionLength;
            set => this.innerTextBox.SelectionLength = value;
        }

        public int TextLength => this.innerTextBox.TextLength;

        public new bool AutoSize
        {
            get => base.AutoSize;
            set => base.AutoSize = value;
        }

        public new bool Focus()
        {
            return this.innerTextBox.Focus();
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);

            if (!this.innerControlsReady)
            {
                return;
            }

            this.innerTextBox.Font = this.Font;
            this.UpdateLayoutBounds();
        }

        protected override void OnBackColorChanged(EventArgs e)
        {
            base.OnBackColorChanged(e);

            if (!this.innerControlsReady)
            {
                return;
            }

            this.innerTextBox.BackColor = this.BackColor;
            this.Invalidate();
        }

        protected override void OnForeColorChanged(EventArgs e)
        {
            base.OnForeColorChanged(e);

            if (!this.innerControlsReady)
            {
                return;
            }

            this.innerTextBox.ForeColor = this.ForeColor;
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);

            if (!this.innerControlsReady)
            {
                return;
            }

            this.innerTextBox.Enabled = this.Enabled;
            this.Invalidate();
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);

            if (!this.innerTextBox.Focused)
            {
                this.innerTextBox.Focus();
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            if (!this.innerControlsReady)
            {
                return;
            }

            this.UpdateLayoutBounds();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Rectangle bounds = new Rectangle(0, 0, Math.Max(0, this.Width - 1), Math.Max(0, this.Height - 1));
            System.Windows.Forms.VisualStyles.TextBoxState state = !this.Enabled
                ? System.Windows.Forms.VisualStyles.TextBoxState.Disabled
                : this.innerTextBox.Focused
                    ? System.Windows.Forms.VisualStyles.TextBoxState.Selected
                    : System.Windows.Forms.VisualStyles.TextBoxState.Normal;

            if (TextBoxRenderer.IsSupported)
            {
                TextBoxRenderer.DrawTextBox(e.Graphics, bounds, state);
            }
            else
            {
                using var brush = new SolidBrush(this.BackColor);
                using var pen = new Pen(SystemColors.WindowFrame);
                e.Graphics.FillRectangle(brush, bounds);
                e.Graphics.DrawRectangle(pen, bounds);
            }
        }

        private void InnerTextBox_TextChanged(object sender, EventArgs e)
        {
            this.OnTextChanged(e);
        }

        private void InnerTextBox_GotFocus(object sender, EventArgs e)
        {
            this.OnEnter(e);
            this.Invalidate();
        }

        private void InnerTextBox_LostFocus(object sender, EventArgs e)
        {
            this.OnLeave(e);
            this.Invalidate();
        }

        private void InnerTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            this.OnKeyDown(e);
        }

        private void UpdateLayoutBounds()
        {
            if (!this.innerControlsReady)
            {
                return;
            }

            int horizontalPadding = this.ScaleForCurrentDpi(8);
            int verticalInset = this.ScaleForCurrentDpi(3);
            int innerWidth = Math.Max(1, this.Width - (horizontalPadding * 2));
            int innerHeight = Math.Min(
                Math.Max(this.innerTextBox.PreferredHeight, TextRenderer.MeasureText("Mg", this.Font).Height),
                Math.Max(1, this.Height - (verticalInset * 2)));
            int innerTop = Math.Max(1, (this.Height - innerHeight) / 2);

            this.innerTextBox.Bounds = new Rectangle(horizontalPadding, innerTop, innerWidth, innerHeight);
        }

        private int ScaleForCurrentDpi(int logicalPixels)
        {
            return (int)Math.Round(logicalPixels * this.DeviceDpi / 96f);
        }
    }

    internal sealed class PreviewImageForm : Form
    {
        private readonly PictureBox pictureBox;

        public PreviewImageForm(AppearanceSearchResult result, SearchPreviewImage previewImage)
        {
            this.Text = $"{result.Name} ({result.Id})";
            this.AutoScaleMode = AutoScaleMode.None;
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.SizableToolWindow;
            this.KeyPreview = true;
            this.MaximizeBox = true;
            this.MinimizeBox = false;
            this.ClientSize = previewImage.IsTooltip
                ? new Size(Math.Min(980, Math.Max(420, previewImage.Bitmap.Width + 36)), Math.Min(900, Math.Max(240, previewImage.Bitmap.Height + 72)))
                : new Size(360, 260);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = previewImage.IsTooltip ? 1 : 2,
                Padding = new Padding(12),
            };
            if (previewImage.IsTooltip)
            {
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            }
            else
            {
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            }

            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = previewImage.IsTooltip ? Color.FromArgb(30, 30, 30) : SystemColors.Control,
            };

            this.pictureBox = new PictureBox
            {
                SizeMode = previewImage.IsTooltip ? PictureBoxSizeMode.AutoSize : PictureBoxSizeMode.CenterImage,
                Image = previewImage.Bitmap,
                Dock = previewImage.IsTooltip ? DockStyle.None : DockStyle.Fill,
            };

            panel.Controls.Add(this.pictureBox);

            if (previewImage.IsTooltip)
            {
                layout.Controls.Add(panel, 0, 0);
            }
            else
            {
                var label = new Label
                {
                    AutoSize = true,
                    Text = $"{result.KindLabel}  {result.Id}\r\n{result.Name}",
                };
                layout.Controls.Add(label, 0, 0);
                layout.Controls.Add(panel, 0, 1);
            }
            this.Controls.Add(layout);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                this.Close();
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            this.pictureBox.Image?.Dispose();
            base.OnFormClosed(e);
        }
    }

    internal sealed class DpiAwarenessScope : IDisposable
    {
        private static readonly IntPtr DpiAwarenessContextUnaware = new IntPtr(-1);
        private static readonly IntPtr DpiAwarenessContextUnawareGdiScaled = new IntPtr(-5);
        private readonly IntPtr previousContext;
        private bool disposed;

        private DpiAwarenessScope(IntPtr previousContext)
        {
            this.previousContext = previousContext;
        }

        public static DpiAwarenessScope EnterReferencePreviewMode()
        {
            IntPtr previous = SetThreadDpiAwarenessContext(DpiAwarenessContextUnawareGdiScaled);
            if (previous == IntPtr.Zero)
            {
                previous = SetThreadDpiAwarenessContext(DpiAwarenessContextUnaware);
            }

            return previous == IntPtr.Zero
                ? new DpiAwarenessScope(IntPtr.Zero)
                : new DpiAwarenessScope(previous);
        }

        public void Dispose()
        {
            if (this.disposed)
            {
                return;
            }

            if (this.previousContext != IntPtr.Zero)
            {
                SetThreadDpiAwarenessContext(this.previousContext);
            }

            this.disposed = true;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);
    }

}
