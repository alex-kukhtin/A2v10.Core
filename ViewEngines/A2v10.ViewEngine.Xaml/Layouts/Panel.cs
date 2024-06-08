// Copyright © 2015-2023 Oleksandr Kukhtin. All rights reserved.

using A2v10.Infrastructure;

namespace A2v10.Xaml;

public enum PaneStyle
{
	Default,
	Info,
	Warning,
	Danger,
	Error,
	Success,
	Green,
	Cyan,
	Red,
	Yellow,
	Cut,
	Transparent
}

public class Panel : Container, ITableControl
{
	public Object? Header { get; set; }

	public Boolean Collapsible { get; set; }
	public Boolean? Collapsed { get; set; }

	public PaneStyle Style { get; set; }
	public Icon Icon { get; set; }

	public BackgroundStyle Background { get; set; }

	public ShadowStyle DropShadow { get; set; }
	public Length? Height { get; set; }
	public Boolean Compact { get; set; }

	public Popover? Hint { get; set; }
    public GapSize? Gap { get; set; }

    public String? TestId { get; set; }
	public Object? Description { get; set; }

	public override void RenderElement(RenderContext context, Action<TagBuilder>? onRender = null)
	{
		if (SkipRender(context))
			return;
		var panel = new TagBuilder("a2-panel", null, IsInGrid);
		MergeBindingAttributeBool(panel, context, ":initial-collapsed", nameof(Collapsed), Collapsed);
		MergeBindingAttributeBool(panel, context, ":collapsible", nameof(Collapsible), Collapsible);
		if (!String.IsNullOrEmpty(TestId) && context.IsDebugConfiguration)
			panel.MergeAttribute("test-id", TestId);
		panel.AddCssClassBool(Compact, "compact");
		if (!HasHeader)
			panel.MergeAttribute(":no-header", "true");
		var sb = GetBinding(nameof(Style));
		if (sb != null)
			panel.MergeAttribute(":panel-style", sb.GetPathFormat(context));
		else if (Style != PaneStyle.Default)
			panel.MergeAttribute("panel-style", Style.ToString().ToLowerInvariant());
		MergeAttributes(panel, context, MergeAttrMode.Visibility);
		if (Background != BackgroundStyle.Default)
			panel.AddCssClass("background-" + Background.ToString().ToKebabCase());
		if (Height != null)
			panel.MergeStyle("height", Height.Value);
		if (DropShadow != ShadowStyle.None)
		{
			panel.AddCssClass("drop-shadow");
			panel.AddCssClass(DropShadow.ToString().ToLowerInvariant());
		}
		panel.RenderStart(context);
		RenderHeader(context);
		var content = new TagBuilder("div", "panel-content");
		MergeAttributes(content, context, MergeAttrMode.Margin | MergeAttrMode.Wrap | MergeAttrMode.Tip);

        if (Gap != null)
            content.MergeStyle("gap", Gap.ToString());

        content.RenderStart(context);
		RenderChildren(context);
		content.RenderEnd(context);
		panel.RenderEnd(context);
	}

	Boolean HasHeader => GetBinding(nameof(Header)) != null || Header != null || Icon != Icon.NoIcon;

	void RenderHeader(RenderContext context)
	{
		if (!HasHeader)
			return;
		var header = new TagBuilder("div", "panel-header-slot");
		header.MergeAttribute("slot", "header");
		header.RenderStart(context);

		RenderIcon(context, Icon);

		var hBind = GetBinding(nameof(Header));
		if (hBind != null)
		{
			var span = new TagBuilder("span");
			span.MergeAttribute("v-text", hBind.GetPathFormat(context));
			span.Render(context);
		}
		else if (Header is UIElementBase uiElemBase)
		{
			uiElemBase.RenderElement(context);
		}
		else if (Header != null)
		{
			context.Writer.Write(context.LocalizeCheckApostrophe(Header.ToString()));
		}
		RenderDesription(context);
		RenderHint(context);
		header.RenderEnd(context);
	}

	void RenderDesription(RenderContext context)
	{
		var dBind = GetBinding(nameof(Description));
		if (dBind == null && Description == null)
			return;
		new ToolbarAligner().RenderElement(context);
		var wrap = new TagBuilder(null, "a2-panel-description");
		wrap.RenderStart(context);
		if (dBind != null)
		{
			var span = new TagBuilder("span");
			span.MergeAttribute("v-text", dBind.GetPathFormat(context));
			span.Render(context);
		}
		else if (Description is UIElementBase uiDescr)
			uiDescr.RenderElement(context);
		else if (Description != null)
			context.Writer.Write(context.LocalizeCheckApostrophe(Description.ToString()));
		wrap.RenderEnd(context);
	}

	void RenderHint(RenderContext context)
	{
		if (Hint == null)
			return;
		if (Hint.Icon == Icon.NoIcon)
			Hint.Icon = Icon.Help;
		Hint.RenderElement(context, (t) =>
		{
			t.AddCssClass("hint");
		});
	}

}
