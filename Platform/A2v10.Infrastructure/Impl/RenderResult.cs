// Copyright © 2021 Alex Kukhtin. All rights reserved.

using System;

namespace A2v10.Infrastructure
{
	public class RenderResult : IRenderResult
	{
		public RenderResult(String body, String contentType)
        {
			Body = body;
			ContentType = contentType;
        }

		public String Body { get;}
		public String ContentType { get;}
	}
}
