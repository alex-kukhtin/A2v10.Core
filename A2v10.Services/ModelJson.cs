using System;
using System.Collections.Generic;
using System.Dynamic;

using A2v10.Infrastructure;

namespace A2v10.Services
{

	public class RequestData : IModelBase
	{
		private ModelJson _parent;

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
				throw new DataServiceException("The model is empty (Load))");
			return $"[{CurrentSchema}].[{cm}.Load]";
		}

		public String Path => _parent.LocalPath;
		public String BaseUrl => _parent.BaseUrl;
	}

	public class RequestBase : RequestData
	{
		#region JSON
		public Boolean Index { get; set; }
		public Boolean Copy { get; set; }

		public RequestData Merge { get; set; }

		public ExpandoObject Parameters { get; set; }
		#endregion


		internal override void SetParent(ModelJson rm)
		{
			base.SetParent(rm);
			Merge?.SetParent(rm);
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

		public override String LoadProcedure()
		{
			var cm = CurrentModel;
			String action = Index ? "Index" : Copy ? "Copy" : "Load";
			if (String.IsNullOrEmpty(cm))
				throw new DataServiceException($"The model is empty ({action})");
			return $"[{CurrentSchema}].[{cm}.{action}]";
		}

		public String ExpandProcedure()
		{
			var cm = CurrentModel;
			if (String.IsNullOrEmpty(cm))
				throw new DataServiceException("The model is empty (Expand)");
			return $"[{CurrentSchema}].[{cm}.Expand]";
		}

		public String LoadLazyProcedure(String propName)
		{
			var cm = CurrentModel;
			if (String.IsNullOrEmpty(cm))
				throw new DataServiceException($"The model is empty (LoadLazy.{propName})");
			return $"[{CurrentSchema}].[{cm}.{propName}]";
		}

		public String UpdateProcedure()
		{
			if (Index)
				throw new DataServiceException($"Could not update index model '{CurrentModel}'");
			var cm = CurrentModel;
			if (String.IsNullOrEmpty(cm))
				throw new DataServiceException("The model is empty (Update)");
			return $"[{CurrentSchema}].[{cm}.Update]";
		}
	}

	public class ModelJsonDialog : ModelJsonView
	{
		public override Boolean IsDialog => true;
	}

	public class ModelJson
	{
		private String _localPath;
		private String _baseUrl;

		#region JSON
		public String Source { get; set; }
		public String Schema { get; set; }
		public String Model { get; set; }

		public Dictionary<String, ModelJsonView> Actions = new Dictionary<string, ModelJsonView>(StringComparer.OrdinalIgnoreCase);
		public Dictionary<String, ModelJsonDialog> Dialogs = new Dictionary<string, ModelJsonDialog>(StringComparer.OrdinalIgnoreCase);
		public Dictionary<String, ModelJsonView> Popups = new Dictionary<string, ModelJsonView>(StringComparer.OrdinalIgnoreCase);

		#endregion

		public String LocalPath => _localPath;
		public String BaseUrl => _baseUrl;

		public ModelJsonView GetAction(String key)
		{
			key ??= "index";
			if (Actions.TryGetValue(key, out ModelJsonView view))
				return view;
			throw new KeyNotFoundException($"Action: {key} not found");
		}

		public ModelJsonDialog GetDialog(String key)
		{
			if (Dialogs.TryGetValue(key, out ModelJsonDialog view))
				return view;
			throw new KeyNotFoundException($"Dialog: {key} not found");
		}

		public ModelJsonView GetPopup(String key)
		{
			if (Popups.TryGetValue(key, out ModelJsonView view))
				return view;
			throw new KeyNotFoundException($"Popup: {key} not found");
		}

		public void OnEndInit(IPlatformUrl url)
		{
			_localPath = url.LocalPath;
			_baseUrl = url.BaseUrl;
			foreach (var (_, v) in Actions)
				v.SetParent(this);
			foreach (var (_, v) in Dialogs)
				v.SetParent(this);
			foreach (var (_, v) in Popups)
				v.SetParent(this);
		}
	}
}
