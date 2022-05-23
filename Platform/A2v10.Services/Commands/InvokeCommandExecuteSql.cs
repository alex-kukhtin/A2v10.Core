// Copyright © 2020-2022 Alex Kukhtin. All rights reserved.

using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

using A2v10.Data.Interfaces;

namespace A2v10.Services;

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

		var result = new InvokeResult(
			body: strResult != null ? Encoding.UTF8.GetBytes(strResult) : Array.Empty<Byte>(),
			contentType: MimeTypes.Application.Json
		);
		return result;
	}
}
