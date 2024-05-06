#if WINDOWS
namespace OpenShock.ShockOsc.Ui;

public partial class MauiApp
{
    public MauiApp()
    {
        InitializeComponent();
        MainPage = new MainPage();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = base.CreateWindow(activationState);
        window.Title = "ShockOSC";
        window.MinimumHeight = 600;
        window.MinimumWidth = 1000;
        
        return window;
    }
}
#endif