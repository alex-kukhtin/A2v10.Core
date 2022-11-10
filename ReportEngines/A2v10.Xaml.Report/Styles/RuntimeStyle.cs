// Copyright © 2022 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.Xaml.Report;


public class RuntimeStyle
{
	public Single? FontSize { get; set; }
	public TextAlign? Align { get; set; }
	public VertAlign? VAlign { get; set; }
	public String? Background {get; set;}
	public Boolean? Bold { get; set; }
	public Boolean? Italic { get; set;}
	public Boolean? Underline { get; set; }

	public Thickness? Margin { get; set; }
	public Thickness? Padding { get; set; }
	public Thickness? Border { get; set; }
	public String? Color { get; set; }

	public RuntimeStyle Clone()
	{
		return new RuntimeStyle()
		{
			FontSize = this.FontSize,
			Padding = this.Padding,
			Margin = this.Margin,
			Align = this.Align,
			VAlign = this.VAlign,
			Border = this.Border,
			Background = this.Background,
			Bold = this.Bold,
			Italic = this.Italic,
			Underline = this.Underline,
			Color = this.Color
		};
	}
}
