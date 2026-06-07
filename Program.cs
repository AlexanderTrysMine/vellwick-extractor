using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("Vellwick Extractor")]
[assembly: AssemblyDescription("Simple Windows zip extraction utility")]
[assembly: AssemblyProduct("Vellwick Extractor")]
[assembly: AssemblyCompany("Vellwick")]
[assembly: AssemblyCopyright("Copyright Vellwick")]
[assembly: AssemblyVersion("1.0.6.0")]
[assembly: AssemblyFileVersion("1.0.6.0")]

namespace VellwickExtractor
{
    internal static class Theme
    {
        public static readonly Color Window = Color.FromArgb(11, 17, 32);
        public static readonly Color Header = Color.FromArgb(2, 6, 23);
        public static readonly Color Surface = Color.FromArgb(15, 23, 42);
        public static readonly Color SurfaceRaised = Color.FromArgb(30, 41, 59);
        public static readonly Color SurfaceHover = Color.FromArgb(51, 65, 85);
        public static readonly Color Border = Color.FromArgb(71, 85, 105);
        public static readonly Color Primary = Color.FromArgb(37, 99, 235);
        public static readonly Color PrimaryHover = Color.FromArgb(29, 78, 216);
        public static readonly Color Accent = Color.FromArgb(56, 189, 248);
        public static readonly Color Text = Color.FromArgb(226, 232, 240);
        public static readonly Color MutedText = Color.FromArgb(148, 163, 184);
        public static readonly Color LogBackground = Color.FromArgb(2, 6, 23);
        public static readonly Color LogText = Color.FromArgb(203, 213, 225);
    }

    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            ExplorerContextMenu.TryRegister(Application.ExecutablePath);

            if (TryHandleCommandLine(args))
            {
                return;
            }

            Application.Run(new ExtractorForm());
        }

        private static bool TryHandleCommandLine(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                return false;
            }

            if (args.Length >= 2 && string.Equals(args[0], "--extract-all", StringComparison.OrdinalIgnoreCase))
            {
                var folderPath = PathInput.Normalize(args[1]);
                if (!Directory.Exists(folderPath))
                {
                    MessageBox.Show("Choose a valid folder.", "Vellwick Extractor", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return true;
                }

                try
                {
                    var result = ExtractorForm.ExtractAllInFolder(folderPath, true, null);
                    MessageBox.Show(
                        string.Format("Extracted all with Vellwick.{0}{0}Found {1}, extracted {2}, failed {3}.", Environment.NewLine, result.Found, result.Extracted, result.Failed),
                        "Vellwick Extractor",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Could not extract this folder." + Environment.NewLine + Environment.NewLine + ex.Message, "Vellwick Extractor", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                return true;
            }

            var zipPath = args.Length >= 2 && string.Equals(args[0], "--extract", StringComparison.OrdinalIgnoreCase)
                ? args[1]
                : args[0];

            zipPath = PathInput.Normalize(zipPath);
            if (!File.Exists(zipPath) || !string.Equals(Path.GetExtension(zipPath), ".zip", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Choose a valid .zip file.", "Vellwick Extractor", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return true;
            }

            try
            {
                var outputFolder = ExtractorForm.ExtractZip(zipPath);
                MessageBox.Show("Extracted with Vellwick:" + Environment.NewLine + outputFolder, "Vellwick Extractor", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not extract this zip file." + Environment.NewLine + Environment.NewLine + ex.Message, "Vellwick Extractor", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            return true;
        }
    }

    internal sealed class ExtractorForm : Form
    {
        private const string GitHubUrl = "https://github.com/AlexanderTrysMine/vellwick-extractor";
        private const string GitHubAccount = "AlexanderTrysMine";
        private const string VellwickSiteUrl = "https://vellwick.com";

        private readonly TextBox folderTextBox;
        private readonly Button browseButton;
        private readonly Button executeButton;
        private readonly CheckBox keepZipCheckBox;
        private readonly DarkProgressBar progressBar;
        private readonly Label statusLabel;
        private readonly TextBox logTextBox;
        private readonly Label foundValueLabel;
        private readonly Label extractedValueLabel;
        private readonly Label failedValueLabel;
        private readonly Label deletedValueLabel;
        private readonly BackgroundWorker worker;

        private bool isRunning;
        private bool updateCheckStarted;
        private BackgroundWorker updateWorker;

        public ExtractorForm()
        {
            Text = "Vellwick Extractor";
            Name = "Vellwick Extractor";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(740, 560);
            Size = new Size(900, 660);
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            BackColor = Theme.Window;
            AutoScaleMode = AutoScaleMode.Dpi;
            Icon = LogoPainter.CreateIcon(32);

            var page = new TableLayoutPanel();
            page.Dock = DockStyle.Fill;
            page.ColumnCount = 1;
            page.RowCount = 4;
            page.Padding = new Padding(22);
            page.BackColor = BackColor;
            page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            page.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            Controls.Add(page);

            var header = BuildHeader();
            page.Controls.Add(header, 0, 0);

            var folderPanel = BuildFolderPanel();
            page.Controls.Add(folderPanel, 0, 1);

            var controlsPanel = BuildControlsPanel();
            page.Controls.Add(controlsPanel, 0, 2);

            var logPanel = BuildLogPanel();
            page.Controls.Add(logPanel, 0, 3);

            folderTextBox = new TextBox();
            folderTextBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            folderTextBox.BorderStyle = BorderStyle.FixedSingle;
            folderTextBox.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            folderTextBox.BackColor = Theme.Surface;
            folderTextBox.ForeColor = Theme.Text;
            folderTextBox.AllowDrop = true;
            folderTextBox.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            folderTextBox.AutoCompleteSource = AutoCompleteSource.FileSystemDirectories;
            folderTextBox.ContextMenuStrip = BuildFolderContextMenu();
            folderTextBox.Margin = new Padding(0, 4, 8, 4);
            folderTextBox.TextChanged += FolderTextBox_TextChanged;
            folderTextBox.DragEnter += FolderTextBox_DragEnter;
            folderTextBox.DragDrop += FolderTextBox_DragDrop;
            folderTextBox.Leave += FolderTextBox_Leave;

            browseButton = CreateButton("Browse...");
            browseButton.Margin = new Padding(0, 4, 0, 4);
            browseButton.Click += BrowseButton_Click;

            folderPanel.Controls.Add(folderTextBox, 0, 1);
            folderPanel.Controls.Add(browseButton, 1, 1);

            keepZipCheckBox = new DarkCheckBox();
            keepZipCheckBox.Text = "Keep zip files after extraction";
            keepZipCheckBox.Checked = true;
            keepZipCheckBox.AutoSize = false;
            keepZipCheckBox.Size = new Size(250, 30);
            keepZipCheckBox.Margin = new Padding(0, 8, 16, 8);
            keepZipCheckBox.ForeColor = Theme.Text;

            executeButton = CreatePrimaryButton("Execute");
            executeButton.Margin = new Padding(0, 4, 0, 4);
            executeButton.Click += ExecuteButton_Click;

            controlsPanel.Controls.Add(keepZipCheckBox, 0, 0);
            controlsPanel.Controls.Add(executeButton, 1, 0);

            progressBar = new DarkProgressBar();
            progressBar.Dock = DockStyle.Fill;
            progressBar.Height = 14;
            progressBar.Margin = new Padding(0, 12, 0, 4);
            progressBar.Style = ProgressBarStyle.Continuous;

            statusLabel = new Label();
            statusLabel.AutoEllipsis = true;
            statusLabel.Dock = DockStyle.Fill;
            statusLabel.ForeColor = Theme.MutedText;
            statusLabel.Margin = new Padding(0, 0, 0, 8);
            statusLabel.Text = "Ready.";

            foundValueLabel = new Label();
            extractedValueLabel = new Label();
            failedValueLabel = new Label();
            deletedValueLabel = new Label();

            var statsPanel = BuildStatsPanel();
            logPanel.Controls.Add(statsPanel, 0, 0);
            logPanel.Controls.Add(progressBar, 0, 1);
            logPanel.Controls.Add(statusLabel, 0, 2);

            logTextBox = new TextBox();
            logTextBox.Dock = DockStyle.Fill;
            logTextBox.Multiline = true;
            logTextBox.ReadOnly = true;
            logTextBox.ScrollBars = ScrollBars.Vertical;
            logTextBox.BorderStyle = BorderStyle.FixedSingle;
            logTextBox.BackColor = Theme.LogBackground;
            logTextBox.ForeColor = Theme.LogText;
            logTextBox.Font = new Font("Consolas", 9F, FontStyle.Regular, GraphicsUnit.Point);
            logTextBox.Margin = new Padding(0);
            logPanel.Controls.Add(logTextBox, 0, 3);

            ResetStats();

            worker = new BackgroundWorker();
            worker.WorkerReportsProgress = true;
            worker.DoWork += Worker_DoWork;
            worker.ProgressChanged += Worker_ProgressChanged;
            worker.RunWorkerCompleted += Worker_RunWorkerCompleted;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            EnableDarkTitleBar();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            StartUpdateCheck();
        }

        private void EnableDarkTitleBar()
        {
            if (Environment.OSVersion.Version.Major < 10)
            {
                return;
            }

            var enabled = 1;
            if (DwmSetWindowAttribute(Handle, 20, ref enabled, Marshal.SizeOf(typeof(int))) != 0)
            {
                DwmSetWindowAttribute(Handle, 19, ref enabled, Marshal.SizeOf(typeof(int)));
            }
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int attributeValue, int attributeSize);

        private Control BuildHeader()
        {
            var header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 88;
            header.Margin = new Padding(0, 0, 0, 16);
            header.BackColor = Theme.Header;

            var accent = new Panel();
            accent.Dock = DockStyle.Left;
            accent.Width = 8;
            accent.BackColor = Theme.Accent;
            header.Controls.Add(accent);

            var layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 3;
            layout.RowCount = 1;
            layout.Padding = new Padding(18, 14, 18, 14);
            layout.BackColor = Color.Transparent;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            header.Controls.Add(layout);

            var logo = new VellwickLogoControl();
            logo.Width = 70;
            logo.Height = 58;
            logo.Margin = new Padding(0, 0, 18, 0);
            layout.Controls.Add(logo, 0, 0);

            var title = new Label();
            title.Text = "Vellwick Extractor";
            title.Font = new Font("Segoe UI Semibold", 18F, FontStyle.Bold, GraphicsUnit.Point);
            title.ForeColor = Theme.Text;
            title.AutoSize = false;
            title.Dock = DockStyle.Fill;
            title.TextAlign = ContentAlignment.MiddleLeft;
            title.Margin = new Padding(0);
            title.Cursor = Cursors.Hand;
            title.Click += VellwickTitle_Click;
            layout.Controls.Add(title, 1, 0);

            var gitHubLink = new GitHubHeaderLinkControl(GitHubUrl, GitHubAccount);
            gitHubLink.Width = 190;
            gitHubLink.Height = 52;
            gitHubLink.Margin = new Padding(20, 3, 0, 0);
            layout.Controls.Add(gitHubLink, 2, 0);

            return header;
        }

        private void VellwickTitle_Click(object sender, EventArgs e)
        {
            LinkLauncher.Open(this, VellwickSiteUrl, "Vellwick.com");
        }

        private void StartUpdateCheck()
        {
            if (updateCheckStarted)
            {
                return;
            }

            updateCheckStarted = true;
            updateWorker = new BackgroundWorker();
            updateWorker.DoWork += UpdateWorker_DoWork;
            updateWorker.RunWorkerCompleted += UpdateWorker_RunWorkerCompleted;
            updateWorker.RunWorkerAsync();
        }

        private void UpdateWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            e.Result = AppUpdater.CheckForUpdate();
        }

        private void UpdateWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            updateWorker = null;
            if (Disposing || IsDisposed || e.Cancelled || e.Error != null)
            {
                return;
            }

            var update = e.Result as UpdateInfo;
            if (update == null || isRunning)
            {
                return;
            }

            var message = "Vellwick Extractor " + update.DisplayVersion + " is available." +
                Environment.NewLine + Environment.NewLine +
                "Install it now and restart Vellwick Extractor?";

            if (MessageBox.Show(this, message, "Vellwick Extractor Update", MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes)
            {
                return;
            }

            InstallUpdate(update);
        }

        private void InstallUpdate(UpdateInfo update)
        {
            try
            {
                UseWaitCursor = true;
                browseButton.Enabled = false;
                executeButton.Enabled = false;
                keepZipCheckBox.Enabled = false;
                statusLabel.Text = "Downloading update...";

                AppUpdater.InstallAndRestart(update);
                statusLabel.Text = "Restarting to finish update...";
                Application.Exit();
            }
            catch (Exception ex)
            {
                UseWaitCursor = false;
                if (!isRunning)
                {
                    browseButton.Enabled = true;
                    executeButton.Enabled = true;
                    keepZipCheckBox.Enabled = true;
                }

                statusLabel.Text = "Update could not be installed.";
                MessageBox.Show(this, "Could not install the update." + Environment.NewLine + Environment.NewLine + ex.Message, "Vellwick Extractor", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private TableLayoutPanel BuildFolderPanel()
        {
            var panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Top;
            panel.AutoSize = true;
            panel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panel.ColumnCount = 2;
            panel.RowCount = 2;
            panel.Margin = new Padding(0, 0, 0, 10);
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var label = new Label();
            label.Text = "Folder";
            label.Dock = DockStyle.Fill;
            label.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold, GraphicsUnit.Point);
            label.ForeColor = Theme.Text;
            label.Margin = new Padding(0, 0, 0, 2);
            panel.Controls.Add(label, 0, 0);
            panel.SetColumnSpan(label, 2);

            return panel;
        }

        private TableLayoutPanel BuildControlsPanel()
        {
            var panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Top;
            panel.AutoSize = true;
            panel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panel.ColumnCount = 2;
            panel.RowCount = 1;
            panel.Margin = new Padding(0, 0, 0, 14);
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            return panel;
        }

        private TableLayoutPanel BuildLogPanel()
        {
            var panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Fill;
            panel.ColumnCount = 1;
            panel.RowCount = 4;
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            return panel;
        }

        private TableLayoutPanel BuildStatsPanel()
        {
            var panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Top;
            panel.AutoSize = true;
            panel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panel.ColumnCount = 4;
            panel.RowCount = 2;
            panel.Margin = new Padding(0, 0, 0, 2);
            for (int i = 0; i < 4; i++)
            {
                panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            }
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            AddStat(panel, "Found", foundValueLabel, 0);
            AddStat(panel, "Extracted", extractedValueLabel, 1);
            AddStat(panel, "Failed", failedValueLabel, 2);
            AddStat(panel, "Deleted", deletedValueLabel, 3);

            return panel;
        }

        private static void AddStat(TableLayoutPanel panel, string name, Label valueLabel, int column)
        {
            var nameLabel = new Label();
            nameLabel.Text = name;
            nameLabel.Dock = DockStyle.Fill;
            nameLabel.ForeColor = Theme.MutedText;
            nameLabel.Margin = new Padding(0, 0, 12, 0);
            nameLabel.AutoEllipsis = true;
            panel.Controls.Add(nameLabel, column, 0);

            valueLabel.Text = "0";
            valueLabel.Dock = DockStyle.Fill;
            valueLabel.ForeColor = Theme.Text;
            valueLabel.Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold, GraphicsUnit.Point);
            valueLabel.Margin = new Padding(0, 0, 12, 8);
            valueLabel.AutoEllipsis = true;
            panel.Controls.Add(valueLabel, column, 1);
        }

        private Button CreateButton(string text)
        {
            var button = new Button();
            button.Text = text;
            button.AutoSize = true;
            button.MinimumSize = new Size(112, 34);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = Theme.Border;
            button.FlatAppearance.MouseOverBackColor = Theme.SurfaceHover;
            button.BackColor = Theme.SurfaceRaised;
            button.ForeColor = Theme.Text;
            button.UseVisualStyleBackColor = false;
            return button;
        }

        private Button CreatePrimaryButton(string text)
        {
            var button = CreateButton(text);
            button.MinimumSize = new Size(132, 38);
            button.FlatAppearance.BorderColor = Theme.Primary;
            button.FlatAppearance.MouseOverBackColor = Theme.PrimaryHover;
            button.BackColor = Theme.Primary;
            button.ForeColor = Color.White;
            button.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold, GraphicsUnit.Point);
            return button;
        }

        private ContextMenuStrip BuildFolderContextMenu()
        {
            var menu = new ContextMenuStrip();
            menu.BackColor = Theme.SurfaceRaised;
            menu.ForeColor = Theme.Text;
            var paste = new ToolStripMenuItem("Paste", null, FolderPaste_Click);
            var clear = new ToolStripMenuItem("Clear", null, FolderClear_Click);
            menu.Items.Add(paste);
            menu.Items.Add(clear);
            menu.Opening += delegate
            {
                paste.Enabled = Clipboard.ContainsText();
                clear.Enabled = folderTextBox != null && folderTextBox.TextLength > 0;
            };
            return menu;
        }

        private void FolderPaste_Click(object sender, EventArgs e)
        {
            if (Clipboard.ContainsText())
            {
                folderTextBox.Text = PathInput.Normalize(Clipboard.GetText());
                folderTextBox.SelectionStart = folderTextBox.TextLength;
            }
        }

        private void FolderClear_Click(object sender, EventArgs e)
        {
            folderTextBox.Clear();
        }

        private void FolderTextBox_TextChanged(object sender, EventArgs e)
        {
            if (isRunning)
            {
                return;
            }

            var folder = PathInput.Normalize(folderTextBox.Text);
            if (folder.Length == 0)
            {
                statusLabel.Text = "Ready.";
            }
            else if (Directory.Exists(folder))
            {
                statusLabel.Text = "Ready to extract from this folder.";
            }
            else
            {
                statusLabel.Text = "Folder path has not been found yet.";
            }
        }

        private void FolderTextBox_Leave(object sender, EventArgs e)
        {
            var normalized = PathInput.Normalize(folderTextBox.Text);
            if (!string.Equals(folderTextBox.Text, normalized, StringComparison.Ordinal))
            {
                folderTextBox.Text = normalized;
            }
        }

        private void FolderTextBox_DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = GetDroppedFolder(e) == null ? DragDropEffects.None : DragDropEffects.Copy;
        }

        private void FolderTextBox_DragDrop(object sender, DragEventArgs e)
        {
            var folder = GetDroppedFolder(e);
            if (folder != null)
            {
                folderTextBox.Text = folder;
                folderTextBox.SelectionStart = folderTextBox.TextLength;
            }
        }

        private static string GetDroppedFolder(DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                return null;
            }

            var paths = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (paths == null || paths.Length == 0)
            {
                return null;
            }

            var folder = PathInput.Normalize(paths[0]);
            return Directory.Exists(folder) ? folder : null;
        }

        private void BrowseButton_Click(object sender, EventArgs e)
        {
            var currentPath = PathInput.Normalize(folderTextBox.Text);
            var selectedPath = NativeFolderPicker.PickFolder(this, currentPath);
            if (!string.IsNullOrWhiteSpace(selectedPath))
            {
                folderTextBox.Text = selectedPath;
                statusLabel.Text = "Ready.";
            }
        }

        private void ExecuteButton_Click(object sender, EventArgs e)
        {
            if (isRunning)
            {
                return;
            }

            var folder = PathInput.Normalize(folderTextBox.Text);
            folderTextBox.Text = folder;
            if (!Directory.Exists(folder))
            {
                MessageBox.Show(this, "Choose a valid folder first.", "Vellwick Extractor", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            isRunning = true;
            browseButton.Enabled = false;
            executeButton.Enabled = false;
            keepZipCheckBox.Enabled = false;
            executeButton.Text = "Working...";
            progressBar.Value = 0;
            progressBar.Style = ProgressBarStyle.Marquee;
            logTextBox.Clear();
            ResetStats();
            statusLabel.Text = "Scanning for zip files...";

            var request = new ExtractionRequest(folder, keepZipCheckBox.Checked);
            worker.RunWorkerAsync(request);
        }

        private void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            var request = (ExtractionRequest)e.Argument;
            var result = new ExtractionResult();
            var workerInstance = (BackgroundWorker)sender;

            var zipFiles = new List<string>();
            foreach (var zip in EnumerateZipFiles(request.RootFolder, workerInstance))
            {
                zipFiles.Add(zip);
            }

            result.Found = zipFiles.Count;
            workerInstance.ReportProgress(0, WorkerMessage.Stats(result));

            if (zipFiles.Count == 0)
            {
                workerInstance.ReportProgress(100, WorkerMessage.StatusText("No zip files found."));
                e.Result = result;
                return;
            }

            for (int i = 0; i < zipFiles.Count; i++)
            {
                var zipPath = zipFiles[i];
                var label = string.Format("[{0}/{1}] {2}", i + 1, zipFiles.Count, zipPath);
                workerInstance.ReportProgress(ProgressFor(i, zipFiles.Count), WorkerMessage.Log("Extracting " + label));

                var extracted = false;
                try
                {
                    var outputFolder = ExtractZip(zipPath);
                    result.Extracted++;
                    extracted = true;
                    workerInstance.ReportProgress(ProgressFor(i + 1, zipFiles.Count), WorkerMessage.Log("Extracted to " + outputFolder));
                }
                catch (Exception ex)
                {
                    result.Failed++;
                    workerInstance.ReportProgress(ProgressFor(i + 1, zipFiles.Count), WorkerMessage.Log("Failed: " + zipPath + Environment.NewLine + "  " + ex.Message));
                }

                if (extracted && !request.KeepZipFiles)
                {
                    try
                    {
                        File.Delete(zipPath);
                        result.Deleted++;
                        workerInstance.ReportProgress(ProgressFor(i + 1, zipFiles.Count), WorkerMessage.Log("Deleted original zip: " + zipPath));
                    }
                    catch (Exception ex)
                    {
                        result.Failed++;
                        workerInstance.ReportProgress(ProgressFor(i + 1, zipFiles.Count), WorkerMessage.Log("Could not delete original zip: " + zipPath + Environment.NewLine + "  " + ex.Message));
                    }
                }

                workerInstance.ReportProgress(ProgressFor(i + 1, zipFiles.Count), WorkerMessage.Stats(result));
            }

            e.Result = result;
        }

        private void Worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (progressBar.Style != ProgressBarStyle.Continuous)
            {
                progressBar.Style = ProgressBarStyle.Continuous;
            }

            var progress = Math.Max(0, Math.Min(100, e.ProgressPercentage));
            progressBar.Value = progress;

            var message = e.UserState as WorkerMessage;
            if (message == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(message.Status))
            {
                statusLabel.Text = message.Status;
            }

            if (!string.IsNullOrEmpty(message.LogLine))
            {
                AppendLog(message.LogLine);
            }

            if (message.StatsValue != null)
            {
                ApplyStats(message.StatsValue);
            }
        }

        private void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            isRunning = false;
            browseButton.Enabled = true;
            executeButton.Enabled = true;
            keepZipCheckBox.Enabled = true;
            executeButton.Text = "Execute";
            progressBar.Style = ProgressBarStyle.Continuous;
            progressBar.Value = 100;

            var result = e.Result as ExtractionResult;
            if (e.Error != null)
            {
                statusLabel.Text = "Stopped with an error.";
                AppendLog("Error: " + e.Error.Message);
                MessageBox.Show(this, e.Error.Message, "Vellwick Extractor", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (result == null)
            {
                statusLabel.Text = "Finished.";
                return;
            }

            ApplyStats(result);
            statusLabel.Text = string.Format("Finished. Found {0}, extracted {1}, failed {2}, deleted {3}.", result.Found, result.Extracted, result.Failed, result.Deleted);
        }

        internal static ExtractionResult ExtractAllInFolder(string rootFolder, bool keepZipFiles, Action<string> log)
        {
            var result = new ExtractionResult();
            var zipFiles = new List<string>();
            foreach (var zip in EnumerateZipFiles(rootFolder, null))
            {
                zipFiles.Add(zip);
            }

            result.Found = zipFiles.Count;
            for (int i = 0; i < zipFiles.Count; i++)
            {
                var zipPath = zipFiles[i];
                try
                {
                    if (log != null)
                    {
                        log("Extracting " + zipPath);
                    }

                    ExtractZip(zipPath);
                    result.Extracted++;

                    if (!keepZipFiles)
                    {
                        File.Delete(zipPath);
                        result.Deleted++;
                    }
                }
                catch (Exception ex)
                {
                    result.Failed++;
                    if (log != null)
                    {
                        log("Failed: " + zipPath + "  " + ex.Message);
                    }
                }
            }

            return result;
        }

        private static IEnumerable<string> EnumerateZipFiles(string root, BackgroundWorker workerInstance)
        {
            var pending = new Stack<string>();
            pending.Push(root);

            while (pending.Count > 0)
            {
                var current = pending.Pop();
                string[] files = new string[0];
                string[] directories = new string[0];

                try
                {
                    files = Directory.GetFiles(current, "*.zip", SearchOption.TopDirectoryOnly);
                }
                catch (Exception ex)
                {
                    if (workerInstance != null)
                    {
                        workerInstance.ReportProgress(0, WorkerMessage.Log("Could not scan files in " + current + Environment.NewLine + "  " + ex.Message));
                    }
                }

                for (int i = 0; i < files.Length; i++)
                {
                    yield return files[i];
                }

                try
                {
                    directories = Directory.GetDirectories(current);
                }
                catch (Exception ex)
                {
                    if (workerInstance != null)
                    {
                        workerInstance.ReportProgress(0, WorkerMessage.Log("Could not scan subfolders in " + current + Environment.NewLine + "  " + ex.Message));
                    }
                }

                for (int i = 0; i < directories.Length; i++)
                {
                    pending.Push(directories[i]);
                }
            }
        }

        internal static string ExtractZip(string zipPath)
        {
            var zipDirectory = Path.GetDirectoryName(zipPath);
            if (string.IsNullOrEmpty(zipDirectory))
            {
                zipDirectory = Environment.CurrentDirectory;
            }

            var baseName = Path.GetFileNameWithoutExtension(zipPath);
            if (string.IsNullOrWhiteSpace(baseName))
            {
                baseName = "Extracted";
            }

            var outputFolder = GetUniqueDirectory(Path.Combine(zipDirectory, baseName));
            Directory.CreateDirectory(outputFolder);

            var outputRoot = Path.GetFullPath(outputFolder);
            if (!outputRoot.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                outputRoot += Path.DirectorySeparatorChar;
            }

            using (var archive = ZipFile.OpenRead(zipPath))
            {
                foreach (var entry in archive.Entries)
                {
                    var destination = Path.GetFullPath(Path.Combine(outputFolder, entry.FullName));
                    if (!destination.StartsWith(outputRoot, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException("The archive contains an unsafe path: " + entry.FullName);
                    }

                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(destination);
                        continue;
                    }

                    var destinationDirectory = Path.GetDirectoryName(destination);
                    if (!string.IsNullOrEmpty(destinationDirectory))
                    {
                        Directory.CreateDirectory(destinationDirectory);
                    }

                    entry.ExtractToFile(GetUniqueFilePath(destination));
                }
            }

            return SurfaceSingleChildFolders(outputFolder);
        }

        private static string SurfaceSingleChildFolders(string folder)
        {
            var current = folder;
            for (int i = 0; i < 32; i++)
            {
                string[] files;
                string[] directories;
                try
                {
                    files = Directory.GetFiles(current, "*", SearchOption.TopDirectoryOnly);
                    directories = Directory.GetDirectories(current, "*", SearchOption.TopDirectoryOnly);
                }
                catch
                {
                    return current;
                }

                if (files.Length != 0 || directories.Length != 1)
                {
                    return current;
                }

                var parent = Directory.GetParent(current);
                if (parent == null)
                {
                    return current;
                }

                var child = directories[0];
                var childName = Path.GetFileName(child);
                if (string.IsNullOrWhiteSpace(childName))
                {
                    return current;
                }

                var desiredDestination = Path.Combine(parent.FullName, childName);
                string destination;
                if (string.Equals(Path.GetFullPath(desiredDestination).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), Path.GetFullPath(current).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                {
                    var temporary = GetUniqueDirectory(Path.Combine(parent.FullName, childName + " Update"));
                    Directory.Move(child, temporary);
                    Directory.Delete(current, false);
                    Directory.Move(temporary, desiredDestination);
                    destination = desiredDestination;
                }
                else
                {
                    destination = GetUniqueDirectory(desiredDestination);
                    Directory.Move(child, destination);
                    Directory.Delete(current, false);
                }

                current = destination;
            }

            return current;
        }

        private static string GetUniqueDirectory(string preferredPath)
        {
            if (!Directory.Exists(preferredPath) && !File.Exists(preferredPath))
            {
                return preferredPath;
            }

            for (int i = 2; i < 10000; i++)
            {
                var candidate = preferredPath + " (" + i + ")";
                if (!Directory.Exists(candidate) && !File.Exists(candidate))
                {
                    return candidate;
                }
            }

            throw new IOException("Could not create a unique output folder for " + preferredPath);
        }

        private static string GetUniqueFilePath(string preferredPath)
        {
            if (!File.Exists(preferredPath) && !Directory.Exists(preferredPath))
            {
                return preferredPath;
            }

            var directory = Path.GetDirectoryName(preferredPath);
            var name = Path.GetFileNameWithoutExtension(preferredPath);
            var extension = Path.GetExtension(preferredPath);

            for (int i = 2; i < 10000; i++)
            {
                var candidate = Path.Combine(directory, name + " (" + i + ")" + extension);
                if (!File.Exists(candidate) && !Directory.Exists(candidate))
                {
                    return candidate;
                }
            }

            throw new IOException("Could not create a unique file path for " + preferredPath);
        }

        private static int ProgressFor(int completed, int total)
        {
            if (total <= 0)
            {
                return 0;
            }

            return Math.Max(0, Math.Min(100, (int)Math.Round((completed * 100.0) / total)));
        }

        private void AppendLog(string text)
        {
            if (logTextBox.TextLength > 0)
            {
                logTextBox.AppendText(Environment.NewLine);
            }

            logTextBox.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + text);
        }

        private void ResetStats()
        {
            var empty = new ExtractionResult();
            ApplyStats(empty);
        }

        private void ApplyStats(ExtractionResult stats)
        {
            foundValueLabel.Text = stats.Found.ToString();
            extractedValueLabel.Text = stats.Extracted.ToString();
            failedValueLabel.Text = stats.Failed.ToString();
            deletedValueLabel.Text = stats.Deleted.ToString();
        }
    }

    internal sealed class ExtractionRequest
    {
        public ExtractionRequest(string rootFolder, bool keepZipFiles)
        {
            RootFolder = rootFolder;
            KeepZipFiles = keepZipFiles;
        }

        public string RootFolder { get; private set; }
        public bool KeepZipFiles { get; private set; }
    }

    internal sealed class ExtractionResult
    {
        public int Found { get; set; }
        public int Extracted { get; set; }
        public int Failed { get; set; }
        public int Deleted { get; set; }
    }

    internal sealed class WorkerMessage
    {
        private WorkerMessage()
        {
        }

        public string Status { get; private set; }
        public string LogLine { get; private set; }
        public ExtractionResult StatsValue { get; private set; }

        public static WorkerMessage StatusText(string status)
        {
            return new WorkerMessage { Status = status };
        }

        public static WorkerMessage Log(string logLine)
        {
            return new WorkerMessage { LogLine = logLine, Status = logLine.Replace(Environment.NewLine, " ") };
        }

        public static WorkerMessage Stats(ExtractionResult stats)
        {
            return new WorkerMessage
            {
                StatsValue = new ExtractionResult
                {
                    Found = stats.Found,
                    Extracted = stats.Extracted,
                    Failed = stats.Failed,
                    Deleted = stats.Deleted
                }
            };
        }
    }

    internal sealed class UpdateInfo
    {
        public Version Version { get; set; }
        public string DisplayVersion { get; set; }
        public string DownloadUrl { get; set; }
        public string ReleaseUrl { get; set; }
    }

    internal static class AppUpdater
    {
        private const string LatestReleaseApiUrl = "https://api.github.com/repos/AlexanderTrysMine/vellwick-extractor/releases/latest";
        private const string UserAgent = "Vellwick-Extractor";

        public static UpdateInfo CheckForUpdate()
        {
            try
            {
                EnableModernTls();
                string json;
                using (var client = CreateWebClient())
                {
                    json = client.DownloadString(LatestReleaseApiUrl);
                }

                var tagName = MatchJsonString(json, "tag_name");
                var downloadUrl = MatchFirstExeDownloadUrl(json);
                if (string.IsNullOrWhiteSpace(tagName) || string.IsNullOrWhiteSpace(downloadUrl))
                {
                    return null;
                }

                var latest = ParseTagVersion(tagName);
                if (latest == null)
                {
                    return null;
                }

                var current = NormalizeVersion(Assembly.GetExecutingAssembly().GetName().Version);
                if (NormalizeVersion(latest).CompareTo(current) <= 0)
                {
                    return null;
                }

                return new UpdateInfo
                {
                    Version = latest,
                    DisplayVersion = tagName,
                    DownloadUrl = downloadUrl,
                    ReleaseUrl = MatchJsonString(json, "html_url")
                };
            }
            catch
            {
                return null;
            }
        }

        public static void InstallAndRestart(UpdateInfo update)
        {
            if (update == null || string.IsNullOrWhiteSpace(update.DownloadUrl))
            {
                throw new InvalidOperationException("No update download was found.");
            }

            EnableModernTls();
            var tempRoot = Path.Combine(Path.GetTempPath(), "VellwickExtractorUpdate");
            Directory.CreateDirectory(tempRoot);

            var downloadPath = Path.Combine(tempRoot, "Vellwick Extractor " + SafeFileToken(update.DisplayVersion) + ".exe");
            using (var client = CreateWebClient())
            {
                client.DownloadFile(update.DownloadUrl, downloadPath);
            }

            if (!File.Exists(downloadPath) || new FileInfo(downloadPath).Length == 0)
            {
                throw new IOException("The update download did not complete.");
            }

            var scriptPath = Path.Combine(tempRoot, "finish-update.ps1");
            File.WriteAllText(scriptPath, BuildUpdaterScript(Process.GetCurrentProcess().Id, downloadPath, Application.ExecutablePath, tempRoot), Encoding.ASCII);

            var startInfo = new ProcessStartInfo("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -File " + QuoteArgument(scriptPath));
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            Process.Start(startInfo);
        }

        private static WebClient CreateWebClient()
        {
            var client = new WebClient();
            client.Headers[HttpRequestHeader.UserAgent] = UserAgent;
            client.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
            return client;
        }

        private static void EnableModernTls()
        {
            ServicePointManager.SecurityProtocol = ServicePointManager.SecurityProtocol | (SecurityProtocolType)3072;
        }

        private static string MatchJsonString(string json, string propertyName)
        {
            var match = Regex.Match(json, "\"" + Regex.Escape(propertyName) + "\"\\s*:\\s*\"((?:\\\\.|[^\"])*)\"", RegexOptions.Singleline);
            return match.Success ? DecodeJsonString(match.Groups[1].Value) : null;
        }

        private static string MatchFirstExeDownloadUrl(string json)
        {
            var matches = Regex.Matches(json, "\"browser_download_url\"\\s*:\\s*\"((?:\\\\.|[^\"])*)\"", RegexOptions.Singleline);
            foreach (Match match in matches)
            {
                var value = DecodeJsonString(match.Groups[1].Value);
                if (value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    return value;
                }
            }

            return null;
        }

        private static string DecodeJsonString(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            try
            {
                return Regex.Unescape(value.Replace("\\/", "/"));
            }
            catch
            {
                return value.Replace("\\/", "/");
            }
        }

        private static Version ParseTagVersion(string tagName)
        {
            var match = Regex.Match(tagName, "\\d+(?:\\.\\d+){0,3}");
            if (!match.Success)
            {
                return null;
            }

            try
            {
                return new Version(match.Value);
            }
            catch
            {
                return null;
            }
        }

        private static Version NormalizeVersion(Version version)
        {
            if (version == null)
            {
                return new Version(0, 0, 0, 0);
            }

            return new Version(
                Math.Max(0, version.Major),
                Math.Max(0, version.Minor),
                Math.Max(0, version.Build),
                Math.Max(0, version.Revision));
        }

        private static string BuildUpdaterScript(int processId, string sourcePath, string targetPath, string tempRoot)
        {
            var builder = new StringBuilder();
            builder.AppendLine("$ErrorActionPreference = 'Stop'");
            builder.AppendLine("$pidToWait = " + processId);
            builder.AppendLine("$source = '" + EscapePowerShellSingleQuoted(sourcePath) + "'");
            builder.AppendLine("$target = '" + EscapePowerShellSingleQuoted(targetPath) + "'");
            builder.AppendLine("$tempRoot = '" + EscapePowerShellSingleQuoted(tempRoot) + "'");
            builder.AppendLine("try { Wait-Process -Id $pidToWait -Timeout 45 -ErrorAction SilentlyContinue } catch {}");
            builder.AppendLine("Start-Sleep -Milliseconds 500");
            builder.AppendLine("Copy-Item -LiteralPath $source -Destination $target -Force");
            builder.AppendLine("Start-Process -FilePath $target");
            builder.AppendLine("Start-Sleep -Seconds 2");
            builder.AppendLine("Remove-Item -LiteralPath $source -Force -ErrorAction SilentlyContinue");
            builder.AppendLine("Remove-Item -LiteralPath $MyInvocation.MyCommand.Path -Force -ErrorAction SilentlyContinue");
            builder.AppendLine("try { Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue } catch {}");
            return builder.ToString();
        }

        private static string EscapePowerShellSingleQuoted(string value)
        {
            return (value ?? string.Empty).Replace("'", "''");
        }

        private static string QuoteArgument(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }

        private static string SafeFileToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "update";
            }

            var cleaned = value.Trim();
            var invalid = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalid.Length; i++)
            {
                cleaned = cleaned.Replace(invalid[i], '-');
            }

            return cleaned.Length == 0 ? "update" : cleaned;
        }
    }

    internal static class PathInput
    {
        public static string Normalize(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var cleaned = text.Trim();
            while (cleaned.Length >= 2 && cleaned.StartsWith("\"", StringComparison.Ordinal) && cleaned.EndsWith("\"", StringComparison.Ordinal))
            {
                cleaned = cleaned.Substring(1, cleaned.Length - 2).Trim();
            }

            return Environment.ExpandEnvironmentVariables(cleaned);
        }
    }

    internal static class LinkLauncher
    {
        public static void Open(IWin32Window owner, string url, string name)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(owner, "Could not open " + name + "." + Environment.NewLine + url + Environment.NewLine + Environment.NewLine + ex.Message, "Vellwick Extractor", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }

    internal static class ExplorerContextMenu
    {
        private const string ExtractVerbName = "VellwickExtract";
        private const string ExtractAllVerbName = "VellwickExtractAll";
        private const string ExtractText = "Extract with Vellwick";
        private const string ExtractAllText = "Extract all with Vellwick";

        [DllImport("shell32.dll")]
        private static extern void SHChangeNotify(uint eventId, uint flags, IntPtr item1, IntPtr item2);

        public static bool TryRegister(string executablePath)
        {
            try
            {
                Register(executablePath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void Register(string executablePath)
        {
            if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
            {
                throw new FileNotFoundException("The Vellwick Extractor executable was not found.", executablePath);
            }

            var extractCommand = "\"" + executablePath + "\" --extract \"%1\"";
            RegisterShellVerb(@"Software\Classes\SystemFileAssociations\.zip\shell\" + ExtractVerbName, ExtractText, executablePath, extractCommand);
            RegisterShellVerb(@"Software\Classes\CompressedFolder\shell\" + ExtractVerbName, ExtractText, executablePath, extractCommand);

            var extractAllCommand = "\"" + executablePath + "\" --extract-all \"%1\"";
            RegisterShellVerb(@"Software\Classes\Directory\shell\" + ExtractAllVerbName, ExtractAllText, executablePath, extractAllCommand);
            RegisterShellVerb(@"Software\Classes\Drive\shell\" + ExtractAllVerbName, ExtractAllText, executablePath, extractAllCommand);

            var extractAllBackgroundCommand = "\"" + executablePath + "\" --extract-all \"%V\"";
            RegisterShellVerb(@"Software\Classes\Directory\Background\shell\" + ExtractAllVerbName, ExtractAllText, executablePath, extractAllBackgroundCommand);

            SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);
        }

        private static void RegisterShellVerb(string shellKeyPath, string menuText, string executablePath, string command)
        {
            using (var shellKey = Registry.CurrentUser.CreateSubKey(shellKeyPath))
            {
                if (shellKey == null)
                {
                    throw new InvalidOperationException("Could not create the registry key for " + shellKeyPath);
                }

                shellKey.SetValue("", menuText);
                shellKey.SetValue("MUIVerb", menuText);
                shellKey.SetValue("Icon", "\"" + executablePath + "\",0");

                using (var commandKey = shellKey.CreateSubKey("command"))
                {
                    if (commandKey == null)
                    {
                        throw new InvalidOperationException("Could not create the command registry key for " + shellKeyPath);
                    }

                    commandKey.SetValue("", command);
                }
            }
        }
    }

    internal static class NativeFolderPicker
    {
        private const uint FOS_NOCHANGEDIR = 0x00000008;
        private const uint FOS_PICKFOLDERS = 0x00000020;
        private const uint FOS_FORCEFILESYSTEM = 0x00000040;
        private const uint FOS_PATHMUSTEXIST = 0x00000800;
        private const uint SIGDN_FILESYSPATH = 0x80058000;
        private const int ERROR_CANCELLED = unchecked((int)0x800704C7);

        public static string PickFolder(IWin32Window owner, string initialPath)
        {
            try
            {
                var selected = PickFolderWithNativeDialog(owner, initialPath);
                if (!string.IsNullOrWhiteSpace(selected))
                {
                    return selected;
                }

                return null;
            }
            catch
            {
                return PickFolderWithFallbackDialog(owner, initialPath);
            }
        }

        private static string PickFolderWithNativeDialog(IWin32Window owner, string initialPath)
        {
            IFileOpenDialog dialog = null;
            IShellItem initialFolder = null;
            IShellItem result = null;
            try
            {
                dialog = (IFileOpenDialog)new FileOpenDialog();
                uint options;
                dialog.GetOptions(out options);
                dialog.SetOptions(options | FOS_PICKFOLDERS | FOS_FORCEFILESYSTEM | FOS_PATHMUSTEXIST | FOS_NOCHANGEDIR);
                dialog.SetTitle("Choose folder");
                dialog.SetOkButtonLabel("Select Folder");

                if (Directory.Exists(initialPath))
                {
                    var shellItemGuid = typeof(IShellItem).GUID;
                    SHCreateItemFromParsingName(initialPath, IntPtr.Zero, ref shellItemGuid, out initialFolder);
                    dialog.SetFolder(initialFolder);
                }

                var hr = dialog.Show(owner == null ? IntPtr.Zero : owner.Handle);
                if (hr == ERROR_CANCELLED)
                {
                    return null;
                }

                if (hr != 0)
                {
                    Marshal.ThrowExceptionForHR(hr);
                }

                dialog.GetResult(out result);
                IntPtr pathPointer;
                result.GetDisplayName(SIGDN_FILESYSPATH, out pathPointer);
                try
                {
                    return Marshal.PtrToStringUni(pathPointer);
                }
                finally
                {
                    Marshal.FreeCoTaskMem(pathPointer);
                }
            }
            finally
            {
                if (result != null) Marshal.FinalReleaseComObject(result);
                if (initialFolder != null) Marshal.FinalReleaseComObject(initialFolder);
                if (dialog != null) Marshal.FinalReleaseComObject(dialog);
            }
        }

        private static string PickFolderWithFallbackDialog(IWin32Window owner, string initialPath)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Choose the folder that contains zip files.";
                dialog.ShowNewFolderButton = false;
                if (Directory.Exists(initialPath))
                {
                    dialog.SelectedPath = initialPath;
                }

                return dialog.ShowDialog(owner) == DialogResult.OK ? dialog.SelectedPath : null;
            }
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        private static extern void SHCreateItemFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
            IntPtr pbc,
            ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out IShellItem ppv
        );

        [ComImport]
        [Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")]
        private class FileOpenDialog
        {
        }

        [ComImport]
        [Guid("D57C7288-D4AD-4768-BE02-9D969532D960")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IFileOpenDialog
        {
            [PreserveSig]
            int Show(IntPtr parent);
            void SetFileTypes(uint cFileTypes, IntPtr rgFilterSpec);
            void SetFileTypeIndex(uint iFileType);
            void GetFileTypeIndex(out uint piFileType);
            void Advise(IntPtr pfde, out uint pdwCookie);
            void Unadvise(uint dwCookie);
            void SetOptions(uint fos);
            void GetOptions(out uint pfos);
            void SetDefaultFolder(IShellItem psi);
            void SetFolder(IShellItem psi);
            void GetFolder(out IShellItem ppsi);
            void GetCurrentSelection(out IShellItem ppsi);
            void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
            void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
            void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
            void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
            void GetResult(out IShellItem ppsi);
            void AddPlace(IShellItem psi, int fdap);
            void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
            void Close(int hr);
            void SetClientGuid(ref Guid guid);
            void ClearClientData();
            void SetFilter(IntPtr pFilter);
            void GetResults(out IntPtr ppenum);
            void GetSelectedItems(out IntPtr ppsai);
        }

        [ComImport]
        [Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem
        {
            void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
            void GetParent(out IShellItem ppsi);
            void GetDisplayName(uint sigdnName, out IntPtr ppszName);
            void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
            void Compare(IShellItem psi, uint hint, out int piOrder);
        }
    }

    internal sealed class VellwickLogoControl : Control
    {
        public VellwickLogoControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Theme.Header;
            MinimumSize = new Size(42, 42);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Theme.Header);
            LogoPainter.Paint(e.Graphics, Rectangle.Inflate(ClientRectangle, -1, -1), false);
        }
    }

    internal sealed class DarkCheckBox : CheckBox
    {
        public DarkCheckBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Theme.Window;
            ForeColor = Theme.Text;
            Cursor = Cursors.Hand;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            var boxSize = Math.Min(18, Math.Max(14, Height - 8));
            var boxTop = (Height - boxSize) / 2;
            var box = new Rectangle(1, boxTop, boxSize, boxSize);

            using (var boxBrush = new SolidBrush(Enabled ? Theme.Surface : Theme.SurfaceRaised))
            using (var borderPen = new Pen(Checked ? Theme.Accent : Theme.Border))
            {
                e.Graphics.FillRectangle(boxBrush, box);
                e.Graphics.DrawRectangle(borderPen, box);
            }

            if (Checked)
            {
                using (var checkPen = new Pen(Color.White, 2F))
                {
                    checkPen.StartCap = LineCap.Round;
                    checkPen.EndCap = LineCap.Round;
                    var left = box.Left + 4;
                    var mid = box.Left + box.Width / 2 - 1;
                    var right = box.Right - 4;
                    var lower = box.Top + box.Height - 5;
                    var center = box.Top + box.Height / 2 + 2;
                    var upper = box.Top + 5;
                    e.Graphics.DrawLines(checkPen, new[] { new Point(left, center), new Point(mid, lower), new Point(right, upper) });
                }
            }

            var textRect = new Rectangle(box.Right + 8, 0, Math.Max(0, Width - box.Right - 8), Height);
            TextRenderer.DrawText(
                e.Graphics,
                Text,
                Font,
                textRect,
                Enabled ? Theme.Text : Theme.MutedText,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }

    internal sealed class DarkProgressBar : Control
    {
        private readonly Timer marqueeTimer;
        private int value;
        private int marqueeOffset;
        private ProgressBarStyle style = ProgressBarStyle.Continuous;

        public DarkProgressBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Theme.Window;
            MinimumSize = new Size(80, 12);

            marqueeTimer = new Timer();
            marqueeTimer.Interval = 30;
            marqueeTimer.Tick += delegate
            {
                marqueeOffset = (marqueeOffset + 7) % Math.Max(1, Width + 80);
                Invalidate();
            };
        }

        public int Value
        {
            get { return value; }
            set
            {
                this.value = Math.Max(0, Math.Min(100, value));
                Invalidate();
            }
        }

        public ProgressBarStyle Style
        {
            get { return style; }
            set
            {
                style = value;
                marqueeOffset = 0;
                if (style == ProgressBarStyle.Marquee)
                {
                    marqueeTimer.Start();
                }
                else
                {
                    marqueeTimer.Stop();
                }

                Invalidate();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                marqueeTimer.Dispose();
            }

            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            var track = new Rectangle(0, Math.Max(0, (Height - 10) / 2), Width - 1, Math.Min(10, Height - 1));
            using (var trackBrush = new SolidBrush(Theme.SurfaceRaised))
            using (var borderPen = new Pen(Theme.Border))
            {
                e.Graphics.FillRectangle(trackBrush, track);
                e.Graphics.DrawRectangle(borderPen, track);
            }

            Rectangle fill;
            if (style == ProgressBarStyle.Marquee)
            {
                var barWidth = Math.Max(42, Width / 4);
                fill = new Rectangle(marqueeOffset - barWidth, track.Top + 1, barWidth, Math.Max(1, track.Height - 1));
            }
            else
            {
                fill = new Rectangle(track.Left + 1, track.Top + 1, Math.Max(0, (track.Width - 1) * value / 100), Math.Max(1, track.Height - 1));
            }

            if (fill.Width > 0)
            {
                using (var fillBrush = new SolidBrush(Theme.Primary))
                {
                    e.Graphics.FillRectangle(fillBrush, fill);
                }
            }
        }
    }

    internal sealed class GitHubHeaderLinkControl : Control
    {
        private readonly string url;
        private readonly string accountName;
        private bool isHovering;

        public GitHubHeaderLinkControl(string url, string accountName)
        {
            this.url = url;
            this.accountName = accountName;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Theme.Header;
            MinimumSize = new Size(170, 44);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            isHovering = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            isHovering = false;
            Invalidate();
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            LinkLauncher.Open(FindForm(), url, "the GitHub link");
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Theme.Header);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;

            var logoSize = 18;
            using (var linkFont = new Font("Segoe UI Semibold", 9.25F, isHovering ? FontStyle.Bold | FontStyle.Underline : FontStyle.Bold, GraphicsUnit.Point))
            using (var accountFont = new Font("Segoe UI", 8.25F, FontStyle.Regular, GraphicsUnit.Point))
            {
                var linkText = "GitHub";
                var textSize = TextRenderer.MeasureText(e.Graphics, linkText, linkFont, new Size(Width, Height), TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
                var rowWidth = logoSize + 7 + textSize.Width;
                var startX = Math.Max(0, Width - rowWidth);
                var logoRect = new Rectangle(startX, 4, logoSize, logoSize);

                using (var logoBack = new SolidBrush(Color.White))
                {
                    e.Graphics.FillEllipse(logoBack, logoRect);
                }

                var githubMark = EmbeddedImages.GitHubMark;
                if (githubMark != null)
                {
                    LogoPainter.DrawImageContained(e.Graphics, githubMark, Rectangle.Inflate(logoRect, -2, -2));
                }

                var linkRect = new Rectangle(logoRect.Right + 7, 1, Width - logoRect.Right - 7, 24);
                TextRenderer.DrawText(e.Graphics, linkText, linkFont, linkRect, Theme.Accent, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);

                var accountRect = new Rectangle(0, 28, Width, 18);
                TextRenderer.DrawText(e.Graphics, accountName, accountFont, accountRect, Theme.MutedText, TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            }
        }
    }

    internal static class EmbeddedImages
    {
        private const string VellwickMarkResource = "VellwickExtractor.Assets.VellwickMarkDark.png";
        private const string GitHubMarkResource = "VellwickExtractor.Assets.GitHubMark.png";

        private static Image vellwickMark;
        private static Image githubMark;

        public static Image VellwickMark
        {
            get
            {
                if (vellwickMark == null)
                {
                    vellwickMark = LoadImage(VellwickMarkResource);
                }

                return vellwickMark;
            }
        }

        public static Image GitHubMark
        {
            get
            {
                if (githubMark == null)
                {
                    githubMark = LoadImage(GitHubMarkResource);
                }

                return githubMark;
            }
        }

        private static Image LoadImage(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    return null;
                }

                using (var image = Image.FromStream(stream))
                {
                    return new Bitmap(image);
                }
            }
        }
    }

    internal static class LogoPainter
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        public static Icon CreateIcon(int size)
        {
            using (var bitmap = new Bitmap(size, size))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Transparent);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var bounds = new Rectangle(0, 0, size - 1, size - 1);
                using (var backgroundPath = RoundedRectangle(bounds, Math.Max(6, size / 5)))
                using (var background = new SolidBrush(Color.FromArgb(17, 24, 39)))
                {
                    graphics.FillPath(background, backgroundPath);
                }

                Paint(graphics, Rectangle.Inflate(bounds, -4, -4), false);
                var handle = bitmap.GetHicon();
                try
                {
                    return (Icon)Icon.FromHandle(handle).Clone();
                }
                finally
                {
                    DestroyIcon(handle);
                }
            }
        }

        public static void Paint(Graphics graphics, Rectangle bounds, bool includeWordmark)
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;

            var mark = EmbeddedImages.VellwickMark;
            if (mark != null)
            {
                DrawImageContained(graphics, mark, bounds);
                return;
            }

            var size = Math.Min(bounds.Width, bounds.Height);
            if (size <= 0)
            {
                return;
            }

            using (var pen = new Pen(Color.White, Math.Max(4, size / 8)))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                var left = new PointF(bounds.Left + size * 0.24F, bounds.Top + size * 0.28F);
                var bottom = new PointF(bounds.Left + size * 0.48F, bounds.Top + size * 0.73F);
                var right = new PointF(bounds.Left + size * 0.78F, bounds.Top + size * 0.28F);
                graphics.DrawLines(pen, new[] { left, bottom, right });
            }
        }

        public static void DrawImageContained(Graphics graphics, Image image, Rectangle bounds)
        {
            if (image == null || bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            var imageRatio = image.Width / (float)image.Height;
            var boundsRatio = bounds.Width / (float)bounds.Height;
            int width;
            int height;
            if (imageRatio > boundsRatio)
            {
                width = bounds.Width;
                height = Math.Max(1, (int)Math.Round(width / imageRatio));
            }
            else
            {
                height = bounds.Height;
                width = Math.Max(1, (int)Math.Round(height * imageRatio));
            }

            var x = bounds.Left + (bounds.Width - width) / 2;
            var y = bounds.Top + (bounds.Height - height) / 2;
            graphics.DrawImage(image, new Rectangle(x, y, width, height));
        }

        private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            var diameter = radius * 2;
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
