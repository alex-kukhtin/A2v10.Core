// Copyright © 2015-2017 Alex Kukhtin. All rights reserved.


namespace A2v10.Xaml
{
	public class StyleDescriptor
	{
		public RootContainer? Root;
		public String? StyleName;

		public void Set(XamlElement elem)
		{
			if (Root?.Styles == null)
				return;
			if (Root.Styles.TryGetValue(StyleName, out Style? style))
				style.Set(elem);
			else
				throw new XamlException($"Style '{StyleName}' not found");
		}
	}


	//[DefaultProperty("Member")]
	public class StyleResource : MarkupExtension
	{
		public String Member { get; set; }

		public StyleResource()
		{
		}

		public StyleResource(String member)
		{
			Member = member;
		}

		public override Object ProvideValue(IServiceProvider serviceProvider)
		{
			if (serviceProvider.GetService(typeof(IRootObjectProvider)) is not IRootObjectProvider iRoot)
				throw new InvalidOperationException("StyleResource.ProvideValue. IRootObjectProvider is null");

			if (iRoot.RootObject is not RootContainer root)
				return null;

			return new StyleDescriptor()
			{
				Root = root,
				StyleName = Member
			};
		}
	}
}
