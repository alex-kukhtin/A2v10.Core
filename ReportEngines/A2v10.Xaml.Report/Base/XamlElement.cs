// Copyright © 2022 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.Xaml.Report;

public class XamlElement : ISupportBinding
{

	public TextAlign? Align { get; init; }
	public VertAlign? VAlign { get; init; }

	public Boolean? Bold { get; init; }
	public Boolean? Italic { get; init; }
	public Boolean? Underline { get; init; }

	public Single? FontSize { get; init; }

	public Thickness? Margin { get; init ;}
	public Thickness? Padding { get; init; }
	public Thickness? Border { get; init; }
	public String? Color { get; init; }
	public String? Background { get; init; }
	public Boolean? If { get; init; }
	public Boolean ShowEntire { get; init; }

	BindImpl? _bindImpl;

	#region ISupportBinding
	public BindImpl BindImpl
	{
		get
		{
			_bindImpl ??= new BindImpl();
			return _bindImpl;
		}
	}

	protected RuntimeStyle? _runtimeStyle;

	public RuntimeStyle? RuntimeStyle => _runtimeStyle;

	public RuntimeStyle GetRuntimeStyle()
	{
		_runtimeStyle ??= new RuntimeStyle();
		return _runtimeStyle;
	}

	public Bind? GetBinding(String name)
	{
		return _bindImpl?.GetBinding(name);
	}

	public BindRuntime? GetBindRuntime(String name)
	{
		return _bindImpl?.GetBindRuntime(name);
	}
	#endregion

	public void ApplyStylesSelf()
	{
		if (Align != null)
			GetRuntimeStyle().Align = Align;
		if (VAlign != null)
			GetRuntimeStyle().VAlign = VAlign;
		if (Bold != null)
			GetRuntimeStyle().Bold = Bold;
		if (Italic != null)
			GetRuntimeStyle().Italic = Italic;
		if (Underline != null)
			GetRuntimeStyle().Underline = Underline;
		if (Margin != null)
			GetRuntimeStyle().Margin = Margin;
		if (Padding != null)
			GetRuntimeStyle().Padding = Padding;
		if (Border != null)
			GetRuntimeStyle().Border = Border;
		if (FontSize != null)
			GetRuntimeStyle().FontSize = FontSize;
		if (Color != null)
			GetRuntimeStyle().Color = Color;
		if (Background != null)
			GetRuntimeStyle().Background = Background;
	}

	public virtual void ApplyStyles(String selector, StyleBag styles)
	{
		ApplyStylesSelf();
	}
}
