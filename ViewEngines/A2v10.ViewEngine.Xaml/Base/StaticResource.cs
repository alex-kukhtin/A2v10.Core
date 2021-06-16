// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using System;
using A2v10.System.Xaml;

namespace A2v10.Xaml
{
	public class StaticResource : MarkupExtension
	{
		public String Member { get; set; }

		public StaticResource()
		{
		}

		public StaticResource(String member)
		{
			Member = member;
		}

		public override Object ProvideValue(IServiceProvider serviceProvider)
		{
			if (serviceProvider.GetService(typeof(IRootObjectProvider)) is not IRootObjectProvider iRoot)
				throw new InvalidOperationException("StaticResource.ProvideValue. IRootObjectProvider is null");
			if (iRoot.RootObject is not RootContainer root)
				return null;
			Object resrc = root.FindResource(Member);
			if (resrc == null)
				throw new XamlException($"Resource '{Member}' not found");
			return resrc;
		}
	}
}
