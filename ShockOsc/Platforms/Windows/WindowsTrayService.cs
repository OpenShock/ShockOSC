#if WINDOWS

using System.Drawing;
using System.Windows.Forms;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using OpenShock.SDK.CSharp.Hub;
using OpenShock.ShockOsc.Services;
using Application = Microsoft.Maui.Controls.Application;
using Color = System.Drawing.Color;
using Image = System.Drawing.Image;

namespace OpenShock.ShockOsc;

public class WindowsTrayService : ITrayService
{
    private readonly OpenShockHubClient _apiHubClient;

    public WindowsTrayService(OpenShockHubClient apiHubClient)
    {
        _apiHubClient = apiHubClient;
        
        _apiHubClient.Reconnecting += _ => HubStateChanged();
        _apiHubClient.Reconnected += _ => HubStateChanged();
        _apiHubClient.Closed += _ => HubStateChanged();
        _apiHubClient.Connected += _ => HubStateChanged();
    }
    
    private ToolStripLabel _stateLabel;
    
    private Task HubStateChanged()
    {
        _stateLabel.Text = $"State: {_apiHubClient.State}";
        return Task.CompletedTask;
    }

    public void Initialize()
    {
        var tray = new NotifyIcon();
        tray.Icon = Icon.ExtractAssociatedIcon(@"Resources\openshock-icon.ico");
        tray.Text = "ShockOSC";

        var menu = new ContextMenuStrip();

        menu.Items.Add("ShockOSC", Image.FromFile(@"Resources\openshock-icon.ico"), OnMainClick);
        menu.Items.Add(new ToolStripSeparator());
        _stateLabel = new ToolStripLabel($"State: {_apiHubClient.State}");
        menu.Items.Add(_stateLabel);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Restart", null, Restart);
        menu.Items.Add("Quit ShockOSC", null, OnQuitClick);
        
        tray.ContextMenuStrip = menu;

        tray.Click += OnMainClick;
        menu.Opened += async (sender, args) =>
        {
            var aa = menu;
        };

        tray.Visible = true;
    }

    private void Restart(object? sender, EventArgs e)
    {
        Application.Current?.Quit();
    }

    private static void OnMainClick(object? sender, EventArgs eventArgs)
    {
        if (eventArgs is MouseEventArgs mouseEventArgs && mouseEventArgs.Button != MouseButtons.Left) return;

        var window = Application.Current?.Windows[0];
        var nativeWindow = window?.Handler?.PlatformView;
        if (nativeWindow == null) return;

        var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
        var windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        
        appWindow.Show();

        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = true;
            presenter.IsAlwaysOnTop = false;    
        }
    }

    private static void OnQuitClick(object? sender, EventArgs eventArgs)
    {
        if (Application.Current != null)
        {
            Application.Current.Quit();
            return;
        }
        
        Environment.Exit(0);
    }
}

#endif