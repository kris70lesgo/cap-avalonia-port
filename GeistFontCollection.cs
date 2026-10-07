using System;
using Avalonia.Media.Fonts;

namespace CapAvalonia;

internal sealed class GeistFontCollection : EmbeddedFontCollection
{
    public GeistFontCollection()
        : base(
            new Uri("fonts:Cap", UriKind.Absolute),
            new Uri("avares://CapAvalonia/assets/Fonts", UriKind.Absolute))
    {
    }
}
