using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("Vellwick Extractor")]
[assembly: AssemblyDescription("Simple Windows zip extraction utility")]
[assembly: AssemblyProduct("Vellwick Extractor")]
[assembly: AssemblyCompany("Vellwick")]
[assembly: AssemblyCopyright("Copyright Vellwick")]
[assembly: AssemblyVersion("1.0.2.0")]
[assembly: AssemblyFileVersion("1.0.2.0")]

namespace VellwickExtractor
{
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

        private readonly TextBox folderTextBox;
        private readonly Button browseButton;
        private readonly Button executeButton;
        private readonly CheckBox keepZipCheckBox;
        private readonly ProgressBar progressBar;
        private readonly Label statusLabel;
        private readonly TextBox logTextBox;
        private readonly Label foundValueLabel;
        private readonly Label extractedValueLabel;
        private readonly Label failedValueLabel;
        private readonly Label deletedValueLabel;
        private readonly BackgroundWorker worker;

        private bool isRunning;

        public ExtractorForm()
        {
            Text = "Vellwick Extractor";
            Name = "Vellwick Extractor";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(740, 560);
            Size = new Size(900, 660);
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            BackColor = Color.FromArgb(245, 247, 250);
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

            keepZipCheckBox = new CheckBox();
            keepZipCheckBox.Text = "Keep zip files after extraction";
            keepZipCheckBox.Checked = true;
            keepZipCheckBox.AutoSize = true;
            keepZipCheckBox.Margin = new Padding(0, 8, 16, 8);
            keepZipCheckBox.ForeColor = Color.FromArgb(37, 49, 65);

            executeButton = CreatePrimaryButton("Execute");
            executeButton.Margin = new Padding(0, 4, 0, 4);
            executeButton.Click += ExecuteButton_Click;

            controlsPanel.Controls.Add(keepZipCheckBox, 0, 0);
            controlsPanel.Controls.Add(executeButton, 1, 0);

            progressBar = new ProgressBar();
            progressBar.Dock = DockStyle.Fill;
            progressBar.Height = 14;
            progressBar.Margin = new Padding(0, 12, 0, 4);
            progressBar.Style = ProgressBarStyle.Continuous;

            statusLabel = new Label();
            statusLabel.AutoEllipsis = true;
            statusLabel.Dock = DockStyle.Fill;
            statusLabel.ForeColor = Color.FromArgb(71, 85, 105);
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
            logTextBox.BackColor = Color.White;
            logTextBox.ForeColor = Color.FromArgb(30, 41, 59);
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

        private Control BuildHeader()
        {
            var header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 88;
            header.Margin = new Padding(0, 0, 0, 16);
            header.BackColor = Color.FromArgb(31, 41, 55);

            var accent = new Panel();
            accent.Dock = DockStyle.Left;
            accent.Width = 8;
            accent.BackColor = Color.FromArgb(37, 99, 235);
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
            title.ForeColor = Color.White;
            title.AutoSize = false;
            title.Dock = DockStyle.Fill;
            title.TextAlign = ContentAlignment.MiddleLeft;
            title.Margin = new Padding(0);
            layout.Controls.Add(title, 1, 0);

            var gitHubLink = new GitHubHeaderLinkControl(GitHubUrl, GitHubAccount);
            gitHubLink.Width = 190;
            gitHubLink.Height = 52;
            gitHubLink.Margin = new Padding(20, 3, 0, 0);
            layout.Controls.Add(gitHubLink, 2, 0);

            return header;
        }

        private TableLayoutPanel BuildFolderPanel()
        {
            var panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Top;
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
            label.ForeColor = Color.FromArgb(30, 41, 59);
            label.Margin = new Padding(0, 0, 0, 2);
            panel.Controls.Add(label, 0, 0);
            panel.SetColumnSpan(label, 2);

            return panel;
        }

        private TableLayoutPanel BuildControlsPanel()
        {
            var panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Top;
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
            nameLabel.ForeColor = Color.FromArgb(100, 116, 139);
            nameLabel.Margin = new Padding(0, 0, 12, 0);
            nameLabel.AutoEllipsis = true;
            panel.Controls.Add(nameLabel, column, 0);

            valueLabel.Text = "0";
            valueLabel.Dock = DockStyle.Fill;
            valueLabel.ForeColor = Color.FromArgb(15, 23, 42);
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
            button.FlatAppearance.BorderColor = Color.FromArgb(148, 163, 184);
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(226, 232, 240);
            button.BackColor = Color.White;
            button.ForeColor = Color.FromArgb(30, 41, 59);
            button.UseVisualStyleBackColor = false;
            return button;
        }

        private Button CreatePrimaryButton(string text)
        {
            var button = CreateButton(text);
            button.MinimumSize = new Size(132, 38);
            button.FlatAppearance.BorderColor = Color.FromArgb(37, 99, 235);
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(29, 78, 216);
            button.BackColor = Color.FromArgb(37, 99, 235);
            button.ForeColor = Color.White;
            button.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold, GraphicsUnit.Point);
            return button;
        }

        private ContextMenuStrip BuildFolderContextMenu()
        {
            var menu = new ContextMenuStrip();
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
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Choose the folder that contains zip files.";
                dialog.ShowNewFolderButton = false;
                if (Directory.Exists(PathInput.Normalize(folderTextBox.Text)))
                {
                    dialog.SelectedPath = PathInput.Normalize(folderTextBox.Text);
                }

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    folderTextBox.Text = dialog.SelectedPath;
                    statusLabel.Text = "Ready.";
                }
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

            return outputFolder;
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

    internal sealed class VellwickLogoControl : Control
    {
        public VellwickLogoControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            MinimumSize = new Size(42, 42);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            LogoPainter.Paint(e.Graphics, ClientRectangle, false);
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
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(FindForm(), "Could not open the GitHub link." + Environment.NewLine + url + Environment.NewLine + Environment.NewLine + ex.Message, "Vellwick Extractor", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
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
                TextRenderer.DrawText(e.Graphics, linkText, linkFont, linkRect, Color.FromArgb(191, 219, 254), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);

                var accountRect = new Rectangle(0, 28, Width, 18);
                TextRenderer.DrawText(e.Graphics, accountName, accountFont, accountRect, Color.FromArgb(203, 213, 225), TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
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
