// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using A2v10.Infrastructure;
using A2v10.Services;

namespace TestServices;

// the same dispatch as ModelJsonReader, over a model.json built in code
internal sealed class TestModelReader(Func<ModelJson> file) : IModelJsonReader
{
	ModelJson Load(IPlatformUrl url)
	{
		var rm = file();
		rm.OnEndInit(url);
		return rm;
	}
	public Task<IModelView?> TryGetViewAsync(IPlatformUrl url) => throw new NotSupportedException();
	public Task<IModelView> GetViewAsync(IPlatformUrl url) => Task.FromResult<IModelView>(url.Kind switch
	{
		UrlKind.Dialog => Load(url).GetDialog(url.Action),
		_ => Load(url).GetAction(url.Action)
	});
	public Task<IModelBlob?> GetBlobAsync(IPlatformUrl url, String? suffix = null) => Task.FromResult<IModelBlob?>(url.Kind switch
	{
		UrlKind.Image => Load(url).GetBlob(url.Action, suffix),
		UrlKind.File => Load(url).GetFile(url.Action),
		_ => null
	});
	public Task<IModelBase> GetBaseAsync(IPlatformUrl url) => Task.FromResult(Load(url).GetBase());
	public Task<IModelBlobDoor?> GetBlobDoorAsync(IPlatformUrl url) => Task.FromResult<IModelBlobDoor?>(Load(url).Blobs.GetValueOrDefault(url.Action));
	public Task<IModelCommand> GetCommandAsync(IPlatformUrl url, String command) => Task.FromResult<IModelCommand>(Load(url).GetCommand(command));
	public Task<IModelReport> GetReportAsync(IPlatformUrl url) => throw new NotSupportedException();
}
