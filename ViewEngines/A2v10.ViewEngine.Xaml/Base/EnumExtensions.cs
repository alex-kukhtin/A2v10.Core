// Copyright © 2015-2017 Alex Kukhtin. All rights reserved.

namespace A2v10.Xaml
{
	public static class EnumExtensions
	{
		public static String? AlignSelf(this AlignItem vAlign)
		{
			return vAlign switch
			{
				AlignItem.Top    or AlignItem.Start  => "start",
				AlignItem.Middle or AlignItem.Center => "center",
				AlignItem.Bottom or AlignItem.End    => "end",
				AlignItem.Stretch => "stretch",
				_ => null,
			};
		}
	}
}
