using MudBlazor;

namespace RepoHub;

public class DialogAction
{
    public string Text { get; set; }
    public object Value { get; set; }
    public Color Color { get; set; }
    public Variant Variant { get; set; } = Variant.Filled;
    public string Icon { get; set; }
} 
