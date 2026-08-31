// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;

using A2v10.Xaml;

namespace A2v10.Metadata;

internal partial class XamlBuilder
{
    /* Nothing of the blank is drawn here - a report engine does that from the layout. The page is
     * the frame around the viewer, and its only content is the address to point it at.
     *
     * The title: a declared header is a template over the record, so it is computed in the browser
     * and bound. Without one the menu caption stands in - static, but a name beats an empty tab and
     * there is nothing to guess about it.
     */
    public Page CreatePrintPageXaml()
    {
        var form = PrintRequest.FormOf(Endpoint, desciptor.PlatformUrl);
        var computed = !String.IsNullOrEmpty(form.Header);

        return new Page()
        {
            Title = computed ? null : form.Title,
            Bindings = computed
                ? b => b.SetBinding(nameof(Page.Title), new Bind($"{Table.Model}.$Title"))
                : null,
            Toolbar = new Toolbar(_xamlServiceProvider)
            {
                Children = [
                    FormButtons.Reload
                ]
            },
            Children = [
                new PdfViewer() {
                    Bindings = b => b.SetBinding(nameof(PdfViewer.Source),
                        new Bind($"{Table.Model}.$ReportUrl"))
                }
            ]
        };
    }
}
