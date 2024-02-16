// Copyright © 2022-2024 Oleksandr Kukhtin. All rights reserved.

using QuestPDF.Infrastructure;

using QuestPDF.Fluent;

using A2v10.Xaml.Report;
using A2v10.ReportEngine.Script;

namespace A2v10.ReportEngine.Pdf;

internal class ReportDocument(Page page, RenderContext context) : IDocument
{
	private readonly Page _page = page;
	private readonly RenderContext _context = context;

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
		title ??= _context.ResolveModel(_page.Title);
		var md = DocumentMetadata.Default;
		md.Title = title;
		return md;
	}
}
