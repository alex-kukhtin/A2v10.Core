
using System;
using System.Dynamic;
using System.Threading.Tasks;
using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Core.Web.Mvc.Builders
{
	public class BaseBuilder
	{
		protected readonly IDbContext _dbContext;
		protected readonly IAppCodeProvider _codeProvider;

		public BaseBuilder(IDbContext dbContext, IAppCodeProvider codeProvider)
		{
			_dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
			_codeProvider = codeProvider ?? throw new ArgumentNullException(nameof(codeProvider));
		}

		public async Task<RequestView> LoadIndirect(RequestView rw, IDataModel innerModel, ExpandoObject loadPrms)
		{
			if (!rw.indirect)
				return rw;
			if (!String.IsNullOrEmpty(rw.target))
			{
				String targetUrl = innerModel.Root.Resolve(rw.target);
				if (String.IsNullOrEmpty(rw.targetId))
					throw new RequestModelException("targetId must be specified for indirect action");
				targetUrl += "/" + innerModel.Root.Resolve(rw.targetId);
				var rm = await RequestModel.CreateFromUrl(_codeProvider, rw.CurrentKind, targetUrl);
				rw = rm.GetCurrentAction();
				String loadProc = rw.LoadProcedure;
				if (loadProc != null)
				{
					loadPrms.Set("Id", rw.Id);
					if (rw.parameters != null)
						loadPrms.AppendIfNotExists(rw.parameters);
					var newModel = await _dbContext.LoadModelAsync(rw.CurrentSource, loadProc, loadPrms);
					innerModel.Merge(newModel);
					innerModel.System.Set("__indirectUrl__", rm.BaseUrl);
				}
			}
			else
			{
				// simple view/model redirect
				if (rw.targetModel == null)
				{
					throw new RequestModelException("'targetModel' must be specified for indirect action without 'target' property");

				}
				rw.model = innerModel.Root.Resolve(rw.targetModel.model);
				rw.view = innerModel.Root.Resolve(rw.targetModel.view);
				rw.viewMobile = innerModel.Root.Resolve(rw.targetModel.viewMobile);
				rw.schema = innerModel.Root.Resolve(rw.targetModel.schema);
				if (String.IsNullOrEmpty(rw.schema))
					rw.schema = null;
				rw.template = innerModel.Root.Resolve(rw.targetModel.template);
				if (String.IsNullOrEmpty(rw.template))
					rw.template = null;
				String loadProc = rw.LoadProcedure;
				if (loadProc != null)
				{
					loadPrms.Set("Id", rw.Id);
					var newModel = await _dbContext.LoadModelAsync(rw.CurrentSource, loadProc, loadPrms);
					innerModel.Merge(newModel);
				}
			}
			return rw;
		}
	}
}
