// Copyright © 2015-2022 Alex Kukhtin. All rights reserved.

namespace A2v10.Xaml;

public static class EnumExtensions
{
	public static String? AlignSelf(this AlignItem vAlign)
	{
		return vAlign switch
		{
			AlignItem.Top or AlignItem.Start => "start",
			AlignItem.Middle or AlignItem.Center => "center",
			AlignItem.Bottom or AlignItem.End => "end",
			AlignItem.Stretch => "stretch",
			_ => null,
		};
	}

	public static String? ToClass(this Overflow? overflow)
	{
		return overflow switch
		{
			Overflow.Visible or Overflow.True => "of-visible",
			Overflow.Hidden or Overflow.False => "of-hidden",
			Overflow.Auto => "of-auto",
			_ => null
		};
	}
}
