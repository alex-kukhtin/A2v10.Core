// Copyright © 2020-2021 Alex Kukhtin. All rights reserved.

using System.Dynamic;
using System.Threading.Tasks;
using Newtonsoft.Json;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;
using System.Text;

namespace A2v10.Services
{
	public class InvokeCommandExecuteSql : IModelInvokeCommand
	{
		private readonly IDbContext _dbContext;

		public InvokeCommandExecuteSql(IDbContext dbContext)
		{
			_dbContext = dbContext;
		}

		public async Task<IInvokeResult> ExecuteAsync(IModelCommand command, ExpandoObject parameters)
		{
			var model = await _dbContext.LoadModelAsync(command.DataSource, command.LoadProcedure(), parameters);

			/*_host.CheckTypes(cmd.Path, cmd.checkTypes, model);
			String invokeTarget = command.GetInvokeTarget();
			if (invokeTarget != null)
			{
				var clr = new ClrInvoker();
				clr.EnableThrow();
				clr.Invoke(invokeTarget, dataToExec); // after execute
			}
			*/
			var strResult = model != null && model.Root != null ?
				JsonConvert.SerializeObject(model.Root, JsonHelpers.DataSerializerSettings) : "{}";

			var result = new InvokeResult()
			{
				Body = strResult != null ? Encoding.UTF8.GetBytes(strResult) : null,
				ContentType = MimeTypes.Application.Json
			};
			return result;
		}
	}
}
