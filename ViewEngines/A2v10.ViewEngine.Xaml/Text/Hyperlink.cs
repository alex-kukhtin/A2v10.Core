﻿// Copyright © 2015-2025 Oleksandr Kukhtin. All rights reserved.

namespace A2v10.Xaml;

public enum HyperlinkStyle
{
	Default,
	Popover
}

[ContentProperty("Content")]
public class Hyperlink : Inline
{
	public Object? Content { get; set; }
	public ControlSize Size { get; set; }
	public Icon Icon { get; set; }
	public Boolean Highlight { get; set; }

	public Command? Command { get; set; }

	public UIElementBase? DropDown { get; set; }

	public HyperlinkStyle Style { get; set; }
	public Boolean HideCaret { get; set; }
	public String? TestId { get; set; }

	public Popover? Hint { get; set; }

	public override void RenderElement(RenderContext context, Action<TagBuilder>? onRender = null)
	{
		if (SkipRender(context))
			return;
		Boolean bHasDropDown = DropDown != null;
		if (bHasDropDown)
		{
			DropDownDirection? dir = (DropDown as DropDownMenu)?.Direction;
			Boolean bDropUp = (dir == DropDownDirection.UpLeft) || (dir == DropDownDirection.UpRight);
			var wrap = new TagBuilder("div", "dropdown hlink-dd-wrapper", IsInGrid)
				.AddCssClass(bDropUp ? "dir-up" : "dir-down")
				.MergeAttribute("v-dropdown", String.Empty);
			onRender?.Invoke(wrap);
			if (IsNoBlock)
				wrap.AddCssClass("a2-inline");
			else if (IsBlock)
				wrap.AddCssClass("block");
			if (Style != HyperlinkStyle.Default)
				wrap.AddCssClass(Style.ToString().ToLowerInvariant());
			MergeAttributesNoBlock(wrap, context, MergeAttrMode.Visibility);
			wrap.RenderStart(context);
			var hasAddOn = wrap.HasClass("add-on");
			RenderHyperlink(context, false, null, inside: true, addOn: hasAddOn);
			DropDown?.RenderElement(context);
			wrap.RenderEnd(context);
		}
		else
		{
			RenderHyperlink(context, IsInGrid, onRender, false);
		}
	}

	Boolean IsNavigateExternal()
	{
		var c = GetBindingCommand(nameof(Command));
		if (c == null)
			return false;
		return c.Command == CommandType.NavigateExternal;
	}

	void RenderHyperlink(RenderContext context, Boolean inGrid, Action<TagBuilder>? onRender = null, Boolean inside = false, Boolean addOn = false)
	{
		Boolean bHasDropDown = DropDown != null;

		var tag = new TagBuilder("a", "a2-hyperlink", inGrid);
		tag.MergeAttribute("href", "javascript:void(0)");
		tag.MergeAttribute("draggable", "false"); // disable drag'n'drop
		onRender?.Invoke(tag);
		var attrMode = MergeAttrMode.All;
		if (inside)
			attrMode &= ~MergeAttrMode.Visibility;
		MergeAttributesNoBlock(tag, context, attrMode);
		MergeCommandAttribute(tag, context);
		tag.AddCssClassBool(Highlight, "highlight");
		if (IsNoBlock)
			tag.AddCssClass("a2-inline");
		else if (IsBlock)
			tag.AddCssClass("block");
		if (Style != HyperlinkStyle.Default)
			tag.AddCssClass(Style.ToString().ToLowerInvariant());

		if (IsNavigateExternal())
			tag.MergeAttribute("rel", "noopener");

		if (Render == RenderMode.Show)
			tag.AddCssClass("rendershow");

		if (Size != ControlSize.Default)
		{
			switch (Size)
			{
				case ControlSize.Small:
					tag.AddCssClass("small");
					break;
				case ControlSize.Large:
					tag.AddCssClass("large");
					break;
				case ControlSize.Normal:
					tag.AddCssClass("normal");
					break;
				default:
					throw new XamlException("Only ControlSize.Small, ControlSize.Normal or ControlSize.Large are supported for the Hyperlink");
			}
		}

		if (bHasDropDown)
			tag.MergeAttribute("toggle", String.Empty);

		if (!String.IsNullOrEmpty(TestId) && context.IsDebugConfiguration)
			tag.MergeAttribute("test-id", TestId);

		tag.RenderStart(context);

		RenderIcon(context, Icon);
		var cbind = GetBinding(nameof(Content));
		if (cbind != null)
		{
			var stb = new TagBuilder("span")
				.MergeAttribute("v-text", cbind.GetPathFormat(context));
			if (cbind.NegativeRed)
                stb.MergeAttribute(":class", $"$getNegativeRedClass({cbind.GetPath(context)})");
            stb.Render(context);
		}
		else if (Content is UIElementBase uiElemBase)
		{
			uiElemBase.RenderElement(context);
		}
		else if (Content != null)
		{
			context.Writer.Write(context.LocalizeCheckApostrophe(Content.ToString()));
		}

		if (bHasDropDown && !addOn && !HideCaret)
		{
			var bDropUp = (DropDown as DropDownMenu)?.IsDropUp;
			new TagBuilder("span", "caret")
				.AddCssClassBool(bDropUp, "up")
				.Render(context);
		}
		RenderHint(context);
		tag.RenderEnd(context);
	}

	void RenderHint(RenderContext context)
	{
		if (Hint == null)
			return;
		if (Hint.Icon == Icon.NoIcon)
			Hint.Icon = Icon.Help;
		var tag = new TagBuilder("span");
		tag.RenderStart(context);
		Hint.RenderElement(context, (t) =>
		{
			t.AddCssClass("hint");
		});
		tag.RenderEnd(context);
	}

	protected override void OnEndInit()
	{
		base.OnEndInit();
		if (DropDown != null && GetBindingCommand(nameof(Command)) != null)
			throw new XamlException("Hyperlink. The DropDown and Command can't be specified at the same time");
	}
}
