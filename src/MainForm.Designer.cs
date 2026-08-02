using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

#nullable disable

namespace 文件名导出工具;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    private void InitializeComponent()
    {
        this.labelFolder = new Label();
        this.txtFolderPath = new TextBox();
        this.btnBrowseFolder = new Button();
        this.chkIncludeHidden = new CheckBox();
        this.labelExport = new Label();
        this.txtExportPath = new TextBox();
        this.btnBrowseExport = new Button();
        this.btnOpenOutput = new Button();
        this.txtExportFileName = new TextBox();
        this.btnStart = new Button();
        this.lblExportHint = new Label();
        this.labelProgress = new Label();
        this.progressBar = new ProgressBar();
        this.lblPercent = new Label();
        this.labelStatus = new Label();
        this.lblStatus = new Label();
        this.labelCount = new Label();
        this.lblFileCount = new Label();
        this.lblSummary = new Label();
        this.SuspendLayout();

        // labelFolder
        this.labelFolder.AutoSize = false;
        this.labelFolder.Location = new Point(12, 15);
        this.labelFolder.Size = new Size(110, 23);
        this.labelFolder.Text = "目标文件夹:";
        this.labelFolder.TextAlign = ContentAlignment.MiddleLeft;

        // txtFolderPath
        this.txtFolderPath.Location = new Point(127, 15);
        this.txtFolderPath.Size = new Size(350, 23);
        this.txtFolderPath.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        // btnBrowseFolder
        this.btnBrowseFolder.Location = new Point(483, 10);
        this.btnBrowseFolder.Size = new Size(65, 32);
        this.btnBrowseFolder.Text = "浏览";
        this.btnBrowseFolder.UseVisualStyleBackColor = true;
        this.btnBrowseFolder.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        this.btnBrowseFolder.Click += btnBrowseFolder_Click;

        // chkIncludeHidden
        this.chkIncludeHidden.AutoSize = true;
        this.chkIncludeHidden.Location = new Point(15, 48);
        this.chkIncludeHidden.Size = new Size(184, 24);
        this.chkIncludeHidden.Text = "包含隐藏文件和文件夹";
        this.chkIncludeHidden.UseVisualStyleBackColor = true;

        // labelExport
        this.labelExport.AutoSize = false;
        this.labelExport.Location = new Point(12, 75);
        this.labelExport.Size = new Size(110, 23);
        this.labelExport.Text = "导出位置:";
        this.labelExport.TextAlign = ContentAlignment.MiddleLeft;

        // txtExportPath
        this.txtExportPath.Location = new Point(127, 75);
        this.txtExportPath.Size = new Size(260, 23);
        this.txtExportPath.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        // btnBrowseExport
        this.btnBrowseExport.Location = new Point(408, 70);
        this.btnBrowseExport.Size = new Size(65, 32);
        this.btnBrowseExport.Text = "浏览";
        this.btnBrowseExport.UseVisualStyleBackColor = true;
        this.btnBrowseExport.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        this.btnBrowseExport.Click += btnBrowseExport_Click;

        // btnOpenOutput
        this.btnOpenOutput.Enabled = false;
        this.btnOpenOutput.Location = new Point(464, 70);
        this.btnOpenOutput.Size = new Size(115, 32);
        this.btnOpenOutput.Text = "打开文件夹";
        this.btnOpenOutput.UseVisualStyleBackColor = true;
        this.btnOpenOutput.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        this.btnOpenOutput.Click += btnOpenOutput_Click;

        // txtExportFileName
        this.txtExportFileName.Location = new Point(127, 105);
        this.txtExportFileName.Size = new Size(260, 23);

        // btnStart
        this.btnStart.Location = new Point(250, 135);
        this.btnStart.Size = new Size(100, 30);
        this.btnStart.Text = "开始";
        this.btnStart.UseVisualStyleBackColor = true;
        this.btnStart.Click += btnStart_Click;

        // lblExportHint
        this.lblExportHint.AutoSize = true;
        this.lblExportHint.ForeColor = Color.Red;
        this.lblExportHint.Location = new Point(12, 175);
        this.lblExportHint.Size = new Size(312, 20);
        this.lblExportHint.Text = "文件生成中，请勿删除、重命名或移动文件";
        this.lblExportHint.Visible = false;

        // labelProgress
        this.labelProgress.AutoSize = false;
        this.labelProgress.Location = new Point(12, 205);
        this.labelProgress.Size = new Size(110, 23);
        this.labelProgress.Text = "进度:";
        this.labelProgress.TextAlign = ContentAlignment.MiddleLeft;

        // progressBar
        this.progressBar.Location = new Point(127, 203);
        this.progressBar.Size = new Size(345, 23);
        this.progressBar.Style = ProgressBarStyle.Continuous;
        this.progressBar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        // lblPercent
        this.lblPercent.AutoSize = true;
        this.lblPercent.Location = new Point(478, 205);
        this.lblPercent.Size = new Size(0, 20);
        this.lblPercent.Text = "";

        // labelStatus
        this.labelStatus.AutoSize = false;
        this.labelStatus.Location = new Point(12, 235);
        this.labelStatus.Size = new Size(110, 23);
        this.labelStatus.Text = "状态:";
        this.labelStatus.TextAlign = ContentAlignment.MiddleLeft;

        // lblStatus
        this.lblStatus.AutoSize = true;
        this.lblStatus.Location = new Point(127, 235);
        this.lblStatus.Size = new Size(500, 20);
        this.lblStatus.Text = "";
        this.lblStatus.MaximumSize = new Size(500, 0);

        // labelCount
        this.labelCount.AutoSize = false;
        this.labelCount.Location = new Point(12, 265);
        this.labelCount.Size = new Size(110, 23);
        this.labelCount.Text = "已发现:";
        this.labelCount.TextAlign = ContentAlignment.MiddleLeft;

        // lblFileCount
        this.lblFileCount.AutoSize = true;
        this.lblFileCount.Location = new Point(127, 265);
        this.lblFileCount.Size = new Size(200, 20);
        this.lblFileCount.Text = "";

        // lblSummary
        this.lblSummary.BorderStyle = BorderStyle.FixedSingle;
        this.lblSummary.Location = new Point(12, 295);
        this.lblSummary.Size = new Size(576, 30);
        this.lblSummary.Text = "";
        this.lblSummary.TextAlign = ContentAlignment.MiddleLeft;
        this.lblSummary.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        // MainForm
        this.ClientSize = new Size(600, 350);
        this.ControlBox = true;
        this.FormBorderStyle = FormBorderStyle.FixedSingle;
        this.MaximizeBox = true;
        this.MinimizeBox = true;
        this.StartPosition = FormStartPosition.CenterScreen;
        this.Text = "文件名导出工具 v1.0";

        this.Controls.AddRange(new Control[] {
            this.labelFolder,
            this.txtFolderPath,
            this.btnBrowseFolder,
            this.chkIncludeHidden,
            this.labelExport,
            this.txtExportPath,
            this.btnBrowseExport,
            this.btnOpenOutput,
            this.txtExportFileName,
            this.btnStart,
            this.lblExportHint,
            this.labelProgress,
            this.progressBar,
            this.lblPercent,
            this.labelStatus,
            this.lblStatus,
            this.labelCount,
            this.lblFileCount,
            this.lblSummary,
        });

        this.ResumeLayout(false);
        this.PerformLayout();
    }

    #endregion

    private Label labelFolder;
    private TextBox txtFolderPath;
    private Button btnBrowseFolder;
    private CheckBox chkIncludeHidden;
    private Label labelExport;
    private TextBox txtExportPath;
    private Button btnBrowseExport;
    private Button btnOpenOutput;
    private TextBox txtExportFileName;
    private Button btnStart;
    private Label lblExportHint;
    private Label labelProgress;
    private ProgressBar progressBar;
    private Label lblPercent;
    private Label labelStatus;
    private Label lblStatus;
    private Label labelCount;
    private Label lblFileCount;
    private Label lblSummary;
}
