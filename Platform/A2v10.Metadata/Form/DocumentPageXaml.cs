// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;

using A2v10.Xaml;

namespace A2v10.Metadata;

internal partial class XamlBuilder
{
    internal Page CreateDocumentPageXaml(FormMetadata form)
    {
        var columnWidths = form.Body.Select(x => x.Is == FormElementKind.Tabs ? "1*" : "auto");
        return new Page()
        {
            Toolbar = new Toolbar(_xamlServiceProvider)
            {
                Children = [..form.Toolbar.Commands.Select(ToolbarControl)]
            },
            Children = [
                new Grid(_xamlServiceProvider)
                {
                    Rows = RowDefinitions.FromString(String.Join(',', columnWidths)),
                    Height = Length.FromString("100%"),
                    Children = [..form.Body.Select(ElementToControl)]
                }
            ]
        };
    }
}
