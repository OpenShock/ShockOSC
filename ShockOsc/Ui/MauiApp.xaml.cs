#if WINDOWS 
namespace OpenShock.ShockOsc.Ui;

public partial class MauiApp
{
    public MauiApp()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new MainPage())
        {
            MinimumHeight = 600,
            MinimumWidth = 1000,
            TitleBar = new TitleBar
            {
                Icon = ImageSource.FromFile("Resources/Icon512.png"),
                Title = "ShockOSC",
                BackgroundColor = Color.FromArgb("212121")
            }
        };
        
        return window;
    }
}
#endif