// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;

using A2v10.Xaml;

namespace A2v10.Metadata;

internal partial class XamlBuilder
{
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
                    Children = [..meta.Body.Select(ElementToControl)]
                }
            ]
        };
    }
}
