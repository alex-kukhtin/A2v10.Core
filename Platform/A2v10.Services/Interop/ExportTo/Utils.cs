// Copyright © 2015-2026 Oleksandr Kukhtin. All rights reserved.

namespace A2v10.Services.Interop;

public struct ExClassList
{
	public HorizontalAlign Align { get; set; }
	public VerticalAlign VAlign { get; set; }
	public Boolean Bold { get; set; }
	public RowRole Role { get; set; }
	public UInt32 Indent { get; set; }
	public Boolean Underline { get; set; }
    public Boolean Vertical { get; set; }
    public Boolean IsGroup { get; set; }
    public RowColor RowColor { get; set; }
}


public static class Utils
{
	public static ExClassList ParseClasses(String strClass)
	{
		var lst = new ExClassList();
		if (String.IsNullOrEmpty(strClass))
			return lst;
		var split = strClass.Split(' ');
		UInt32 level = 0;
		foreach (var cls in split)
		{
			switch (cls)
			{
				case "row-header":
					lst.Role = RowRole.Header;
					break;
				case "row-footer":
					lst.Role = RowRole.Footer;
					break;
				case "row-title":
					lst.Role = RowRole.Title;
					break;
				case "row-total":
					lst.Role = RowRole.Total;
					break;
				case "row-parameter":
					lst.Role = RowRole.Parameter;
					break;
				case "row-divider":
					lst.Role = RowRole.Divider;
					break;
				case "vert":
					lst.Vertical = true;
					break;
				case "underline":
					lst.Underline = true;
					break;
                case "group":
                    lst.IsGroup = true;
                    break;
                case "group-red":
                    lst.RowColor = RowColor.Red;
                    break;
                case "group-cyan":
                    lst.RowColor = RowColor.Cyan;
                    break;
            }
            if (cls.StartsWith("text-"))
			{
				switch (cls)
				{
					case "text-center":
						lst.Align = HorizontalAlign.Center;
						break;
					case "text-right":
						lst.Align = HorizontalAlign.Right;
						break;
					case "text-left":
					case "text-default":
						lst.Align = HorizontalAlign.Left;
						break;
				}
			}
			if (cls.StartsWith("valign-"))
			{
				switch (cls)
				{
					case "valign-middle":
						lst.VAlign = VerticalAlign.Middle;
						break;
					case "valign-top":
						lst.VAlign = VerticalAlign.Top;
						break;
					case "valign-bottom":
						lst.VAlign = VerticalAlign.Bottom;
						break;
				}
			}
			if (cls == "bold")
				lst.Bold = true;
			if (cls.StartsWith("lev-"))
				level = UInt32.Parse(cls[4..]);
		}
		if (strClass.Contains("indent"))
			lst.Indent = level;
		return lst;
	}

    public static String BlendColors(String baseArgb, String overlayArgb)
    {
        var b = ParseArgb(baseArgb);
        var o = ParseArgb(overlayArgb);

        Double ao = o.a / 255.0;
        Double ab = b.a / 255.0;

        Double ar = ao + ab * (1 - ao);

        if (ar == 0)
            return "00000000";

        Int32 r = (Int32)((o.r * ao + b.r * ab * (1 - ao)) / ar);
        Int32 g = (Int32)((o.g * ao + b.g * ab * (1 - ao)) / ar);
        Int32 bl = (Int32)((o.b * ao + b.b * ab * (1 - ao)) / ar);
        Int32 a = (Int32)(ar * 255);

        return $"{a:X2}{r:X2}{g:X2}{bl:X2}";
    }

    public static (Int32 a, Int32 r, Int32 g, Int32 b) ParseArgb(String hex)
    {
        return (
            Convert.ToInt32(hex[0..2], 16),
            Convert.ToInt32(hex[2..4], 16),
            Convert.ToInt32(hex[4..6], 16),
            Convert.ToInt32(hex[6..8], 16)
        );
    }
}

