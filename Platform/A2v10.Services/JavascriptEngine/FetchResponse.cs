// Copyright © 2020-2021 Alex Kukhtin. All rights reserved.

using System.Net;

using Newtonsoft.Json;

namespace A2v10.Services.Javascript
{
	public class FetchResponse
	{
		internal FetchResponse(HttpStatusCode status, String? contentType, String? body, ExpandoObject? headers, String statusText = "OK")
		{
			this.status = status;
			this.contentType = contentType;
			this.body = body;
			this.statusText = statusText;
			this.headers = headers;
		}

#pragma warning disable IDE1006 // Naming Styles
		public Boolean ok => ((int)status >= 200) && ((int)status <= 299);
		public String statusText { get; }
		public HttpStatusCode status { get; }
		public String? contentType { get; }
		public String? body { get; }
		public Boolean isJson => contentType != null && contentType.StartsWith(MimeTypes.Application.Json);
		public ExpandoObject? headers { get; }
		public Object json()
		{
			if (isJson)
			{
				if (body == null)
					return new ExpandoObject();
				if (IsBodyArray())
                    return JsonConvert.DeserializeObject<List<ExpandoObject>>(body)
						?? new List<ExpandoObject>();
                return JsonConvert.DeserializeObject<ExpandoObject>(body) ?? new ExpandoObject();
			}
			throw new InvalidOperationException($"The answer is not in {MimeTypes.Application.Json} format");
		}

        private Boolean IsBodyArray()
        {
			if (body == null)
				return false;
            for (int i = 0; i < body.Length; i++)
            {
                if (body[i] == '[')
                    return true;
                else if (body[i] == '{')
                    return false;
            }
            return false;
        }

        public String text()
		{
			return body ?? String.Empty;
		}
#pragma warning restore IDE1006 // Naming Styles
    }
}
