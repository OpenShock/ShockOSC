namespace OpenShock.ShockOsc.Ui;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        MainPage = new MainPage();
    }

    protected override Window CreateWindow(IActivationState activationState)
    {
        var window = base.CreateWindow(activationState);
        if (window != null)
        {
            window.Title = "ShockOSC";
            window.MinimumHeight = 600;
            window.MinimumWidth = 1000;
        }

        return window;
    }
}