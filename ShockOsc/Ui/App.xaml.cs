namespace ShockOsc;

public partial class App : Application
{
    public App()
    {
        _ = OpenShock.ShockOsc.ShockOsc.StartMain();

        InitializeComponent();

        MainPage = new MainPage();
    }

    protected override Window CreateWindow(IActivationState activationState)
    {
        var window = base.CreateWindow(activationState);
        if (window != null)
        {
            window.Title = "ShockOSC";
        }

        return window;
    }
}