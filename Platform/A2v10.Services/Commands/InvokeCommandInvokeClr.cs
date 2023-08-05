// Copyright © 2021-2023 Oleksandr Kukhtin. All rights reserved.

using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

namespace A2v10.Services;

public class InvokeCommandInvokeClr : IModelInvokeCommand
{
	private readonly IServiceProvider _serviceProvider;

	public InvokeCommandInvokeClr(IServiceProvider serviceProvider)
	{
		_serviceProvider = serviceProvider;
	}

	public async Task<IInvokeResult> ExecuteAsync(IModelCommand command, ExpandoObject parameters)
	{
		if (command.ClrType == null)
			throw new InvalidOperationException("Command.ClrType is null");
        try
		{
			var (assembly, clrType) = ClrHelpers.ParseClrType(command.ClrType);
			var ass = Assembly.Load(assembly);
			var tp = ass.GetType(clrType)
				?? throw new InvalidOperationException("Type not found");
			var ctor = tp.GetConstructor(new Type[] { typeof(IServiceProvider) })
				?? throw new InvalidOperationException($"ctor(IServiceProvider) not found in {clrType}");
			var elem = ctor.Invoke(new Object[] { _serviceProvider })
				?? throw new InvalidOperationException($"Unable to create element of {clrType}");
			if (elem is not IClrInvokeTarget invokeTarget)
				throw new InvalidOperationException($"The type '{clrType}' must implement the interface IClrInvokeTarget");
			var invokeResult = await invokeTarget.InvokeAsync(parameters);
			if (invokeResult is ExpandoObject eo)
			{
				var body = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(invokeResult,
					JsonHelpers.DataSerializerSettings));
				return new InvokeResult(body, MimeTypes.Application.Json);
			}
			else if (invokeResult is InvokeBlobResult blobResult)
                return new InvokeResult(
					blobResult.Stream ?? Array.Empty<Byte>(), 
					blobResult.Mime ?? MimeTypes.Application.Json
				);
			else if (invokeResult != null)
			{
                var body = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(invokeResult,
                    JsonHelpers.DataSerializerSettings));
                return new InvokeResult(body, MimeTypes.Application.Json);
            }

            return new InvokeResult(Encoding.UTF8.GetBytes("{}"), MimeTypes.Application.Json);
		}
		catch (Exception ex)
		{
			throw new InvalidOperationException(ex.Message, ex);
		}
	}
}
