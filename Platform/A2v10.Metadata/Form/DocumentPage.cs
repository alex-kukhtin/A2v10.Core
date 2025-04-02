// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

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
            FormBuild.Button(FormCommand.Apply, "@Apply"),
            FormBuild.Button(FormCommand.UnApply, "@UnApply"),
            FormBuild.Button(FormCommand.Reload),
            new FormItem(FormItemIs.Aligner)
        ];

        FormItem? CreateTaskPad()
        {
            return null;
        }

        IEnumerable<FormItem> HeaderItems()
        {
            // Title, @Number [Number] @Date, [DatePicker]
            yield return FormBuild.Header($"@{_table.RealItemName}");
            yield return FormBuild.Label("@Date");
            yield return new FormItem(FormItemIs.DatePicker)
            {
                Data = $"{_table.RealItemName}.Date"
            };
        }

        IEnumerable<FormItem> Body()
        {
            Int32 rowNo = 1;
            // header
            yield return new FormItem(FormItemIs.Grid)
            {
                Grid = new FormItemGrid(rowNo++, 1),
                Props = new FormItemProps() { 
                    Rows = "auto",
                    Columns = "auto auto 12rem auto 12rem 1fr",
                },
                Items = [..HeaderItems()]
            };
            // body
            yield return new FormItem(FormItemIs.Grid)
            {
                Grid = new FormItemGrid(rowNo++, 1),
                Props = new FormItemProps()
                {
                    Rows = "auto auto auto",
                    Columns = "22rem 22rem 1fr"
                }
            };

            // details
            if (hasDetails)
            {
                var details = _table.Details.Select(d => 
                {
                    var cells = d.EditableColumns(_appMeta).Select(c =>
                        new FormItem(FormItemIs.TableCell)
                        {
                            Label = $"@{c.Name}",
                            Items = [
                                new FormItem(c.Column2Is()) 
                                {
                                    Data = c.Name,
                                    DataType = c.ToItemDataType(),  
                                    Props= c.IsReference ? 
                                        new FormItemProps() {
                                            Url = c.Reference.EndpointPath(),
                                        } 
                                        : null
                                },
                            ]
                        }
                    );

                    var dataBind = $"{_table.RealItemName}.{d.RealItemsName}";

                    return new FormItem(FormItemIs.Tab)
                    {
                        Label = $"@{d.Name}",
                        Data = "Root.$$Tab",
                        Items = [
                            new FormItem(FormItemIs.Toolbar) {
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
                                Items = [
                                    ..cells, 
                                    new FormItem(FormItemIs.TableCell) 
                                    {
                                        Items = [
                                            FormBuild.Button(new FormItemCommand() {
                                                Command = FormCommand.Remove
                                            }, "✕"),
                                        ]
                                    }
                                ]
                            },
                        ]
                    };
                });
                yield return new FormItem(FormItemIs.Tabs)
                {
                    Grid = new FormItemGrid(rowNo++, 1),
                    Items = [..details],
                };
                rowNo += 2; // toolbar & table
            }
            // footer
            yield return new FormItem(FormItemIs.Grid)
            {
                Grid = new FormItemGrid(rowNo++, 1),
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
            UseCollectionView = true,
            Schema = _table.Schema,
            Table = _table.Name,
            Data = _table.RealItemName,
            Label = $"@{_table.RealItemName}",
            Items = [
                new FormItem() {
                    Is = FormItemIs.Grid,
                    Props = new FormItemProps() {
                        Rows = hasDetails ? "auto auto auto auto 1fr auto" : "auto auto 1fr auto",
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
