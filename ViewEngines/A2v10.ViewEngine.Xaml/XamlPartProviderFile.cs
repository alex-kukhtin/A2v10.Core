// Copyright © 2022 Oleksandr Kukhtin. All rights reserved.

using System.IO;
using System.Threading.Tasks;

using A2v10.Infrastructure;

namespace A2v10.ViewEngine.Xaml;

public class XamlPartProviderFile : IXamlPartProvider
{

	private readonly XamlReaderService _readerService;
	private readonly IAppCodeProvider _codeProvider;

	public XamlPartProviderFile(IAppCodeProvider codeProvider)
	{
		_readerService = new AppXamlReaderService(this);
		_codeProvider = codeProvider;
	}
	public Object GetXamlPart(String path)
	{
		var fullPath = _codeProvider.MakeFullPath(String.Empty, path, false);
		using var stream = _codeProvider.FileStreamFullPathRO(fullPath);
		return _readerService.Load(stream, new Uri(fullPath));
	}

	public Task<Object?> GetXamlPartAsync(String path)
	{
		throw new NotImplementedException();
	}
}
