// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System.Linq;
using A2v10.Xaml;

namespace A2v10.Metadata;

internal partial class XamlBuilder
{
    internal Page CreateDocumentPageXaml(FormMetadata form)
    {
        return new Page()
        {
            Toolbar = new Toolbar(_xamlServiceProvider)
            {
                Children = [..form.Toolbar.Select(ToolbarControl)]
            },
            Children = [
                new Grid(_xamlServiceProvider)
                {
                    Children = [..form.Elements.Select(ElementToControl)]
                }
            ]
        };
    }
}
