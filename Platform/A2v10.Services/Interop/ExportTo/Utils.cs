﻿// Copyright © 2015-2025 Oleksandr Kukhtin. All rights reserved.

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
}
