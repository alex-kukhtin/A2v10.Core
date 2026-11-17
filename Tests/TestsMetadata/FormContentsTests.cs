// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

namespace A2v10.Metadata.Tests;

/* A key written on a form node that never reads it is refused (FormElement.CheckContents): the node would
 * render, empty of it, and leave no trace.
 */
public class FormContentsTests
{
    // a tab draws the grid of its rows: what a toolbar or the filters would hold goes nowhere
    [Fact]
    public void A_tab_reads_its_fields_and_nothing_else()
    {
        FormElement.CheckElement(FormElementKind.Tabs, new FormElement() { Is = FormElementKind.Tab, Scope = "Rows", Fields = ["Item"] });

        var ex = Assert.Throws<InvalidOperationException>(() => FormElement.CheckElement(FormElementKind.Tabs,
            new FormElement() { Is = FormElementKind.Tab, Scope = "Rows", Fields = ["Item"], Filters = ["Agent"], Commands = [CommandBarItem.Separator] }));
        Assert.Equal("'tab' declares 'filters', 'commands', which nothing reads. It shows 'fields' and nothing else.", ex.Message);
    }
}
