// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;

using A2v10.Xaml;

namespace A2v10.Metadata;

internal partial class XamlBuilder
{
    /* Why the document name and not the operation: the operation is switched on the page, and a
     * title that changed with it would name the switch, not the document. Why the Operation model
     * as the key prefix: operation codes start with the document name, so the dictionary has one
     * family for the document and its operations. Why no header for one implicit operation: its
     * Operation field already shows that key (ControlsXaml), a header would repeat it.
     */
    internal Page CreateDocumentPageXaml(FormMetadata form)
    {
        UIElementBase[] title = Endpoint.Declaration.OperationDeclarations.Count > 0
            ? [new Header() { Content = $"@[{TableMetadataDefaults.OperationsTable().Model}.{Endpoint.Name}]" }]
            : [];
        var columnWidths = title.Select(_ => "auto")
            .Concat(form.Body.Select(x => x.Is == FormElementKind.Tabs ? "1*" : "auto"));
        var taskpad = (Taskpad)ElementToControl(form.Taskpad);
        return new Page()
        {
            Toolbar = EditToolbar(form.Toolbar),
            Children = [
                new Grid(_xamlServiceProvider)
                {
                    Rows = RowDefinitions.FromString(String.Join(',', columnWidths)),
                    Height = Length.FromString("100%"),
                    Padding = Thickness.FromString("20px"),
                    Children = [.. title, ..form.Body.Select(ElementToControl)]
                }
            ],
            Taskpad = taskpad.Children.Count > 0 ? taskpad : null
        };
    }
}
