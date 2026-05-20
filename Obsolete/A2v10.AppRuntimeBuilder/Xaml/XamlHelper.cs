// Copyright © 2024 Oleksandr Kukhtin. All rights reserved.

using System;

using A2v10.Xaml;

namespace A2v10.AppRuntimeBuilder;

public static class XamlHelper
{
    public static Button CreateButton(CommandType commandType, String? content = null, Icon icon = Icon.NoIcon, Action<Button>? onCreate = null)
    {
        var btn = new Button()
        {
            Icon = icon,
            Content = content,
            Bindings = btn => {
                btn.SetBinding(nameof(Button.Command), new BindCmd() { Command = commandType });
            }
        };
        onCreate?.Invoke(btn);
        return btn;
    }
    public static Button CreateButton(CommandType commandType, Icon icon, String? Tip = null)
    {
        return new Button()
        {
            Icon = icon,
            Tip = Tip,
            Bindings = btn => {
                btn.SetBinding(nameof(Button.Command), new BindCmd() { Command = commandType });
            }
        };
    }

    public static TextBox CreateSearchBox()
    {
        return new TextBox()
        {
            Placeholder = "@[Search]",
            TabIndex = 1,
            Width = Length.FromString("20rem"),
            ShowSearch = true,
            ShowClear = true,
            Bindings = tb =>
            {
                tb.SetBinding(nameof(TextBox.Value), new Bind("Parent.Filter.Fragment"));
            }
        };
    }
}
