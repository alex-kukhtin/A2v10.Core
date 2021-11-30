// Copyright © 2021 Alex Kukhtin. All rights reserved.

using A2v10.Infrastructure;

namespace A2v10.Xaml;
public class AppXamlReaderService : XamlReaderService
{
	private readonly IAppCodeProvider _codeProvider;
	private readonly XamlServicesOptions _options;

	public AppXamlReaderService(IAppCodeProvider codeProvider)
	{
		_codeProvider = codeProvider;
		_options = new XamlServicesOptions(Array.Empty<NamespaceDef>())
		{
			OnCreateReader = (rdr) =>
			{
				rdr.InjectService<IAppCodeProvider>(_codeProvider);
				rdr.InjectService<IXamlReaderService>(this);
			}
		};
	}

	public override XamlServicesOptions Options => _options;
}

