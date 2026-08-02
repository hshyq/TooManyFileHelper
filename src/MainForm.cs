using System.Windows.Forms;

namespace 文件名导出工具;

public partial class MainForm : Form
{
    private enum UIState { Idle, Scanning, Exporting, Done }

    private UIState _currentState = UIState.Idle;
    private System.Windows.Forms.Timer _dotTimer = new();
    private int _dotCount;
    private List<FileEntry>? _scanResult;

    public MainForm()
    {
        InitializeComponent();

        _dotTimer.Interval = 500;
        _dotTimer.Tick += DotTimer_Tick;

        // 默认导出文件名
        txtExportFileName.Text = $"文件名导出_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
    }

    /// <summary>
    /// 全量扫描阶段点动画：1→6 个点循环
    /// </summary>
    private void DotTimer_Tick(object? sender, EventArgs e)
    {
        _dotCount = (_dotCount % 6) + 1;
        lblStatus.Text = "全量扫描中，即将开始任务" + new string('.', _dotCount);
    }

    /// <summary>
    /// 统一管理 UI 控件状态
    /// </summary>
    private void SetUIState(UIState state)
    {
        _currentState = state;

        bool enabled = state is UIState.Idle or UIState.Done;
        btnStart.Enabled = enabled;
        btnBrowseFolder.Enabled = enabled;
        btnBrowseExport.Enabled = enabled;
        btnOpenOutput.Enabled = state is UIState.Done;
        txtFolderPath.ReadOnly = state is not UIState.Idle;
        txtExportPath.ReadOnly = state is not UIState.Idle;
        txtExportFileName.ReadOnly = state is not UIState.Idle;
        chkIncludeHidden.Enabled = enabled;
    }

    /// <summary>
    /// 清空上次扫描结果
    /// </summary>
    private void ClearResults()
    {
        _scanResult = null;
        progressBar.Value = 0;
        lblPercent.Text = "";
        lblStatus.Text = "";
        lblFileCount.Text = "";
        lblSummary.Text = "";
    }

    private async void btnStart_Click(object? sender, EventArgs e)
    {
        // ---- 验证输入 ----
        string folderPath = txtFolderPath.Text.Trim();
        if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
        {
            MessageBox.Show("请选择有效的目标文件夹。", "路径错误",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string exportDir = txtExportPath.Text.Trim();
        if (string.IsNullOrEmpty(exportDir))
        {
            MessageBox.Show("请设置导出位置。", "路径错误",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (!Directory.Exists(exportDir))
        {
            MessageBox.Show("导出位置目录不存在。", "路径错误",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string exportFileName = txtExportFileName.Text.Trim();
        if (string.IsNullOrEmpty(exportFileName))
        {
            MessageBox.Show("请输入导出文件名。", "文件名错误",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (!exportFileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            exportFileName += ".xlsx";
            txtExportFileName.Text = exportFileName;
        }

        // ---- 开始扫描 ----
        SetUIState(UIState.Scanning);
        ClearResults();

        bool includeHidden = chkIncludeHidden.Checked;
        var scanner = new FileScanner();
        var scanProgress = new Progress<ScanProgress>(OnScanProgress);

        _dotCount = 0;
        _dotTimer.Start();
        progressBar.Style = ProgressBarStyle.Marquee;
        progressBar.MarqueeAnimationSpeed = 50;
        lblPercent.Text = "";

        List<FileEntry> files;
        try
        {
            files = await Task.Run(() => scanner.ScanAsync(
                folderPath, includeHidden, scanProgress, CancellationToken.None));
        }
        catch (Exception ex)
        {
            _dotTimer.Stop();
            progressBar.Style = ProgressBarStyle.Continuous;
            progressBar.MarqueeAnimationSpeed = 100;
            MessageBox.Show("扫描出错：" + ex.Message, "错误",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetUIState(UIState.Idle);
            return;
        }

        _dotTimer.Stop();
        _scanResult = files;

        // ---- 导出阶段 ----
        SetUIState(UIState.Exporting);
        lblExportHint.Visible = true;

        string savePath = Path.Combine(exportDir, exportFileName);

        // 检查同名文件
        if (File.Exists(savePath))
        {
            var result = MessageBox.Show(
                "文件 \"" + exportFileName + "\" 已存在，是否覆盖？",
                "确认覆盖",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (result == DialogResult.No)
            {
                lblExportHint.Visible = false;
                SetUIState(UIState.Done);
                return;
            }
        }

        try
        {
            var exporter = new ExcelExporter();
            await exporter.ExportAsync(files, savePath);
        }
        catch (Exception ex)
        {
            MessageBox.Show("导出失败：" + ex.Message, "错误",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            lblExportHint.Visible = false;
            SetUIState(UIState.Done);
        }
    }

    /// <summary>
    /// 接收 FileScanner 报告的扫描进度，更新 UI
    /// </summary>
    private void OnScanProgress(ScanProgress p)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => OnScanProgress(p));
            return;
        }

        switch (p.Phase)
        {
            case ScanPhase.Collecting:
                // Marquee 样式 + 点动画由 _dotTimer 驱动
                if (p.SkippedPath != null)
                {
                    lblStatus.Text = "已跳过（权限不足）：" + p.SkippedPath;
                }
                break;

            case ScanPhase.Reading:
                _dotTimer.Stop();
                progressBar.Style = ProgressBarStyle.Continuous;
                progressBar.MarqueeAnimationSpeed = 100;
                progressBar.Value = Math.Clamp(p.Percent, 0, 100);
                lblPercent.Text = p.Percent + "%";
                lblStatus.Text = p.CurrentFile;
                lblFileCount.Text = p.FileCount.ToString("N0") + " 个文件";
                break;

            case ScanPhase.Done:
                _dotTimer.Stop();
                progressBar.Style = ProgressBarStyle.Continuous;
                progressBar.MarqueeAnimationSpeed = 100;
                progressBar.Value = 100;
                lblPercent.Text = "100%";
                lblFileCount.Text = p.FileCount.ToString("N0") + " 个文件";
                lblSummary.Text = "扫描完成，共 " + p.FileCount.ToString("N0") + " 个文件";
                break;
        }
    }

    private void btnBrowseFolder_Click(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog();
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            txtFolderPath.Text = dialog.SelectedPath;
        }
    }

    private void btnBrowseExport_Click(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog();
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            txtExportPath.Text = dialog.SelectedPath;
        }
    }

    private void btnOpenOutput_Click(object? sender, EventArgs e)
    {
        string exportDir = txtExportPath.Text.Trim();
        if (!string.IsNullOrEmpty(exportDir) && Directory.Exists(exportDir))
        {
            System.Diagnostics.Process.Start("explorer.exe", exportDir);
        }
    }
}