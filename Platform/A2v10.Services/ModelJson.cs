// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Dynamic;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Services
{
	public class ModelJsonBase : IModelBase
	{
		protected ModelJson _parent;

		public String Source;
		public String Schema;
		public String Model;

		public Int32 CommandTimeout;

		public ExpandoObject Parameters { get; set; }

		internal virtual void SetParent(ModelJson rm)
		{
			_parent = rm;
		}

		public String DataSource => String.IsNullOrEmpty(Source) ? _parent.Source : Source;
		public String CurrentModel => String.IsNullOrEmpty(Model) ? _parent.Model : Model;
		public String CurrentSchema => String.IsNullOrEmpty(Schema) ? _parent.Schema : Schema;

		public Boolean HasModel() => Model != String.Empty && !String.IsNullOrEmpty(CurrentModel);

		public virtual String LoadProcedure()
		{
			var cm = CurrentModel;
			if (String.IsNullOrEmpty(cm))
				throw new ModelJsonException("The model is empty (Load))");
			return $"[{CurrentSchema}].[{cm}.Load]";
		}

		public String Path => _parent.LocalPath;
		public String BaseUrl => _parent.BaseUrl;

		public virtual ExpandoObject CreateParameters(IPlatformUrl url, Object id,  Action<ExpandoObject> setParams = null, IModelBase.ParametersFlags flags = IModelBase.ParametersFlags.None)
		{
			// model.json, query, id, system
			var eo = new ExpandoObject();
			if (!flags.HasFlag(IModelBase.ParametersFlags.SkipModelJsonParams))
				eo.Append(Parameters);
			eo.Append(url.Query);
			if (!flags.HasFlag(IModelBase.ParametersFlags.SkipId))
			{
				eo.SetNotNull("Id", url.Id);
				eo.SetNotNull("Id", id);
			}
			setParams?.Invoke(eo);
			return eo;
		}
	}

	public class ModelJsonMerge : ModelJsonBase, IModelMerge
	{
		public ExpandoObject CreateMergeParameters(IDataModel model, ExpandoObject prms)
		{
			if (Parameters == null || Parameters.IsEmpty())
				return prms;
			var result = prms.Clone();
			foreach (var (k, v) in Parameters)
			{
				if (v is String strVal && strVal.StartsWith("{{", StringComparison.Ordinal))
					result.Set(k, model.Root.Resolve(strVal));
				else
					result.Set(k, v);
			}
			return result;
		}
	}

	public class ModelJsonViewBase : ModelJsonBase
	{
		#region JSON
		public Boolean Index { get; set; }
		public Boolean Copy { get; set; }

		public ModelJsonMerge Merge { get; set; }
		#endregion

		internal override void SetParent(ModelJson rm)
		{
			base.SetParent(rm);
			Merge?.SetParent(rm);
		}
	}

	public class ModelJsonBlob : ModelJsonViewBase, IModelBlob
	{
		public String Key { get; init; }
		public String Id { get; init; }
		public String Suffix { get; init; }
		public String Procedure { get; init; }

		public override String LoadProcedure()
		{
			if (!String.IsNullOrEmpty(Procedure))
				return Procedure;
			var suffix = Suffix ?? "Load";
			return $"[{Schema}].[{Model}.{Key}.{suffix}]";
		}
	}

	public class ModelJsonView : ModelJsonViewBase, IModelView
	{
		// explicit
		IModelMerge IModelView.Merge => Merge;
		IModelView IModelView.TargetModel => TargetModel;

		public String View { get; set; }
		public String ViewMobile { get; set; }
		public String Template { get; set; }
		public String CheckTypes { get; set; }

		public virtual Boolean IsDialog => false;

		public Boolean Indirect { get; set; }
		public String Target { get; set; }
		public String TargetId { get; set; }
		public ModelJsonView TargetModel { get; }

		public String GetView(Boolean mobile)
		{
			if (mobile && !String.IsNullOrEmpty(ViewMobile))
				return ViewMobile;
			return View;
		}

		public IModelView Resolve(IDataModel model)
		{
			if (model == null || model.Root == null)
				return this;
			View = model.Root.Resolve(View);
			ViewMobile = model.Root.Resolve(ViewMobile);
			Template = model.Root.Resolve(Template);
			CheckTypes = model.Root.Resolve(CheckTypes);
			return this;
		}

		public override String LoadProcedure()
		{
			var cm = CurrentModel;
			String action = Index ? "Index" : Copy ? "Copy" : "Load";
			if (String.IsNullOrEmpty(cm))
				throw new ModelJsonException($"The model is empty ({action})");
			return $"[{CurrentSchema}].[{cm}.{action}]";
		}

		public String DeleteProcedure(String propName)
		{
			var cm = CurrentModel;
			if (!String.IsNullOrEmpty(propName))
				propName = "." + propName;
			return $"[{CurrentSchema}].[{cm}{propName}.Delete]";
		}

		public String ExpandProcedure()
		{
			var cm = CurrentModel;
			if (String.IsNullOrEmpty(cm))
				throw new ModelJsonException("The model is empty (Expand)");
			return $"[{CurrentSchema}].[{cm}.Expand]";
		}

		public String LoadLazyProcedure(String propName)
		{
			var cm = CurrentModel;
			if (String.IsNullOrEmpty(cm))
				throw new ModelJsonException($"The model is empty (LoadLazy.{propName})");
			return $"[{CurrentSchema}].[{cm}.{propName}]";
		}

		public String UpdateProcedure()
		{
			if (Index)
				throw new ModelJsonException($"Could not update index model '{CurrentModel}'");
			var cm = CurrentModel;
			if (String.IsNullOrEmpty(cm))
				throw new ModelJsonException("The model is empty (Update)");
			return $"[{CurrentSchema}].[{cm}.Update]";
		}
	}

	public class ModelJsonDialog : ModelJsonView
	{
		public override Boolean IsDialog => true;
	}

	public class ModelJsonFile : ModelJsonBase
	{
	}

	public enum ModelCommandType
	{
		none,
		sql,
		clr,
		javascript,
		xml,
		file,
		callApi,
		sendMessage,
		processDbEvents,
		startProcess,
		resumeProcess,
		script,
		invokeTarget
	}

	public class ModelJsonCommand : ModelJsonBase, IModelCommand
	{
		public ModelCommandType Type { get; init; }
		public String Procedure { get; set; }
		public String File { get; set; }
		public String ClrType { get; set; }
		public Boolean Async { get; set; }
		public Boolean DebugOnly { get; set; } /*TODO: Implement me*/

		public override String LoadProcedure()
		{
			if (String.IsNullOrEmpty(Procedure))
				throw new DataServiceException("A procedure must be specified for sql-type command");
			return $"[{CurrentSchema}].[{Procedure}]";
		}

		public IModelInvokeCommand GetCommandHandler(IServiceProvider serviceProvider)
		{
			return ServerCommandRegistry.GetCommand(Type, serviceProvider);
		}
		public String Target { get; init; }
	}

	public class ModelJsonReport : ModelJsonBase, IModelReport
	{
		public String Type { get; set; }
		public String Report { get; set; }
		public String Procedure { get; set; }
		public String Name { get; set; }
		public String Encoding { get; set; }
		public Boolean Validate { get; set; }

		public ExpandoObject Variables { get; set; }

		public override String LoadProcedure()
		{
			var cm = CurrentModel;
			if (String.IsNullOrEmpty(cm))
				return null;
			return $"[{CurrentSchema}].[{cm}.Report]";
		}

		public IModelReportHandler GetReportHandler(IServiceProvider serviceProvider)
		{
			var provider = serviceProvider.GetService<IReportEngineProvider>();

			// default report type is "stimulsoft" (DotNet Framework compatibility);
			return new ServerReport(provider.FindReportEngine(Type ?? "stimulsoft"),
				serviceProvider.GetService<IDbContext>()
			);
		}

		readonly String[] ExcludeParams = new String[] { "Rep", "Base", "Format" };

		public ExpandoObject CreateParameters(ExpandoObject query, Action<ExpandoObject> setParams)
		{
			ExpandoObject prms = new();
			prms.Append(Parameters);
			prms.Append(query, ExcludeParams);
			setParams?.Invoke(prms);
			return prms;
		}

		public ExpandoObject CreateVariables(ExpandoObject query, Action<ExpandoObject> setParams)
		{
			ExpandoObject vars = new();
			vars.Append(Variables);
			vars.Append(Parameters);
			vars.Append(query, ExcludeParams);
			setParams?.Invoke(vars);
			return vars;
		}
	}

	public class ModelJson
	{
		private String _localPath;
		private String _baseUrl;
		private String _id;

		#region JSON
		public String Source { get; set; }
		public String Schema { get; set; }
		public String Model { get; set; }

		public Dictionary<String, ModelJsonView> Actions = new(StringComparer.OrdinalIgnoreCase);
		public Dictionary<String, ModelJsonDialog> Dialogs = new(StringComparer.OrdinalIgnoreCase);
		public Dictionary<String, ModelJsonView> Popups = new(StringComparer.OrdinalIgnoreCase);
		public Dictionary<String, ModelJsonFile> Files = new(StringComparer.InvariantCultureIgnoreCase);
		public Dictionary<String, ModelJsonCommand> Commands = new(StringComparer.InvariantCultureIgnoreCase);
		public Dictionary<String, ModelJsonReport> Reports = new(StringComparer.InvariantCultureIgnoreCase);
		#endregion

		public String LocalPath => _localPath;
		public String BaseUrl => _baseUrl;

		public ModelJsonView GetAction(String key)
		{
			key ??= "index";
			if (Actions.TryGetValue(key, out ModelJsonView view))
				return view;
			throw new ModelJsonException($"Action: {key} not found");
		}

		public ModelJsonDialog GetDialog(String key)
		{
			if (Dialogs.TryGetValue(key, out ModelJsonDialog view))
				return view;
			throw new ModelJsonException($"Dialog: {key} not found");
		}

		public ModelJsonReport GetReport(String key)
		{
			if (Reports.TryGetValue(key, out ModelJsonReport report))
				return report;
			throw new ModelJsonException($"Report: {key} not found");
		}

		public ModelJsonCommand GetCommand(String key)
		{
			if (Commands.TryGetValue(key, out ModelJsonCommand command))
				return command;
			throw new ModelJsonException($"Command: {key} not found");
		}

		public ModelJsonBlob GetBlob(String key, String suffix = null)
		{
			var blob = new ModelJsonBlob()
			{
				Id = _id,
				Schema = this.Schema,
				Source = this.Source,
				Model = this.Model,
				Key = key.ToPascalCase(),
				Suffix = suffix
			};
			blob.SetParent(this);
			return blob;
		}

		public ModelJsonBlob GetFile(String key)
		{
			if (!Files.TryGetValue(key, out ModelJsonFile file))
				throw new ModelJsonException($"File: {key} not found");

			var blob = new ModelJsonBlob()
			{
				Id = _id,
				Schema = this.Schema,
				Source = this.Source,
				Procedure = file.LoadProcedure()
			};
			blob.SetParent(this);
			return blob;

		}

		public ModelJsonView GetPopup(String key)
		{
			if (Popups.TryGetValue(key, out ModelJsonView view))
				return view;
			throw new ModelJsonException($"Popup: {key} not found");
		}

		public void OnEndInit(IPlatformUrl url)
		{
			_localPath = url.LocalPath;
			_baseUrl = url.BaseUrl;
			_id = url.Id;
			foreach (var (_, v) in Actions)
				v.SetParent(this);
			foreach (var (_, v) in Dialogs)
				v.SetParent(this);
			foreach (var (_, v) in Popups)
				v.SetParent(this);
			foreach (var (_, f) in Files)
				f.SetParent(this);
			foreach (var (_, c) in Commands)
				c.SetParent(this);
			foreach (var (_, r) in Reports)
				r.SetParent(this);
		}
	}
}
