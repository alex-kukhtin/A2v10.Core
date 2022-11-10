// Copyright © 2022 Oleksandr Kukhtin. All rights reserved.

using QuestPDF.Infrastructure;
using QuestPDF.Drawing;

using QuestPDF.Fluent;

using A2v10.Xaml.Report;

namespace A2v10.ReportEngine.Pdf;

internal class ReportDocument : IDocument
{
	private readonly Page _page;
	private readonly RenderContext _context;
	public ReportDocument(Page page, RenderContext context)
	{
		_page = page;
		_context = context;
	}

	public void Compose(IDocumentContainer container)
	{
		container.Page(page =>
		{
			new PageComposer(_page, _context).Compose(page);
		});
	}

	public DocumentMetadata GetMetadata()
	{
		var title = _context.GetValueAsString(_page, "Title");
		title ??= _page.Title;
		var md = DocumentMetadata.Default;
		md.Title = title;
		return md;
	}
}
