using A2v10.Infrastructure;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2v10.Services
{

	public class RequestBase
	{
		private ModelJson _parent;

		public String Source;
		public String Schema;
		public String Model;

		public Boolean Index;
		public Boolean Copy;

		public Int32 CommandTimeout;

		public ExpandoObject Parameters { get; set; }

		internal void SetParent(ModelJson rm)
		{
			_parent = rm;
		}

		public String DataSource => String.IsNullOrEmpty(Source) ? _parent.Source : Source;
		public String CurrentModel => String.IsNullOrEmpty(Model) ? _parent.Model : Model;
		public String CurrentSchema => String.IsNullOrEmpty(Schema) ? _parent.Schema : Schema;
	}

	public class ModelJsonView : RequestBase, IModelView
	{
		public String LoadProcedure()
		{
			var cm = CurrentModel;
			String action = Index ? "Index" : Copy ? "Copy" : "Load";
			if (String.IsNullOrEmpty(cm))
				throw new DataServiceException($"The model is empty (Load.{action})");
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

	public class ModelJson
	{
		public String Source;
		public String Schema;
		public String Model;

		public Dictionary<String, ModelJsonView> Actions = new Dictionary<string, ModelJsonView>(StringComparer.OrdinalIgnoreCase);
		public Dictionary<String, ModelJsonView> Dialogs = new Dictionary<string, ModelJsonView>(StringComparer.OrdinalIgnoreCase);
		public Dictionary<String, ModelJsonView> Popups = new Dictionary<string, ModelJsonView>(StringComparer.OrdinalIgnoreCase);

		public ModelJsonView GetAction(String key)
		{
			key ??= "index";
			if (Actions.TryGetValue(key, out ModelJsonView view))
				return view;
			throw new KeyNotFoundException($"Action: {key} not found");
		}

		public void OnEndInit()
		{
			foreach (var (_, v) in Actions)
				v.SetParent(this);
			foreach (var (_, v) in Dialogs)
				v.SetParent(this);
			foreach (var (_, v) in Popups)
				v.SetParent(this);
		}
	}
}
