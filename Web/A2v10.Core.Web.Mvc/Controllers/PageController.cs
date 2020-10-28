// Copyright © 2015-2020 Alex Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.IO;
using System.Threading.Tasks;
using System.Web;

using Microsoft.AspNetCore.Mvc;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Core.Web.Mvc.Controllers
{
	public class PageController : BaseController
	{

		public Boolean Admin => _host.IsAdminMode;

		public PageController(IDbContext dbContext, IApplicationHost host, IAppCodeProvider codeProvider)
			: base(dbContext, host, codeProvider)
		{
		}

		[Route("_page/{*pathInfo}")]
		public Task Page(String pathInfo)
		{
			// {pagePath}/action/id
			return Render(pathInfo, RequestUrlKind.Page);
		}

		[Route("_dialog/{*pathInfo}")]
		public Task Dialog(String pathInfo)
		{
			// {pagePath}/dialog/id
			return Render(pathInfo, RequestUrlKind.Dialog);
		}

		[Route("_popup/{*pathInfo}")]
		public Task Popup(String pathInfo)
		{
			// {pagePath}/popup/id
			return Render(pathInfo, RequestUrlKind.Popup);
		}

		async Task Render(String path, RequestUrlKind kind)
		{
			try
			{
				ExpandoObject loadPrms = new ExpandoObject();
				path = path.ToLowerInvariant();
				loadPrms.Append(CheckPeriod(HttpUtility.ParseQueryString(Request.QueryString.Value)), toPascalCase: true);
				SetSqlQueryParams(loadPrms);

				RequestModel rm = await RequestModel.CreateFromUrl(_codeProvider, kind, path);
				RequestView rw = rm.GetCurrentAction(kind);
				await Render(rw, loadPrms);
			} 
			catch (Exception ex)
			{
				if (ex.Message.StartsWith("UI:"))
				{
					var error = Localize(ex.Message.Substring(3));
					WriteExceptionStatus(ex);
				}
				else
				{
					await WriteHtmlException(ex);
				}
			}
		}

		internal async Task<DataModelAndView> GetDataModelForView(RequestView rw, ExpandoObject loadPrms)
		{
			var dmv = new DataModelAndView()
			{
				RequestView = rw
			};
			String loadProc = rw.LoadProcedure;
			IDataModel model = null;
			if (rw.parameters != null && loadPrms == null)
				loadPrms = rw.parameters;
			if (loadPrms != null)
			{
				loadPrms.AppendIfNotExists(rw.parameters);
				if (rw.Id != null)
					loadPrms.Set("Id", rw.Id);
			}
			if (loadProc != null)
			{
				ExpandoObject prms2 = loadPrms;
				if (rw.indirect)
				{
					// for indirect - @TenantId, @UserId and @Id only
					prms2 = new ExpandoObject();
					prms2.Set("Id", rw.Id);
					if (loadPrms != null)
					{
						prms2.Set("UserId", loadPrms.Get<Int64>("UserId"));
						prms2.Set("TenantId", loadPrms.Get<Int32>("TenantId"));
					}
				}
				model = await _dbContext.LoadModelAsync(rw.CurrentSource, loadProc, prms2);
				if (rw.HasMerge)
				{
					var mergeModel = await _dbContext.LoadModelAsync(rw.MergeSource, rw.MergeLoadProcedure, prms2);
					model.Merge(mergeModel);
				}
				if (rw.copy)
					model.MakeCopy();
				if (!String.IsNullOrEmpty(rw.Id) && !rw.copy)
				{
					var me = model.MainElement;
					if (me.Metadata != null)
					{
						var modelId = me.Id ?? String.Empty;
						if (rw.Id != modelId.ToString())
							throw new RequestModelException($"Element not found. Id={rw.Id}");
					}
				}
			}
			if (rw.indirect)
				rw = await LoadIndirect(rw, model, loadPrms);
			if (model?.Root != null)
			{
				// side effect!
				rw.view = model.Root.Resolve(rw.view);
				rw.viewMobile = model.Root.Resolve(rw.viewMobile);
				rw.template = model.Root.Resolve(rw.template);
				rw.checkTypes = model.Root.Resolve(rw.checkTypes);
			}

			if (_userStateManager != null && model != null)
			{
				Int64 userId = loadPrms.Get<Int64>("UserId");
				if (_userStateManager.IsReadOnly(userId))
					model.SetReadOnly();
			}
			dmv.Model = model;
			dmv.RequestView = rw;
			return dmv;
		}

		async Task Render(RequestView rwArg, ExpandoObject loadPrms)
		{
			var dmAndView = await GetDataModelForView(rwArg, loadPrms);

			IDataModel model = dmAndView.Model;
			var rw = dmAndView.RequestView;

			String rootId = "el" + Guid.NewGuid().ToString();

			var typeChecker = _host.CheckTypes(rw.Path, rw.checkTypes, model);

			var msi = new ModelScriptInfo()
			{
				DataModel = model,
				RootId = rootId,
				IsDialog = rw.IsDialog,
				Template = rw.template,
				Path = rw.Path,
				BaseUrl = rw.ParentModel.BasePath
			};
			var si = await _scripter.GetModelScript(msi);

			String modelScript = si.Script;
			// TODO: use view engines
			// try xaml
			String fileName = rw.GetView(_host.Mobile) + ".xaml";
			String basePath = rw.ParentModel.BasePath;

			String filePath = _codeProvider.MakeFullPath(rw.Path, fileName);

			Boolean bRendered = false;
			if (_codeProvider.FileExists(filePath))
			{
				// render XAML
				using (var strWriter = new StringWriter())
				{
					var ri = new RenderInfo()
					{
						RootId = rootId,
						FileName = filePath,
						FileTitle = fileName,
						Path = basePath,
						Writer = strWriter,
						DataModel = model,
						Localizer = _localizer,
						TypeChecker = typeChecker,
						CurrentLocale = null,
						IsDebugConfiguration = _host.IsDebugConfiguration,
						SecondPhase = secondPhase
					};
					_renderer.Render(ri);
					// write markup
					writer.Write(strWriter.ToString());
					bRendered = true;
				}
			}
			else
			{
				// try html
				fileName = rw.GetView(_host.Mobile) + ".html";
				filePath = _codeProvider.MakeFullPath(rw.Path, fileName);
				if (_host_codeProvider.FileExists(filePath))
				{
					using (_host.Profiler.CurrentRequest.Start(ProfileAction.Render, $"render: {fileName}"))
					{
						using (var tr = new StreamReader(filePath))
						{
							String htmlText = await tr.ReadToEndAsync();
							htmlText = htmlText.Replace("$(RootId)", rootId);
							htmlText = _localizer.Localize(null, htmlText, false);
							writer.Write(htmlText);
							bRendered = true;
						}
					}
				}
			}
			if (!bRendered)
			{
				throw new RequestModelException($"The view '{rw.GetView(_host.Mobile)}' was not found. The following locations were searched:\n{rw.GetRelativePath(".xaml", _host.Mobile)}\n{rw.GetRelativePath(".html", _host.Mobile)}");
			}
			await ProcessDbEvents(rw);
			writer.Write(modelScript);
		}
	}
}
