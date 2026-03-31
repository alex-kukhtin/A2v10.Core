// Copyright © 2015-2026 Oleksandr Kukhtin. All rights reserved.

namespace A2v10.Xaml;

[ContentProperty("Content")]
public class ListItemSimple : UIElement
{
	public Object? Content { get; init; }
	public Icon Icon { get; init; }
	public Command? Command { get; init; }
    public CommandBar? CommandBar { get; init; }

    public override void RenderElement(RenderContext context, Action<TagBuilder>? onRender = null)
	{
		if (SkipRender(context))
			return;
		var div = new TagBuilder("div", "list-item-simple", IsInGrid);
		onRender?.Invoke(div);
		MergeAttributes(div, context);
		MergeCommandAttribute(div, context, withHref: false);
		div.RenderStart(context);
		RenderIconBlock(context);
		RenderBody(context);
        CommandBar?.RenderElement(context, tag =>
			{
				tag.AddCssClass("list-item-commands");
			}
		);
        div.RenderEnd(context);
	}

	Boolean HasBody => GetBinding(nameof(Content)) != null || Content != null;

	void RenderIconBlock(RenderContext context)
	{
		var iBind = GetBinding(nameof(Icon));
		if (iBind != null)
		{
			var iSpan = new TagBuilder("i", "ico list-item-simple-icon ");
			iSpan.MergeAttribute(":class", $"'ico-' + {iBind.GetPath(context)}");
			iSpan.Render(context, TagRenderMode.Normal, addSpace: true);
		}
		else if (Icon != Icon.NoIcon)
		{
			RenderIcon(context, Icon, "list-item-simple-icon");
		}
	}

	void RenderBody(RenderContext context)
	{
		if (!HasBody)
			return;
		var hBody = new TagBuilder("div", "list-item-simple-body");
		var bBody = GetBinding(nameof(Content));
		if (bBody != null)
			hBody.MergeAttribute("v-text", bBody.GetPathFormat(context));
		hBody.RenderStart(context);
		if (Content is UIElementBase uiElemBase)
			uiElemBase.RenderElement(context);
		else if (Content != null)
			context.Writer.Write(context.LocalizeCheckApostrophe(Content.ToString()));
		hBody.RenderEnd(context);
	}

    protected override void OnEndInit()
    {
        base.OnEndInit();
        CommandBar?.SetParent(this);
    }
}
