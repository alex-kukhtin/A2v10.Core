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
            yield return FormBuild.Header($"@{_table.RealItemName}", new FormItemGrid(1, 1));
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

            foreach (var c in _table.EditableColumns(_appMeta).Where(c => !skipColumns.Contains(c.Name)))
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
                },
                Items = [..BodyItems(cols: 2)]
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
                        Data = d.Name,
                        Items = [
                            new FormItem(FormItemIs.Toolbar) {
                                Grid = new FormItemGrid(2, 1),
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
                                Grid = new FormItemGrid(3, 1),
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
                yield return new FormItem(FormItemIs.Grid)
                {
                    Props = new FormItemProps() { 
                        Columns = "auto",
                        Rows = "auto auto 1fr",
                    },
                    Height = "100%",
                    Items = [
                        new FormItem(FormItemIs.Tabs)
                        {
                            Grid = new FormItemGrid(1, 1),
                            Data = $"{_table.RealItemName}.$$Tab",
                            Items = [..details]
                        }
                    ]
                };
                rowNo += 1; // toolbar & table
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
                        Rows = hasDetails ? "auto auto 1fr auto" : "auto auto 1fr auto",
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
