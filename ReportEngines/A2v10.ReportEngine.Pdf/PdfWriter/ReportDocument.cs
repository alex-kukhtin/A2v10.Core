// Copyright © 2022-2024 Oleksandr Kukhtin. All rights reserved.

using QuestPDF.Infrastructure;

using QuestPDF.Fluent;

using A2v10.Xaml.Report;
using A2v10.ReportEngine.Script;
using A2v10.Xaml.Report.Spreadsheet;

namespace A2v10.ReportEngine.Pdf;

internal class ReportDocument(Page page, RenderContext context) : IDocument
{
	private readonly Page _page = page;
	private readonly RenderContext _context = context;

    public void Compose(IDocumentContainer container)
	{
		container.Page(page =>
		{
			if (_page is Spreadsheet ss)
				new SpreadsheetComposer(ss, _context).Compose(page);
			else
				new PageComposer(_page, _context).Compose(page);
			ComposeWatermark(page);
		});
	}

	// Место одно, потому что через него проходят обе ветки, и _page тут на руках —
	// иначе знак пришлось бы заводить дважды, в PageComposer и в SpreadsheetComposer.
	// Background, а не Foreground: знак идёт ПОД содержимым. Принятая цена — ячейку
	// с явной заливкой он не просветит, это лечится на стороне бланка
	private void ComposeWatermark(PageDescriptor page)
	{
		var image = _context.ResolveImage(_page, _context.DataModel, nameof(Page.Watermark), _page.Watermark);
		if (image == null)
			return;
		page.Background().DrawImage(image);
	}

	public DocumentMetadata GetMetadata()
	{
		var title = _context.GetValueAsString(_page, _context.DataModel, "Title");
		title ??= _context.ResolveModel(_page.Title);
		var md = DocumentMetadata.Default;
		md.Title = title;
		return md;
	}
}
