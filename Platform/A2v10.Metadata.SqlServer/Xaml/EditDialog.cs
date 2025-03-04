﻿// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using A2v10.Infrastructure;
using A2v10.Xaml;
using System;

namespace A2v10.Metadata.SqlServer;

internal partial class ModelPageBuilder
{
    UIElement CreateEditDialog(IPlatformUrl platformUrl, IModelView modelView, TableMetadata meta)
    {
        var viewMeta = modelView.Meta ??
            throw new InvalidOperationException("Meta is null");
        var elemName = viewMeta.Table.Singular();
        UIElementCollection Controls()
        {
            var coll = new UIElementCollection();
            foreach (var column in meta.RealColumns(viewMeta))
            {
                if (column.IsReference)
                {
                    coll.Add(new SelectorSimple()
                    {
                        Label = $"@[{column.Name}]",
                        Url = $"/catalog/{column.Reference!.RefTable.Singular().ToLowerInvariant()}",
                        Bindings = b => b.SetBinding(nameof(SelectorSimple.Value), new Bind($"{elemName}.{column.Name}"))
                    });
                }
                else if (column.ColumnDataType == ColumnDataType.Boolean)
                {
                    coll.Add(new CheckBox()
                    {
                        Label = $"@[{column.Name}]",
                        Bindings = b => b.SetBinding(nameof(CheckBox.Value), new Bind($"{elemName}.{column.Name}"))
                    });
                }
                else
                {
                    coll.Add(new TextBox()
                    {
                        Label = $"@[{column.Name}]",
                        Multiline = column.MaxLength >= 255,
                        Bindings = b => b.SetBinding(nameof(TextBox.Value), new Bind($"{elemName}.{column.Name}"))
                    });
                }
            }
            return coll;
        }

        return new Dialog()
        {
            Title = $"@[{meta.Table.Singular()}]",
            Buttons = [
                new Button() {
                    Content = "@[SaveAndClose]",
                    Style = ButtonStyle.Primary,
                    Bindings = b => b.SetBinding(nameof(Button.Command), new BindCmd("SaveAndClose"))
                },
                new Button() {
                    Content = "@[Cancel]",
                    Bindings = b => b.SetBinding(nameof(Button.Command), new BindCmd("Close"))
                }
            ],
            Children = [
                new Grid(_xamlSericeProvider) {
                    Children = Controls()
                }
            ]
        };
    }
}
