﻿// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.Collections.Generic;

namespace A2v10.Metadata;

internal partial class BaseModelBuilder
{
    private Form CreateDocumentPage()
    {
        var hasDetails = _table.Details.Any();

        IEnumerable<FormItem> toolbarButtons = [
            FormBuild.Button(FormCommand.SaveAndClose, "@SaveAndClose"),
            FormBuild.Button(FormCommand.Save),
            FormBuild.Button(FormCommand.Print),
            new FormItem(FormItemIs.Separator),
            new FormItem(FormItemIs.Button) {
                Label = "@Apply",
                Command = new FormItemCommand(FormCommand.Apply),
                If = $"!{_table.RealItemName}.{_table.DoneField}"
            },
            new FormItem(FormItemIs.Button) {
                Label = "@UnApply",
                Command = new FormItemCommand(FormCommand.UnApply),
                If = $"{_table.RealItemName}.{_table.DoneField}"
            },
            new FormItem(FormItemIs.Button) {
                Label = "@ShowTrans",
                Command = new FormItemCommand(FormCommand.Dialog, _table.RealItemName, $"{_table.EndpointPathUseBase(_baseTable)}/showtrans"),
                If = $"{_table.RealItemName}.{_table.DoneField}"
            },
            new FormItem(FormItemIs.Separator),
            FormBuild.Button(FormCommand.Reload),
            new FormItem(FormItemIs.Aligner)
        ];

        FormItem? CreateTaskPad()
        {
            return null;
        }

        IEnumerable<FormItem> HeaderItems()
        {
            var title = _baseTable?.RealItemLabel ?? _table.RealItemLabel;

            // Title, @Number [Number] @Date, [DatePicker]
            yield return FormBuild.Header(title, new FormItemGrid(1, 1));

            yield return FormBuild.Label("@Date", new FormItemGrid(1, 2));
            yield return new FormItem(FormItemIs.DatePicker)
            {
                Data = $"{_table.RealItemName}.Date",
                Grid = new FormItemGrid(1, 3),
            };
        }

        IEnumerable<FormItem> BodyItems(Int32 cols)
        {
            var row = 1;
            var col = 1;
            var skipColumns = new HashSet<String>() { "Date", "Done", "Memo" };

            foreach (var c in _table.EditableColumns().Where(c => !skipColumns.Contains(c.Name)))
            {
                yield return CreateControl(c, row, col++);
                if (col > cols)
                {
                    col = 1;
                    row += 1;
                }

            }
        }

        IEnumerable<FormItem> Body()
        {
            // header
            yield return new FormItem(FormItemIs.Grid)
            {
                Grid = new FormItemGrid(1, 1),
                CssClass = "document-header",
                Props = new FormItemProps() { 
                    Rows = "auto",
                    Columns = "auto auto 12rem auto 12rem 1fr",
                },
                Items = [..HeaderItems()]
            };

            // body
            yield return new FormItem(FormItemIs.Grid)
            {
                Grid = new FormItemGrid(2, 1),
                Props = new FormItemProps()
                {
                    Rows = "auto auto auto",
                    Columns = "22rem 22rem 22rem 22rem 1fr"
                },
                Items = [..BodyItems(cols: 4)]
            };

            IEnumerable<FormItem> DetailsTableCells(TableMetadata d) 
            {
                return d.EditableColumns().OrderBy(c => c.Order).Select(c =>
                    new FormItem(FormItemIs.TableCell)
                    {
                        Label =  c.Label ?? $"@{c.Name}",
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

            FormItem DetailsTab(TableMetadata d, String dataBind, String tabBind, String? tabName)
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
                        MinHeight = "25rem",
                        Props = new FormItemProps()
                        {
                            Rows = "auto 1fr",
                            Columns = "auto",
                        },
                        Items = [
                            new FormItem(FormItemIs.Toolbar) {
                                Grid = new FormItemGrid(1, 1),
                                CssClass = "document-details-toolbar",
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
                                CssClass = "document-details",
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

            IEnumerable<FormItem> DetailsTabs()
            {
                foreach (var d in _table.Details)
                {
                    if (d.Kinds.Count == 0)
                        yield return DetailsTab(d, $"{_table.RealItemName}.{d.RealItemsName}", d.Name, null);
                    else
                        foreach (var k in d.Kinds)
                        {
                            var dataBind = $"{_table.RealItemName}.{k.Name}";
                            yield return DetailsTab(d, dataBind, k.Name, k.Label);
                        }
                }
            }

            // details
            if (hasDetails)
            {
                yield return new FormItem(FormItemIs.Tabs)
                {
                    Data = $"{_table.RealItemName}.$$Tab",
                    Grid = new FormItemGrid(3, 1),
                    CssClass = "document-tabbar",
                    Items = [.. DetailsTabs()]
                };
            }
            // footer
            yield return new FormItem(FormItemIs.Grid)
            {
                Grid = new FormItemGrid(hasDetails ? 5 : 3, 1),
                CssClass = "document-footer",
                Props = new FormItemProps()
                {
                    Columns = "auto 1fr",
                },
                Items = 
                [
                    FormBuild.Label("@Memo"),
                    new FormItem(FormItemIs.TextBox) 
                    {
                        Data = $"{_table.RealItemName}.Memo"
                    }
                ]
            };
        }

        return new Form()
        {
            Is = FormItemIs.Page,
            CssClass = "document-page",
            Schema = _table.Schema,
            Table = _table.Name,
            Data = _table.RealItemName,
            Label =  _baseTable?.RealItemLabel ?? _table.RealItemLabel,
            EditWith = _table.EditWith,
            Items = [
                new FormItem() {
                    Is = FormItemIs.Grid,
                    CssClass = "document-grid",
                    Props = new FormItemProps() {
                        Rows = hasDetails ? "auto auto auto 1fr auto" : "auto auto 1fr auto",
                        Columns = "1fr",
                    },
                    Height = "100%",
                    Items = [..Body() ]
                }
            ],
            Taskpad = CreateTaskPad(),
            Toolbar = new FormItem(FormItemIs.Toolbar)
            {
                Items = [..toolbarButtons],
            }
        };
    }
}
