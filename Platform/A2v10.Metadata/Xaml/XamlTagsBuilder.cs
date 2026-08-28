// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;

using A2v10.System.Xaml;
using A2v10.Xaml;

namespace A2v10.Metadata;

internal class XamlTagsBuilder
{
    private readonly IServiceProvider _xamlServiceProvider = new XamlServiceProvider();

    public Dialog RenderSettingsDialog()
    {
        static Table CreateTagTable() =>
            new Table()
            {
                Height = Length.FromString("20rem"),
                Width = Length.FromString("100%"),
                CellSpacing = CellSpacingMode.Medium,
                StickyHeaders = true,
                Columns = TableColumnCollection.FromString("15rem,4rem,Auto,2px"),
                Bindings = b => b.SetBinding(nameof(Table.ItemsSource), new Bind(Constants.FieldNames.Tags)),
                Rows = [
                    new TableRow()
                    {
                        Cells = [
                            new TextBox() {
                                Bindings = b => b.SetBinding(nameof(TextBox.Value), new Bind("Name"))
                            },
                            new ColorPicker() {
                                Compact = true,
                                Bindings = b => b.SetBinding(nameof(ColorPicker.Value), new Bind("Color"))
                            },
                            new TextBox() {
                                Bindings = b => b.SetBinding(nameof(TextBox.Value), new Bind("Memo"))
                            },
                            new TableCell() 
                            {
                                Content = new Hyperlink()
                                {
                                    Content = "✕",
                                    Bindings = b => {
                                        var cmd = new BindCmd(CommandType.Remove);
                                        cmd.BindImpl.SetBinding(nameof(BindCmd.Argument), new Bind());
                                        b.SetBinding(nameof(Hyperlink.Command), cmd);
                                        b.SetBinding(nameof(Hyperlink.If), new Bind("!Used"));
                                    }
                                }
                            }
                        ]
                    }
                ],
                Header = [
                    new TableRow() 
                    {
                        Cells = [
                            new TableCell() { Content = "@[Tag]" },
                            new TableCell() { Content = "@[Color]" },
                            new TableCell() { Content = "@[Memo]" },
                            new TableCell()
                        ]
                    }
                ]
            };

        return new Dialog()
        {
            Title = "@[Tags]",
            Width = Length.FromString("40rem"),
            Buttons = [
                new Button() {
                    Content = "@[SaveAndClose]",
                    Style = ButtonStyle.Primary,
                    Bindings = b => b.SetBinding(nameof(Button.Command), new BindCmd(CommandType.SaveAndClose) { ValidRequired = true})
                },
                new Button() {
                    Content = "@[Cancel]",
                    Bindings = b => b.SetBinding(nameof(Button.Command), new BindCmd(CommandType.Close))
                }
            ],
            Children = [
                new Grid(_xamlServiceProvider) {
                    Rows = RowDefinitions.FromString("Auto,Auto"),
                    Padding = Thickness.FromString("1rem,.25rem,1rem,1rem"),
                    Children = [
                        new Toolbar(_xamlServiceProvider) 
                        {
                            Children = [
                                new Button() {
                                    Content = "@[Add]",
                                    Icon = Icon.Plus,
                                    Bindings = b => {
                                        var cmd = new BindCmd(CommandType.Append);
                                        cmd.BindImpl.SetBinding(nameof(BindCmd.Argument), new Bind("Tags"));
                                        b.SetBinding(nameof(Button.Command), cmd);
                                    }
                                }
                            ]
                        },
                        CreateTagTable()
                    ]
                }
            ]
        };
    }
}
