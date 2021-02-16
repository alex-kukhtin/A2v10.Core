
using System;

using A2v10.Infrastructure;

namespace A2v10.Web.Services
{
	public class DataModelService
	{
		private readonly IDbContext _dbContext;
		private readonly IAppCodeProvider _codeProvider;

		public DataModelService(IDbContext dbContext, IAppCodeProvider codeProvider)
		{
			_dbContext = dbContext ?? throw new ArgumentNullException(nameof(IDbContext));
			_codeProvider = codeProvider ?? throw new ArgumentNullException(nameof(IAppCodeProvider));
		}
	}
}
