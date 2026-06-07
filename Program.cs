using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Windows.Forms;

[assembly: AssemblyTitle("Vellwick Extractor")]
[assembly: AssemblyDescription("Simple Windows zip extraction utility")]
[assembly: AssemblyProduct("Vellwick Extractor")]
[assembly: AssemblyCompany("Vellwick")]
[assembly: AssemblyCopyright("Copyright Vellwick")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace VellwickExtractor
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new ExtractorForm());
        }
    }

    internal sealed class ExtractorForm : Form
    {
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
            MinimumSize = new Size(720, 500);
            Size = new Size(860, 590);
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            BackColor = Color.FromArgb(245, 247, 250);
            AutoScaleMode = AutoScaleMode.Dpi;

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
            folderTextBox.ReadOnly = true;
            folderTextBox.Margin = new Padding(0, 4, 8, 4);

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
            header.Height = 86;
            header.Margin = new Padding(0, 0, 0, 16);
            header.BackColor = Color.FromArgb(31, 41, 55);

            var accent = new Panel();
            accent.Dock = DockStyle.Left;
            accent.Width = 8;
            accent.BackColor = Color.FromArgb(37, 99, 235);
            header.Controls.Add(accent);

            var title = new Label();
            title.Text = "Vellwick Extractor";
            title.Font = new Font("Segoe UI Semibold", 18F, FontStyle.Bold, GraphicsUnit.Point);
            title.ForeColor = Color.White;
            title.AutoSize = false;
            title.Dock = DockStyle.Top;
            title.Height = 42;
            title.Padding = new Padding(22, 14, 22, 0);
            header.Controls.Add(title);

            var subtitle = new Label();
            subtitle.Text = "Select a folder, choose whether to keep the original zip files, then execute.";
            subtitle.Font = new Font("Segoe UI", 9.75F, FontStyle.Regular, GraphicsUnit.Point);
            subtitle.ForeColor = Color.FromArgb(203, 213, 225);
            subtitle.AutoEllipsis = true;
            subtitle.AutoSize = false;
            subtitle.Dock = DockStyle.Fill;
            subtitle.Padding = new Padding(22, 0, 22, 8);
            header.Controls.Add(subtitle);

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

        private void BrowseButton_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Choose the folder that contains zip files.";
                dialog.ShowNewFolderButton = false;
                if (Directory.Exists(folderTextBox.Text))
                {
                    dialog.SelectedPath = folderTextBox.Text;
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

            var folder = folderTextBox.Text.Trim();
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
                    workerInstance.ReportProgress(0, WorkerMessage.Log("Could not scan files in " + current + Environment.NewLine + "  " + ex.Message));
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
                    workerInstance.ReportProgress(0, WorkerMessage.Log("Could not scan subfolders in " + current + Environment.NewLine + "  " + ex.Message));
                }

                for (int i = 0; i < directories.Length; i++)
                {
                    pending.Push(directories[i]);
                }
            }
        }

        private static string ExtractZip(string zipPath)
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
}
