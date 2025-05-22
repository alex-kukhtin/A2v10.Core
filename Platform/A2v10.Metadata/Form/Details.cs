// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

namespace A2v10.Metadata;

internal partial class BaseModelBuilder
{
    /*
     * Tab
     *   Grid
     *     Toolbar
     *     Table
     */
    IEnumerable<FormItem> DetailsTableCells(TableMetadata d)
    {
        return d.EditableColumns().OrderBy(c => c.Order).Select(c =>
            new FormItem(FormItemIs.TableCell)
            {
                Label = c.Label ?? $"@{c.Name}",
                Width = c.ToColumnWidth(),
                DataType = c.ToItemDataType(),
                Items = [
                    new FormItem(c.Column2Is())
                            {
                                DataType = c.ToItemDataType(),
                                Data = c.Name,
                                Props= c.IsReference ?
                                    new FormItemProps() {
                                        Url = c.Reference.EndpointPath(),
                                    }
                                    : null
                            }
                ]
            }
        );
    }

    FormItem DetailsTab(TableMetadata d, String dataBind, String tabBind, String? tabName, String cssPrefix)
    {

        return new FormItem(FormItemIs.Tab)
        {
            Label = tabName ?? $"@{tabBind}",
            Grid = new FormItemGrid(4, 1),
            Data = tabBind,
            Items = [
            new FormItem(FormItemIs.StackPanel)
            {
                Height = "100%",
                CssClass = "details",
                Items = [
                    new FormItem(FormItemIs.Toolbar) {
                        CssClass = $"{cssPrefix}-details-toolbar",
                        Items = [
                            new FormItem(FormItemIs.Button)
                            {
                                Label = "@AddRow",
                                Command = new FormItemCommand()
                                {
                                    Command = FormCommand.Append,
                                    Argument = dataBind,
                                },
                            }
                        ]
                    },
                    new FormItem(FormItemIs.Table) {
                        Data = dataBind,
                        Height = "100%",
                        CssClass = $"{cssPrefix}-details",
                        Items = [
                            ..DetailsTableCells(d),
                            new FormItem(FormItemIs.TableCell)
                            {
                                Width = "1px",
                                Items = [
                                    FormBuild.Button(new FormItemCommand() {
                                        Command = FormCommand.Remove
                                    }, "✕"),
                                ]
                            }
                        ]
                    }]
                }
            ]
        };
    }

    FormItem DetailsTabGrid(TableMetadata d, String dataBind, String tabBind, String? tabName, String cssPrefix)
    {
        return new FormItem(FormItemIs.Tab)
        {
            Label = tabName ?? $"@{tabBind}",
            Grid = new FormItemGrid(4, 1),
            Data = tabBind,
            Items = [
            new FormItem(FormItemIs.Grid)
            {
                Height = "100%",
                CssClass = "details",
                Props = new FormItemProps() {
                    Rows = "auto 1fr",
                    Columns = "1fr"
                },
                Items = [
                    new FormItem(FormItemIs.Toolbar) {
                        Grid = new FormItemGrid(1, 1),
                        CssClass = $"{cssPrefix}-details-toolbar",
                        Items = [
                            new FormItem(FormItemIs.Button)
                            {
                                Label = "@AddRow",
                                Command = new FormItemCommand()
                                {
                                    Command = FormCommand.Append,
                                    Argument = dataBind,
                                },
                            }
                        ]
                    },
                    new FormItem(FormItemIs.Table) {
                        Data = dataBind,
                        Height = "100%",
                        CssClass = $"{cssPrefix}-details",
                        Grid = new FormItemGrid(2, 1),
                        Items = [
                            ..DetailsTableCells(d),
                            new FormItem(FormItemIs.TableCell)
                            {
                                Width = "1px",
                                Items = [
                                    FormBuild.Button(new FormItemCommand() {
                                        Command = FormCommand.Remove
                                    }, "✕"),
                                ]
                            }
                        ]
                    }]
                }
            ]
        };
    }
}
