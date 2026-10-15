// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.Metadata;

/* The colours a declaration may name: each is a class of the platform's palette, `.color-<name>`
 * (Palette.less), held to the stylesheet by PaletteTests - so a colour the load accepts is a colour
 * that renders. Lower case, as stored: the value lands in a class attribute.
 * Its own list, not the XAML enum TagLabelStyle it was read from until 2026-10-15: a declaration is
 * read by clients that are not XAML, and the enum is the TagLabel's, drawn by another selector.
 */
internal static class Palette
{
    internal const String White = "white";

    internal static readonly String[] Colors = [
        "default", "success", "green", "warning", "orange", "info", "cyan", "danger", "red", "error",
        "purple", "pink", "gold", "blue", "salmon", "seagreen", "tan", "magenta", "lightgray", "olive",
        "teal", White
    ];
}
