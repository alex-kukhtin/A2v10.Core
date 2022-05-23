// Copyright © 2015-2022 Alex Kukhtin. All rights reserved.

using System.Threading.Tasks;

namespace A2v10.Services;

public class ModelJsonReader : IModelJsonReader
{
	private readonly IModelJsonPartProvider _partProvider;

	public ModelJsonReader(IModelJsonPartProvider partProvider)
	{
		_partProvider = partProvider;
	}

	public async Task<IModelView?> TryGetViewAsync(IPlatformUrl url)
	{
		if (url.Kind != UrlKind.Page)
			return null;
		var rm = await TryLoad(url);
		if (rm == null)
			return null;
		return rm.GetAction(url.Action);
	}

	public async Task<IModelView> GetViewAsync(IPlatformUrl url)
	{
		var rm = await Load(url);
		return url.Kind switch
		{
			UrlKind.Page => rm.GetAction(url.Action),
			UrlKind.Dialog => rm.GetDialog(url.Action),
			UrlKind.Popup => rm.GetPopup(url.Action),
			_ => throw new ModelJsonException($"Invalid view kind 'url.Kind'"),
		};
	}

	public async Task<IModelCommand> GetCommandAsync(IPlatformUrl url, String command)
	{
		var rm = await Load(url);
		return rm.GetCommand(command);
	}

	public async Task<IModelReport> GetReportAsync(IPlatformUrl url)
	{
		var rm = await Load(url);
		return rm.GetReport(url.Action);
	}

	public async Task<IModelBlob?> GetBlobAsync(IPlatformUrl url, String? suffix = null)
	{
		var rm = await Load(url);
		return url.Kind switch
		{
			UrlKind.Image => rm.GetBlob(url.Action, suffix),
			UrlKind.File => rm.GetFile(url.Action),
			_ => null
		};
	}

	public async Task<ModelJson> Load(IPlatformUrl url)
	{
		return await TryLoad(url) ?? throw new ModelJsonException($"File not found '{url.LocalPath}/model.json'");
	}

	public Task<ModelJson?> TryLoad(IPlatformUrl url)
	{
		return _partProvider.GetModelJsonAsync(url);
	}
}
