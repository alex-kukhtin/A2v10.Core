// Copyright © 2015-2025 Alex Kukhtin. All rights reserved.


namespace A2v10.Xaml;

public enum AlertStyle
{
	Default,
	Success,
	Warning,
	Info,
	Danger,
	Dark,
	Light,
	//Primary,
	//Secondary
}

[ContentProperty("Content")]
public class Alert : UIElement
{
	public Object? Content { get; set; }
	public AlertStyle Style { get; set; }
	public Icon Icon { get; set; }
	public ShadowStyle DropShadow { get; set; }


	public override void RenderElement(RenderContext context, Action<TagBuilder>? onRender = null)
	{
		if (SkipRender(context))
			return;
		var tag = new TagBuilder("div", "a2-alert", IsInGrid);
		tag.MergeAttribute("role", "alert");
		MergeAttributes(tag, context);
		var bindStyle = GetBinding(nameof(Style));
		if (bindStyle != null)
			tag.MergeAttribute(":class", bindStyle.GetPath(context));
		else if (Style != AlertStyle.Default)
			tag.AddCssClass(Style.ToString().ToLowerInvariant());
		if (DropShadow != ShadowStyle.None)
		{
			tag.AddCssClass("drop-shadow");
			tag.AddCssClass(DropShadow.ToString().ToLowerInvariant());
		}
		tag.RenderStart(context);
		RenderIcon(context, Icon);
		if (Content is UIElementBase uiElemBase)
		{
			uiElemBase.RenderElement(context, null);
		}
		else
		{
			var span = new TagBuilder("span");
			var cbind = GetBinding(nameof(Content));
			if (cbind != null)
				span.MergeAttribute("v-text", cbind.GetPathFormat(context));
			else if (Content != null)
				span.SetInnerText(Content.ToString()!);
			span.Render(context);
		}
		tag.RenderEnd(context);
	}

    protected override void OnEndInit()
    {
        base.OnEndInit();
        if (Content != null && Content is XamlElement xaml)
            xaml.SetParent(this);
    }
}

