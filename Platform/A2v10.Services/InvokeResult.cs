// Copyright © 2020-2021 Alex Kukhtin. All rights reserved.

using System;
using System.Text;

using Newtonsoft.Json;

using A2v10.Infrastructure;

namespace A2v10.Services
{
	public record InvokeResult : IInvokeResult
	{
		public Byte[] Body { get; init; }
		public String ContentType { get; init; }
		public String FileName { get; init; }

		public static InvokeResult JsonFromObject(Object obj)
		{
			var json = JsonConvert.SerializeObject(obj, JsonHelpers.CompactSerializerSettings);
			return new InvokeResult()
			{
				ContentType = MimeTypes.Application.Json,
				Body = Encoding.UTF8.GetBytes(json)
			};
		}
	}
}
