// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using A2v10.Xaml;

namespace A2v10.Metadata;

/* Buttons with no entity behind them: the same control whatever screen asks for it, so there is
 * nothing to derive and nothing to parameterise. Kept apart from CommandBarControl because a form
 * may want one directly - the print page has a Reload and no command bar at all.
 */
internal static class FormButtons
{
    internal static Button Reload => new()
    {
        Icon = Icon.Reload,
        Bindings = b => b.SetBinding(nameof(Button.Command), new BindCmd(CommandType.Reload))
    };

    internal static Button Save => new()
    {
        Icon = Icon.SaveOutline,
        Bindings = b => b.SetBinding(nameof(Button.Command), new BindCmd(CommandType.Save))
    };

    internal static Button SaveAndClose => new()
    {
        Icon = Icon.SaveCloseOutline,
        Content = "@[SaveAndClose]",
        Bindings = b => b.SetBinding(nameof(Button.Command), new BindCmd(CommandType.SaveAndClose))
    };
}
