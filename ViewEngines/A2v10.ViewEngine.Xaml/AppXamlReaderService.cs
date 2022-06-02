// Copyright © 2021-2022 Alex Kukhtin. All rights reserved.

using A2v10.Infrastructure;

namespace A2v10.ViewEngine.Xaml;
public class AppXamlReaderService : XamlReaderService
{
	private readonly XamlServicesOptions _options;

	public AppXamlReaderService(IXamlPartProvider partProvider)
	{
		_options = new XamlServicesOptions(Array.Empty<NamespaceDef>())
		{
			OnCreateReader = (rdr) =>
			{
				rdr.InjectService<IXamlPartProvider>(partProvider);
				rdr.InjectService<IXamlReaderService>(this);
			}
		};
	}

	public override XamlServicesOptions Options => _options;
}

