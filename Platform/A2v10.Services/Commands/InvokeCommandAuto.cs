// Copyright © 2020-2024 Oleksandr Kukhtin. All rights reserved.

using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

using Microsoft.Extensions.DependencyInjection;

namespace A2v10.Services;

public class InvokeCommandAuto(IServiceProvider _serviceProvider) : IModelInvokeCommand
{
	private readonly IAppRuntimeBuilder _runtimeBuilder = _serviceProvider.GetRequiredService<IAppRuntimeBuilder>();
    public async Task<IInvokeResult> ExecuteAsync(IModelCommand command, ExpandoObject parameters)
	{
		var model = await _runtimeBuilder.ExecuteCommandAsync(command, parameters);

		var strResult = model != null && model.Root != null ?
			JsonConvert.SerializeObject(model.Root, JsonHelpers.DataSerializerSettings) : "{}";

		var result = new InvokeResult(
			body: strResult != null ? Encoding.UTF8.GetBytes(strResult) : [],
			contentType: MimeTypes.Application.Json
		);
		return result;
	}
}
