// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

namespace A2v10.Metadata;

internal partial class PlainModelBuilder
{
    /*
     * Tab
     *   Grid
     *     Toolbar
     *     Table
     */
    static IEnumerable<FormItem> DetailsTableCells(TableMetadata d, String dataBind)
    {
        static FormItemProps? GetItemProps(TableColumn c)
        {
            if (c.IsReference)
                return new FormItemProps()
                {
                    Url = c.Reference.EndpointPath(),
                };
            else if (c.IsEnum)
                return new FormItemProps()
                {
                    ItemsSource = $"Root.{c.Reference.RefTable}"
                };
            return null;
        }

        return d.EditableColumns()
            .Where(c => !c.IsParent)
            .OrderBy(c => c.IsRowNo ? -1 :  c.Order)
            .Select(c =>
            new FormItem(FormItemIs.TableCell)
            {
                Label = c.Label ?? $"@{c.Name}",
                Width = c.ToColumnWidth(),
                DataType = c.ToItemDataType(),
                Data = c.Total ? $"{dataBind}.{c.Name}" : String.Empty,
                Items = [
                    new FormItem(c.Column2Is())
                    {
                        DataType = c.ToItemDataType(),
                        Data = c.Name,
                        Props= GetItemProps(c)
                    }
                ],
                Props = c.Total ? new FormItemProps()
                {
                    Total = true
                }: null
            }
        );
    }

    static FormItem DetailsTab(TableMetadata d, String dataBind, String tabBind, String? tabName, String cssPrefix)
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
                            ..DetailsTableCells(d, dataBind),
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

    static FormItem DetailsTabGrid(TableMetadata d, String dataBind, String tabBind, String? tabName, String cssPrefix)
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
                            ..DetailsTableCells(d, dataBind),
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
