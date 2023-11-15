// Copyright © 2021-2023 Oleksandr Kukhtin. All rights reserved.

using A2v10.Infrastructure;

namespace A2v10.ViewEngine.Xaml;
public class AppXamlReaderService : XamlReaderService
{
	private readonly XamlServicesOptions _options;

	public AppXamlReaderService(IXamlPartProvider partProvider, IAppCodeProvider codeProvider)
	{
		_options = new XamlServicesOptions([])
		{
			OnCreateReader = (rdr) =>
			{
				rdr.InjectService<IXamlPartProvider>(partProvider);
				rdr.InjectService<IAppCodeProvider>(codeProvider);
			}
		};
	}

	public override XamlServicesOptions Options => _options;
}

