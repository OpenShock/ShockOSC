using Microsoft.AspNetCore.Components;
using Color = MudBlazor.Color;

namespace OpenShock.ShockOsc.Ui.Components.Parts;

public partial class StatePart : ComponentBase
{
    [Parameter]
    public required Color IconColor { get; set; }
    
    [Parameter]
    public required string Icon { get; set; }
    
    [Parameter]
    public required string Tooltip { get; set; }
    
    [Parameter]
    public required string Text { get; set; }
}