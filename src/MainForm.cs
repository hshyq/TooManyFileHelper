using System.Reflection;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace 文件整理助手;

/// <summary>
/// 主窗体：承载 WebView2 与 HTML UI，负责 JS ↔ C# 消息桥接。
/// 所有业务逻辑委托给 BridgeService，本类只负责窗口与通信。
/// </summary>
public partial class MainForm : Form
{
    private BridgeService? _bridge;

    public MainForm()
    {
        InitializeComponent();
        _bridge = new BridgeService(SendToJs);

        // 窗口标题栏与任务栏图标（取自可执行文件自身的图标资源）
        try
        {
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch
        {
            // 图标提取失败时使用系统默认图标，不影响运行
        }
        Load += async (_, _) => await InitializeWebViewAsync();


    }

    /// <summary>
    /// 初始化 WebView2 并加载嵌入式 HTML 页面
    /// </summary>
    private async Task InitializeWebViewAsync()
    {
        try
        {
            await webView.EnsureCoreWebView2Async();
            webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

            string html = LoadEmbeddedHtml();
            webView.CoreWebView2.NavigateToString(html);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "界面初始化失败：需要 Microsoft Edge WebView2 运行时。\n" +
                "Windows 11 自带；Windows 10 请安装 WebView2 Runtime 后重试。\n\n" +
                ex.Message,
                "错误",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            Close();
        }
    }

    /// <summary>
    /// 接收前端发来的消息，转交桥接服务处理
    /// </summary>
    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        string? json = e.TryGetWebMessageAsString();
        if (string.IsNullOrEmpty(json))
        {
            return;
        }
        _ = _bridge!.HandleAsync(json);
    }

    /// <summary>
    /// 向 JS 发送消息（自动切回 UI 线程）
    /// </summary>
    private void SendToJs(string json)
    {
        if (webView.CoreWebView2 == null)
        {
            return;
        }
        if (InvokeRequired)
        {
            BeginInvoke(() => SendToJs(json));
            return;
        }
        webView.CoreWebView2.PostWebMessageAsJson(json);
    }

    /// <summary>
    /// 读取程序集中的嵌入式 HTML 资源
    /// </summary>
    private static string LoadEmbeddedHtml()
    {
        const string resourceName = "文件整理助手.ui.index.html";
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("未找到嵌入式 UI 资源：" + resourceName);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
