// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System.Threading.Tasks;

namespace A2v10.Services;

internal class NullXamlPartProvider : IXamlPartProvider
{
    public Object? GetCachedXamlPart(String path) => null;

    public Object? GetCachedXamlPartOrNull(String path) => null;


    public Object? GetXamlPart(String path) => null;

    public Task<Object?> GetXamlPartAsync(String path) => Task.FromResult<Object?>(null);

    public Object? GetXamlPartText(String text, String path) => null;
}
