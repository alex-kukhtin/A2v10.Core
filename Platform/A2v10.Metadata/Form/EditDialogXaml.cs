// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.Collections.Generic;
using A2v10.Xaml;

namespace A2v10.Metadata;

internal partial class PlainModelBuilder
{
    internal Dialog CreateEditDialogXaml()
    {
        return new Dialog()
        {
            Overflow = true,
            Buttons = [
                new Button()
                {
                    Content = "@[SaveAndClose]",
                    Style = ButtonStyle.Primary,
                    Bindings = b => b.SetBinding(nameof(Button.Command), new BindCmd(nameof(CommandType.SaveAndClose)))
                },
                new Button()
                {
                    Content = "@[Cancel]",
                    Bindings = b => b.SetBinding(nameof(Button.Command), new BindCmd(nameof(CommandType.Close)))
                }
            ],
            Children = [
                new Grid(_xamlServiceProvider)
                {
                    Children = [
                        new TextBox() {
                            Label = "@[Name]",
                            TabIndex = 1,
                            Bold = true,
                            Bindings = b => b.SetBinding(nameof(TextBox.Value), new Bind($"{_table.Model}.Name"))
                        },
                        new DatePicker() {
                            Label = "@[Date]",
                            Width = Length.FromString("12rem"),
                            Bindings = b => b.SetBinding(nameof(TextBox.Value), new Bind($"{_table.Model}.Date"))
                        },
                        new TextBox() {
                            Label = "@[Memo]",
                            Multiline = true,
                            Rows = 3,
                            Bindings = b => b.SetBinding(nameof(TextBox.Value), new Bind($"{_table.Model}.Memo"))
                        }
                    ]
                }
            ]
        };
    }
}
