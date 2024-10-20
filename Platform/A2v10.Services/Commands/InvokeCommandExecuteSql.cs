// Copyright © 2020-2023 Oleksandr Kukhtin. All rights reserved.

using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

using A2v10.Data.Interfaces;

namespace A2v10.Services;

public class InvokeCommandExecuteSql(IDbContext dbContext) : IModelInvokeCommand
{
	private readonly IDbContext _dbContext = dbContext;

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

		ExpandoObject? signal = null;
		if (command.Signal && model != null && model.Root != null)
		{
			signal = model.Root.Get<ExpandoObject>("Signal");
			model.Root.Set("Signal", null);
		}

		var result = new InvokeResult(
			body: strResult != null ? Encoding.UTF8.GetBytes(strResult) : [],
			contentType: MimeTypes.Application.Json
		);
		if (signal != null)
			result.Signal = SignalResult.FromData(signal);	
		return result;
	}
}
