// Copyright © 2022 Oleksandr Kukhtin. All rights reserved.

using System.Threading.Tasks;

using A2v10.Infrastructure;

namespace A2v10.ViewEngine.Xaml;

public class XamlPartProviderFile : IXamlPartProvider
{

	private readonly XamlReaderService _readerService;
	private readonly IAppCodeProvider _codeProvider;

	private Object? _xamlStyles = null;
	private Boolean _stylesLoaded = false;

	public XamlPartProviderFile(IAppCodeProvider codeProvider)
	{
		_readerService = new AppXamlReaderService(this, codeProvider);
		_codeProvider = codeProvider;
	}

	Object? LoadStyles()
	{
		if (_stylesLoaded)
			return _xamlStyles;
		_stylesLoaded = true;
		var fullPath = _codeProvider.MakeFullPath(String.Empty, "styles.xaml", false);
		using var stream = _codeProvider.FileStreamFullPathRO(fullPath);
		_xamlStyles = _readerService.Load(stream, new Uri(fullPath));
		return _xamlStyles;

	}

	public Object? GetXamlPart(String path)
	{
		if (path == "styles.xaml")
			return LoadStyles();
	
		var fullPath = _codeProvider.MakeFullPath(String.Empty, path, false);
		using var stream = _codeProvider.FileStreamFullPathRO(fullPath);
		return _readerService.Load(stream, new Uri(fullPath));
	}

	public Task<Object?> GetXamlPartAsync(String path)
	{
		throw new NotImplementedException();
	}
}
