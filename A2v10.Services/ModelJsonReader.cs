﻿// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;

using Newtonsoft.Json;

using A2v10.Infrastructure;

namespace A2v10.Services
{

	public class ModelJsonReader : IModelJsonReader
	{
		private readonly IAppCodeProvider _appCodeProvider;

		public ModelJsonReader(IAppCodeProvider appCodeProvider)
		{
			_appCodeProvider = appCodeProvider;
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

		public async Task<ModelJson> Load(IPlatformUrl url)
		{
			String json = await _appCodeProvider.ReadTextFileAsync(url.LocalPath, "model.json");
			var rm = JsonConvert.DeserializeObject<ModelJson>(json, JsonHelpers.CamelCaseSerializerSettings);
			rm.OnEndInit(url);
			return rm;
		}
	}
}
