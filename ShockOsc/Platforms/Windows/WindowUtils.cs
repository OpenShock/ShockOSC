#if WINDOWS
using Microsoft.UI;
using Microsoft.UI.Windowing;

// ReSharper disable once CheckNamespace
namespace OpenShock.ShockOsc.Platforms.Windows;

public static class WindowUtils
{
    public static void ShowOnTop(this AppWindow appWindow)
    {
        appWindow.Show();

        if (appWindow.Presenter is not OverlappedPresenter presenter) return;
        presenter.IsAlwaysOnTop = true;
        presenter.IsAlwaysOnTop = false;
        presenter.Restore();
    }
    
    public static AppWindow GetAppWindow(object window)
    {
        var handle = WinRT.Interop.WindowNative.GetWindowHandle(window);
        var id = Win32Interop.GetWindowIdFromWindow(handle);
        return AppWindow.GetFromWindowId(id);
    }
}
#endif