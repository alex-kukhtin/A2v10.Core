// Copyright © 2015-2023 Oleksandr Kukhtin. All rights reserved.


using Microsoft.Extensions.DependencyInjection;

using A2v10.Data.Interfaces;

namespace A2v10.Services;
public class ModelJsonBase : IModelBase
{
	protected ModelJson? _parent;
	protected ModelJson Parent => _parent ?? throw new ModelJsonException("Parent is null");

	public String? Source { get; init; }
	public String? Schema { get; init; }
	public String? Model { get; init; }	
	public Boolean Signal { get; init; }

	public Int32 CommandTimeout { get; init; }

	public ExpandoObject? Parameters { get; set; }

	internal virtual void SetParent(ModelJson rm)
	{
		_parent = rm;
	}

	public String? DataSource => String.IsNullOrEmpty(Source) ? Parent?.Source : Source;
	public String? CurrentModel => String.IsNullOrEmpty(Model) ? Parent.Model : Model;
	public String CurrentSchema => (String.IsNullOrEmpty(Schema) ? Parent.Schema : Schema) ?? "dbo";

	public Boolean HasModel() => Model != String.Empty && !String.IsNullOrEmpty(CurrentModel);

	public virtual String LoadProcedure()
	{
		var cm = CurrentModel;
		if (String.IsNullOrEmpty(cm))
			throw new ModelJsonException("The model is empty (Load))");
		return $"[{CurrentSchema}].[{cm}.Load]";
	}
    public virtual String UpdateProcedure()
    {
        var cm = CurrentModel;
        if (String.IsNullOrEmpty(cm))
            throw new ModelJsonException("The model is empty (Update))");
        return $"[{CurrentSchema}].[{cm}.Update]";
    }

    public String Path => Parent.LocalPath;
	public String BaseUrl => Parent.BaseUrl;

	public virtual ExpandoObject CreateParameters(IPlatformUrl url, Object? id,  Action<ExpandoObject>? setParams = null, IModelBase.ParametersFlags flags = IModelBase.ParametersFlags.None)
	{
		// model.json, query, id, system
		var eo = new ExpandoObject();
		if (!flags.HasFlag(IModelBase.ParametersFlags.SkipModelJsonParams) && Parameters != null)
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

	public Boolean SkipDataStack { get; set; }
	public Boolean Plain { get; set; }
	public Boolean Copy { get; set; }

	public ModelJsonMerge? Merge { get; set; }
	#endregion

	internal override void SetParent(ModelJson rm)
	{
		base.SetParent(rm);
		Merge?.SetParent(rm);
	}
}

public class ModelJsonBlob : ModelJsonViewBase, IModelBlob
{
	public String? Key { get; init; }
	public String? Id => _parent?.Id;
	public String? Suffix { get; init; }
    public String? OutputFileName { get; init; }
    public String? ClrType { get; init; }
    public Boolean Zip { get; init; }
    public ModelBlobType Type { get; init; }
	public ModelParseType Parse { get; init; }

	public override String LoadProcedure()
	{
		var strSuffix = Suffix ?? "Load";
		var strKey = Key != null ? $"{Key}." : String.Empty;
		return $"[{CurrentSchema}].[{CurrentModel}.{strKey}{strSuffix}]";
	}

    public override String UpdateProcedure()
    {
        var strSuffix = Suffix ?? "Update";
        var strKey = Key != null ? $"{Key}." : String.Empty;
        return $"[{CurrentSchema}].[{CurrentModel}.{strKey}{strSuffix}]";
    }
}

public class ModelJsonView : ModelJsonViewBase, IModelView
{
	// explicit
	IModelMerge? IModelView.Merge => Merge;
	IModelView? IModelView.TargetModel => TargetModel;

	public String? View { get; set; }
	public String? ViewMobile { get; set; }
	public String? Template { get; set; }
	public String? CheckTypes { get; set; }

	public virtual Boolean IsDialog => false;
	public Boolean IsIndex => Index;
	public Boolean IsSkipDataStack => SkipDataStack;
	public Boolean IsPlain => Plain;
	public Boolean Indirect { get; init; }
	public String? Target { get; init; }
	public String? TargetId { get; init; }
	public ModelJsonView? TargetModel { get; }

	public List<String>? Scripts { get; init; } 
	public List<String>? Styles { get; init; }

	public String? SqlTextKey()
	{
		var cm = CurrentModel;
		if (cm == null)
			return null;
		if (cm.StartsWith("@sql:"))
			return cm[5..];
		return null;
	}
	public String GetView(Boolean mobile)
	{
		if (mobile && !String.IsNullOrEmpty(ViewMobile))
			return ViewMobile;
		return View ?? throw new InvalidProgramException("View not defined");
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

	public String DeleteProcedure(String? propName)
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

	public override String UpdateProcedure()
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
	invokeTarget,
	// new
	csharp,
	signal
}

public class ModelJsonCommand : ModelJsonBase, IModelCommand
{
	public ModelCommandType Type { get; init; }
	public String? Procedure { get; init; }
	public String? File { get; init; }
	public String? ClrType { get; init; }
	public Boolean Async { get; init; }
	public Boolean DebugOnly { get; init; } /*TODO: Implement me*/
	public ExpandoObject? Args { get; init; }
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
	public String? Target { get; init; }
}

public class ModelJsonReport : ModelJsonBase, IModelReport
{
	public String? Type { get; init; }
	public String? Report { get; init; }
	public String? Procedure { get; init; }
	public String? Name { get; init; }
	public String? Encoding { get; init; }
	public Boolean Validate { get; init; }

	public ExpandoObject? Variables { get; init; }

	public override String LoadProcedure()
	{
		var cm = CurrentModel;
		if (String.IsNullOrEmpty(cm))
			throw new ModelJsonException("The model is empty (Report))");
		return $"[{CurrentSchema}].[{cm}.Report]";
	}

	public IModelReportHandler GetReportHandler(IServiceProvider serviceProvider)
	{
		var provider = serviceProvider.GetRequiredService<IReportEngineProvider>();

		// default report type is "stimulsoft" (DotNet Framework compatibility);
		return new ServerReport(provider.FindReportEngine(Type ?? "stimulsoft"),
			serviceProvider.GetRequiredService<IDbContext>()
		);
	}

	readonly String[] ExcludeParams = new String[] { "Rep", "Base", "Format" };

	public ExpandoObject CreateParameters(ExpandoObject? query, Action<ExpandoObject> setParams)
	{
		ExpandoObject prms = new();
		prms.Append(Parameters);
		prms.Append(query, ExcludeParams);
		setParams?.Invoke(prms);
		return prms;
	}

	public ExpandoObject CreateVariables(ExpandoObject? query, Action<ExpandoObject> setParams)
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
	private String? _localPath;
	private String? _baseUrl;
	private String? _id;

	internal String? Id => _id;

	#region JSON
	public String? Source { get; init; }
	public String? Schema { get; init; }
	public String? Model { get; init; }

	public Dictionary<String, ModelJsonView> Actions { get; init; } = new(StringComparer.OrdinalIgnoreCase);
	public Dictionary<String, ModelJsonDialog> Dialogs { get; init; } = new(StringComparer.OrdinalIgnoreCase);
	public Dictionary<String, ModelJsonView> Popups { get; init;  } = new(StringComparer.OrdinalIgnoreCase);
	public Dictionary<String, ModelJsonBlob> Files { get; init; } = new(StringComparer.OrdinalIgnoreCase);
	public Dictionary<String, ModelJsonCommand> Commands { get; init; } = new(StringComparer.OrdinalIgnoreCase);
	public Dictionary<String, ModelJsonReport> Reports { get; init; } = new(StringComparer.OrdinalIgnoreCase);
	#endregion

	public String LocalPath => _localPath ?? throw new InvalidProgramException("LocalPath is null");

	public String BaseUrl => _baseUrl ?? throw new InvalidProgramException("BaseUrl is null");

	public ModelJsonView? TryGetAction(String key)
	{
		if (Actions.TryGetValue(key ?? "index", out ModelJsonView? view))
			return view;
		return null;
	}

	public ModelJsonView GetAction(String key)
	{
		var view = TryGetAction(key);
		return view ?? throw new ModelJsonException($"Action: {key} not found");
	}

	public ModelJsonDialog GetDialog(String key)
	{
		if (Dialogs.TryGetValue(key, out ModelJsonDialog? view))
			return view;
		throw new ModelJsonException($"Dialog: {key} not found");
	}

	public ModelJsonReport GetReport(String key)
	{
		if (Reports.TryGetValue(key, out ModelJsonReport? report))
			return report;
		throw new ModelJsonException($"Report: {key} not found");
	}

	public ModelJsonCommand GetCommand(String key)
	{
		if (Commands.TryGetValue(key, out ModelJsonCommand? command))
			return command;
		throw new ModelJsonException($"Command: {key} not found");
	}

	public ModelJsonBlob GetBlob(String key, String? suffix = null)
	{
		var blob = new ModelJsonBlob()
		{
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
		if (!Files.TryGetValue(key, out ModelJsonBlob? file))
			throw new ModelJsonException($"File: {key} not found");
		return file;
	}

	public ModelJsonView GetPopup(String key)
	{
		if (Popups.TryGetValue(key, out ModelJsonView? view))
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

