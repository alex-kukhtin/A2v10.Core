// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;

using Newtonsoft.Json;

using A2v10.Infrastructure;

namespace A2v10.Services
{

	public class ModelJsonReader : IModelJsonReader
	{
		private readonly IAppCodeProvider _appCodeProvider;
		private readonly ICurrentUser _currentUser;

		private readonly RedirectModule _redirect;

		public ModelJsonReader(IAppCodeProvider appCodeProvider, IAppConfiguration appConig, ICurrentUser currentUser)
		{
			_appCodeProvider = appCodeProvider;
			_currentUser = currentUser;
			var redPath = _appCodeProvider.MakeFullPath(String.Empty, "redirect.json", false);
			if (appCodeProvider.FileExists(redPath))
				_redirect = new RedirectModule(redPath, appConig.Watch);
		}

		public async Task<IModelView> TryGetViewAsync(IPlatformUrl url)
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
				_ => null,
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

		public async Task<IModelBlob> GetBlobAsync(IPlatformUrl url, String suffix = null)
		{
			var rm = await Load(url);
			return url.Kind switch
			{
				UrlKind.Image => rm.GetBlob(url.Action, suffix),
				UrlKind.File => rm.GetFile(url.Action),
				_ => null
			};
		}

		public Task<ModelJson> Load(IPlatformUrl url)
		{
			return TryLoad(url) ?? throw new ModelJsonException($"File not found '{url.LocalPath}/model.json'");
		}

		public async Task<ModelJson> TryLoad(IPlatformUrl url)
		{
			var localPath = _redirect?.Redirect(url.LocalPath);
			url.Redirect(localPath);
			String json = await _appCodeProvider.ReadTextFileAsync(url.LocalPath, "model.json", _currentUser.IsAdminApplication);
			if (json == null)
				return null;
			var rm = JsonConvert.DeserializeObject<ModelJson>(json, JsonHelpers.CamelCaseSerializerSettings);
			rm.OnEndInit(url);
			return rm;
		}
	}
}
