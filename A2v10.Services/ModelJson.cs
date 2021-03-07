﻿using System;
using System.Collections.Generic;
using System.Dynamic;
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

		internal virtual void SetParent(ModelJson rm)
		{
			_parent = rm;
		}

		public String DataSource => String.IsNullOrEmpty(Source) ? _parent.Source : Source;
		public String CurrentModel => String.IsNullOrEmpty(Model) ? _parent.Model : Model;
		public String CurrentSchema => String.IsNullOrEmpty(Schema) ? _parent.Schema : Schema;

		public Boolean HasModel() => !String.IsNullOrEmpty(CurrentModel);

		public virtual String LoadProcedure()
		{
			var cm = CurrentModel;
			if (String.IsNullOrEmpty(cm))
				throw new ModelJsonException("The model is empty (Load))");
			return $"[{CurrentSchema}].[{cm}.Load]";
		}

		public String Path => _parent.LocalPath;
		public String BaseUrl => _parent.BaseUrl;
	}

	public class RequestBase : ModelJsonBase
	{
		#region JSON
		public Boolean Index { get; set; }
		public Boolean Copy { get; set; }

		public ModelJsonBase Merge { get; set; }

		public ExpandoObject Parameters { get; set; }
		#endregion

		internal override void SetParent(ModelJson rm)
		{
			base.SetParent(rm);
			Merge?.SetParent(rm);
		}
	}

	public class ModelJsonBlob : RequestBase, IModelBlob
	{
		public String Key { get; init; }
		public String Id { get; init; }
		public String Suffix { get; init; }

		public override String LoadProcedure()
		{
			var suffix = Suffix ?? "Load";
			return $"[{Schema}].[{Model}.{Key}.{suffix}]";
		}
	}

	public class ModelJsonView : RequestBase, IModelView
	{
		// explicit
		IModelBase IModelView.Merge => Merge;
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

	public class ModelJsonCommand : ModelJsonBase
	{

	}

	public enum ModelJsonReportType
	{
		stimulsoft,
		xml,
		json
	}

	public class ModelJsonReport : ModelJsonBase
	{
		public ModelJsonReportType Type { get; set; }
		public String Report { get; set; }
		public String Procedure { get; set; }
		public String Name { get; set; }
		public String Encoding { get; set; }
		public Boolean Validate { get; set; }

		public String ReportProcedure()
		{
			var cm = CurrentModel;
			if (String.IsNullOrEmpty(cm))
				return null;
			return $"[{CurrentSchema}].[{cm}.Report]";
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
			throw new ModelJsonException($"Dialog: {key} not found");
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
