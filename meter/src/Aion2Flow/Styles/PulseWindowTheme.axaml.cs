using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace Cloris.Aion2Flow.Styles;

public sealed partial class PulseWindowTheme : Avalonia.Styling.Styles
{
    public PulseWindowTheme() => AvaloniaXamlLoader.Load(this);
}
