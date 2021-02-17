
using System;
using System.Dynamic;
using System.Threading.Tasks;
using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Web.Services
{
	public class DataModelService
	{
		private readonly IDbContext _dbContext;
		private readonly IAppCodeProvider _codeProvider;
		private readonly IModelDefinitionService _model;

		public DataModelService(IModelDefinitionService _model, IDbContext dbContext, IAppCodeProvider codeProvider)
		{
			_dbContext = dbContext ?? throw new ArgumentNullException(nameof(IDbContext));
			_codeProvider = codeProvider ?? throw new ArgumentNullException(nameof(IAppCodeProvider));
			_model = _model ?? throw new ArgumentNullException(nameof(IModelDefinitionService));

		}

		async Task LoadModel(String baseUrl)
		{
			var rw = await _model.CreateFromBaseUrlAsync(baseUrl);
			var loadProc = rw.LoadProcedure;
			if (loadProc == null)
				throw new Exception("TODO:");
			var prms2 = new ExpandoObject();
			IDataModel model = await _dbContext.LoadModelAsync(rw.CurrentSource, loadProc, prms2);
		}
	}
}
