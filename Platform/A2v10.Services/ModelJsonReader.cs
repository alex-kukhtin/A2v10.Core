// Copyright © 2015-2025 Oleksandr Kukhtin. All rights reserved.

using System.Threading.Tasks;

namespace A2v10.Services;

public class ModelJsonReader(IModelJsonPartProvider _partProvider, IAppRuntimeBuilder _appRuntimeBuilder) : IModelJsonReader
{
    public async Task<IModelView?> TryGetViewAsync(IPlatformUrl url)
	{
		if (url.Kind != UrlKind.Page)
			return null;
		var rm = await TryLoad(url);
		if (rm == null)
			return null;
		return rm.GetAction(url.Action);
	}

	/* A folder with no model.json at all: the whole of it is served by the metadata layer, and so is
	 * what its model.json would say - requireBase, and the doors: the fields the layer says hold a blob,
	 * each under the model the data come as ('$meta' routes, it names nothing). A model.json the author
	 * wrote is never added to
	 */
	async Task<ModelJson> CreateMetaAsync(IPlatformUrl url)
	{
		var endpoint = await _appRuntimeBuilder.DescribeAsync(url.LocalPath);
        var ms = new ModelJson()
        {
			Model = IModelBase.MetaModel,
			RequireBase = endpoint.RequireBase,
			Blobs = endpoint.Blobs.ToDictionary(kv => kv.Key,
				kv => new ModelJsonBlobNew() { Model = kv.Value.Model, Accept = kv.Value.Accept, MaxSize = kv.Value.MaxSize },
				StringComparer.OrdinalIgnoreCase)
        };
        ms.OnEndInit(url);
		return ms;
    }

    static ModelJson CreateAuto(IPlatformUrl url, String? command = null)
	{
		var model = String.Join('.', url.LocalPath.Split('/').Select(s => s.ToPascalCase()));
		var ms = new ModelJson()
		{
			Model = model
		};
		if (!String.IsNullOrEmpty(command))
		{
			ms.Commands.Add(command, new ModelJsonCommand()
			{
				Type = ModelCommandType.auto,
				Procedure = command.ToPascalCase(),
			});
		}
		else if (url.Kind == UrlKind.Page)
		{
			ms.Actions.Add(url.Action, new ModelJsonView()
			{
				Index = url.Action == "index",
				Auto = new ModelJsonAuto() { Render = AutoRender.Page }
			});
		}
		else if (url.Kind == UrlKind.Dialog)
			ms.Dialogs.Add(url.Action, new ModelJsonDialog()
			{
				Index = url.Action == "browse",	
				Auto = new ModelJsonAuto() { Render = AutoRender.Page }
			});
		ms.OnEndInit(url);
		return ms;
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
		var rm = await Load(url, command);
		return rm.GetCommand(command);
	}

	public async Task<IModelReport> GetReportAsync(IPlatformUrl url)
	{
		var rm = await Load(url);
		return rm.GetReport(url.Action);
	}

	// through Load, as every element: the doors of a metadata folder exist only in the root it synthesizes
	public async Task<IModelBlobDoor?> GetBlobDoorAsync(IPlatformUrl url)
	{
		var rm = await Load(url);
		return rm.Blobs.GetValueOrDefault(url.Action);
	}

	public async Task<IModelBase> GetBaseAsync(IPlatformUrl url) => (await Load(url)).GetBase();

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

	public async Task<ModelJson> Load(IPlatformUrl url, String? command = null)
	{
		var ms = await TryLoad(url);
		if (ms != null)
			return ms;
		if (_appRuntimeBuilder.IsAutoSupported)
			return CreateAuto(url, command);
		else if (_appRuntimeBuilder.IsMetaSupported)
			return await CreateMetaAsync(url);
		throw new ModelJsonException($"File not found '{url.LocalPath}/model.json'");
	}

	public Task<ModelJson?> TryLoad(IPlatformUrl url)
	{
		return _partProvider.GetModelJsonAsync(url);
	}
}
