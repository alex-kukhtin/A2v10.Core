// Copyright © 2021-2026 Oleksandr Kukhtin. All rights reserved.

using A2v10.Infrastructure;

namespace A2v10.ViewEngine.Xaml;
public class AppXamlReaderService : XamlReaderService
{
	private readonly XamlServicesOptions _options;

	public AppXamlReaderService(IXamlPartProvider partProvider, IAppCodeProvider codeProvider)
	{
		_options = new XamlServicesOptions([])
		{
			// the reader does not know which container renders nothing; a wrapper that does
			// (Block, StackPanel) passes validation and shifts the layout unseen
			SingleContentHint = "Wrap the children in <Group>: it renders no element of its own.",
			OnCreateReader = (rdr) =>
			{
				rdr.InjectService<IXamlPartProvider>(partProvider);
				rdr.InjectService<IAppCodeProvider>(codeProvider);
			}
		};
	}

	public override XamlServicesOptions Options => _options;
}

