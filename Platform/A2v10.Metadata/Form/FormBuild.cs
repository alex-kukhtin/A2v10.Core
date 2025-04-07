// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.Metadata;

internal static class FormBuild
{
    public static FormItem Header(String content, FormItemGrid? grid) =>
        new FormItem(FormItemIs.Header)
        {
            Label = content,
            Grid = grid
        };

    public static FormItem Label(String content, FormItemGrid? grid = null) =>
        new FormItem(FormItemIs.Label)
        {
            Label = content,
            Grid = grid
        };

    public static FormItem Button(FormCommand command, String label = "") =>
        new FormItem(FormItemIs.Button)
        {
            Label = label,
            Command = new FormItemCommand(command)
        };

    public static FormItem Button(FormItemCommand command, String label = "") =>
        new FormItem(FormItemIs.Button)
        {
            Label = label,
            Command = command
        };
}
