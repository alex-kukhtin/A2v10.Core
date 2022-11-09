// Copyright © 2022 Oleksandr Kukhtin. All rights reserved.

using A2v10.Infrastructure;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace A2v10.ViewEngine.Xaml;

public class XamlPartProviderClr : IXamlPartProvider
{

	private readonly XamlReaderService _readerService;
	private readonly IAppCodeProvider _codeProvider;

	private readonly Dictionary<String, Object?> _cache = new(StringComparer.InvariantCultureIgnoreCase);

	public XamlPartProviderClr(IAppCodeProvider codeProvider)
	{
		_readerService = new AppXamlReaderService(this, codeProvider);
		_codeProvider = codeProvider;
	}

	public Object? GetCachedXamlPart(String path)
	{
		if (_cache.TryGetValue(path, out var obj))
			return obj;
		var res = GetXamlPart(path);
		_cache.Add(path, res);
		return res;
	}

	public Object? GetXamlPart(String path)
	{
		var fullPath = _codeProvider.MakeFullPath(String.Empty, path, false);
		using var stream = _codeProvider.FileStreamFullPathRO(fullPath);
		return _readerService.Load(stream, new Uri("app:" + fullPath));
	}

	public Task<Object?> GetXamlPartAsync(String path)
	{
		throw new NotImplementedException();
	}
}
