// Copyright © 2015-2026 Oleksandr Kukhtin. All rights reserved.

namespace A2v10.Xaml;

public class PdfReportViewer : UIElementBase
{
	public String? Url { get; set; }
	public Length? Height { get; init; }
    public String? Report { get; init; }
    public String? Argument { get; init; }
    public override void RenderElement(RenderContext context, Action<TagBuilder>? onRender = null)
	{
		if (SkipRender(context))
			return;

		if (String.IsNullOrEmpty(Report))
			throw new XamlException("'Report' property is required for PdfReportViewer");
        if (String.IsNullOrEmpty(Url))
            throw new XamlException("'Url' property is required for PdfReportViewer");

        var tag = new TagBuilder("a2-pdfreport-viewer", "a2-pdfreport-viewer");

		if (Height != null)
			tag.MergeStyle("height", Height.Value);

		MergeAttributes(tag, context);

		MergeBindingAttributeString(tag, context, "url", nameof(Url), Url);
		MergeBindingAttributeString(tag, context, "report", nameof(Report), Report);
		MergeBindingAttributeString(tag, context, "argument", nameof(Argument), Argument);
		tag.Render(context);
	}
}
