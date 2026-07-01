// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;

using A2v10.Xaml;

namespace A2v10.Metadata;

internal partial class XamlBuilder
{
    Control CreateEditControl(FormColumn column)
    {
        var valueBind = new Bind($"{Table.Model}.{column.TableColumn.Name}");
        return column.TableColumn.Type switch
        {
            ColumnType.Date => new DatePicker()
            {
                Label = column.Header,
                Width = Length.FromString("12rem"),
                Bindings = b => b.SetBinding(nameof(DatePicker.Value), valueBind)
            },
            ColumnType.Name => new TextBox()
            {
                Label = column.Header,
                Bold = true,
                TabIndex = 1,
                Bindings = b => b.SetBinding(nameof(TextBox.Value), valueBind)
            },
            ColumnType.Memo => new TextBox()
            {
                Label = column.Header,
                Multiline = true,
                Rows = 3,
                Bindings = b => b.SetBinding(nameof(TextBox.Value), valueBind)
            },
            ColumnType.Ref => new SelectorSimple()
            {
                Label = column.Header,
                Url = column.TableColumn.RefTableCheck.Path,
                Bindings = b => b.SetBinding(nameof(TextBox.Value), valueBind)
            },
            ColumnType.Done or ColumnType.Bit or ColumnType.Boolean => new CheckBox()
            {
                Label = column.Header,
                Bindings = b => b.SetBinding(nameof(TextBox.Value), valueBind)
            },
            _ => new TextBox()
            {
                Label = column.Header,
                Bindings = b => b.SetBinding(nameof(TextBox.Value), valueBind)
            }
        };
    }

    internal Container CreateEditXaml(FormMetadata meta)
    {
        return meta.Is switch
        {
            FormKind.Dialog => CreateEditDialogXaml(meta),
            FormKind.Page => CreateDocumentPageXaml(meta),
            _ => throw new NotSupportedException($"FormKind {meta.Is} is not supported")
        };
    }

    internal Dialog CreateEditDialogXaml(FormMetadata meta)
    {
        return new Dialog()
        {
            Overflow = true,
            Bindings = b => b.SetBinding(nameof(Dialog.Title), new Bind($"{Table.Model}.Id") { Format = $$"""@[{{Table.Model}}] [{0}]"""}),
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
                    Children = [..meta.Elements.Select(ElementToControl)]
                }
            ]
        };
    }
}
