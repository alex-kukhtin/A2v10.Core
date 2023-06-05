// Copyright © 2020-2023 Oleksandr Kukhtin. All rights reserved.

using System.Text;

using Newtonsoft.Json;

namespace A2v10.Services;

public record SignalResult : ISignalResult
{
	public static ISignalResult FromExpando(ExpandoObject data)
	{
		return new SignalResult()
		{
			UserId = data.Get<Int64>("userId"),
			Message = data.Get<String>("message") ?? "Signal",
			Data = data.Get<ExpandoObject>("data")	
		};
	}
	public static ISignalResult FromData(ExpandoObject data)
	{
		return new SignalResult()
		{
			UserId = data.Get<Int64>("User"),
			Message = data.Get<String>("Message") ?? "Signal",
			Data = data.Get<ExpandoObject>("Data")
		};
	}
	public Int64 UserId { get; init; }

	public String Message { get; init; } = "Signal";

	public ExpandoObject? Data {get; init; }
}
public record InvokeResult : IInvokeResult
{
    public InvokeResult(Byte[] body, String contentType, String? fileName = null)
    {
        Body = body;
        ContentType = contentType;
        FileName = fileName;
    }

    public Byte[] Body { get; }
	public String ContentType { get; }
	public String? FileName { get; }
	public ISignalResult? Signal { get; set; }
	public static InvokeResult JsonFromObject(Object obj)
	{
		var json = JsonConvert.SerializeObject(obj, JsonHelpers.CompactSerializerSettings);
		return new InvokeResult(
			body: Encoding.UTF8.GetBytes(json),
			contentType: MimeTypes.Application.Json
		);
	}
}

