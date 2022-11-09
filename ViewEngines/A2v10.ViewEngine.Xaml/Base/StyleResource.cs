// Copyright © 2015-2022 Alex Kukhtin. All rights reserved.


namespace A2v10.Xaml;

public class StyleDescriptor
{
	public RootContainer? Root;
	public String StyleName { get; set; } = String.Empty;

	public void Set(XamlElement elem, RootContainer rootContainer)
	{
		var root = Root ?? rootContainer;
		if (root == null)
			return;
		if (root?.Styles == null)
			return;
		if (root.Styles.TryGetValue(StyleName, out Style? style))
			style.Set(elem);
		else
			throw new XamlException($"Style '{StyleName}' not found");
	}
}


//[DefaultProperty("Member")]
public class StyleResource : MarkupExtension
{
	public String Member { get; set; } = String.Empty;

	public StyleResource()
	{
	}

	public StyleResource(String member)
	{
		Member = member;
	}

	public override Object? ProvideValue(IServiceProvider serviceProvider)
	{
		if (serviceProvider.GetService(typeof(IRootObjectProvider)) is not IRootObjectProvider iRoot)
			throw new InvalidOperationException("StyleResource.ProvideValue. IRootObjectProvider is null");

		return new StyleDescriptor()
		{
			Root = iRoot.RootObject as RootContainer,
			StyleName = Member
		};
	}
}
