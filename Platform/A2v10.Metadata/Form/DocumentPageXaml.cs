// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;

using A2v10.Xaml;

namespace A2v10.Metadata;

internal partial class XamlBuilder
{
    /* A document that switches between operations is titled by ITSELF: the operation is a value
     * picked on the page, and a title that changed with it would name the switch, not the document.
     * The key is the one the codes of its operations start with - '@[Operation.receipt]' over
     * 'receipt.supplier' - and for a document that is one implicit operation it is that operation's
     * own key, which its Operation field shows as the title already (ControlsXaml).
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
