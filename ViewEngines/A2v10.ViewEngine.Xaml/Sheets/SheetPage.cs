// Copyright © 2015-2023 Oleksandr Kukhtin. All rights reserved.

namespace A2v10.Xaml;

public class SheetPage : Container, IHasWrapper
{
	public PageOrientation Orientation { get; set; }
	public Size? PageSize { get; set; }

	public PrintPage? PrintPage { get; set; }

	public override void RenderElement(RenderContext context, Action<TagBuilder>? onRender = null)
	{
		var wrap = new TagBuilder("div", "sheet-page-wrapper", IsInGrid);
		MergeAttributes(wrap, context);
		wrap.RenderStart(context);
		var page = new TagBuilder("div", "sheet-page");

		ApplyPropsFromChildren(context);

		if (PrintPage != null)
		{
			page.AddCssClass(PrintPage.Orientation.ToString().ToLowerInvariant());
			page.MergeAttribute("v-print-page", PrintPage.ToJson());
		}
		else
		{
			page.AddCssClass(Orientation.ToString().ToLowerInvariant());
			page.MergeAttribute("v-page-orientation", $"'{Orientation.ToString().ToLowerInvariant()}'");
		}

		if (PageSize != null)
		{
			if (!PageSize.Width.IsEmpty)
			{
				page.MergeStyle("width", PageSize.Width.ToString());
				var pw = PageSize.Width.ToString();
				if (pw == "auto")
					pw = "none";
				page.MergeStyle("max-width", pw);
			}
			if (!PageSize.Height.IsEmpty)
			{
				page.MergeStyle("min-height", PageSize.Height.ToString());
			}
		}

		page.RenderStart(context);
		RenderChildren(context);
		page.RenderEnd(context);
		wrap.RenderEnd(context);
	}

    void ApplyPropsFromChildren(RenderContext context)
	{
		if (Children == null || Children.Count == 0)
			return;
		var ch = Children[0];
		if (ch is not Sheet sheet)
			return;
		sheet.ApplySheetPageProps(context, this);
	}
}
