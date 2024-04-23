using MudBlazor;

namespace OpenShock.ShockOsc.Ui.Utils;

public static class ThemeDefinition
{
    public static readonly MudTheme ShockOscTheme = new()
    {
        Palette = new PaletteDark
        {
            Primary = "#e14a6d",
            PrimaryDarken = "#b31e40",
            Secondary = MudBlazor.Colors.Green.Accent4,
            AppbarBackground = MudBlazor.Colors.Red.Default,
            Background = "#2f2f2f",
            Surface = "#1f1f1f",
        },
        LayoutProperties = new LayoutProperties
        {
            DrawerWidthLeft = "260px",
            DrawerWidthRight = "300px"
        },
        Typography = new Typography
        {
            Default = new Default
            {
                FontFamily = new string[] { "'Poppins', Roboto, Helvetica, Arial, sans-serif" }
            },
        }
    };
}