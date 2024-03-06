namespace ShockOsc;

public partial class App : Application
{
    public App()
    {
        _ = OpenShock.ShockOsc.ShockOsc.StartMain([ "--debug" ]);

        InitializeComponent();

        MainPage = new MainPage();
    }
}