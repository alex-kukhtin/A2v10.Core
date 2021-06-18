// Copyright © 2021 Alex Kukhtin. All rights reserved.

using System;

namespace A2v10.Infrastructure
{
	public class RenderResult : IRenderResult
	{
		public String Body { get; init; }
		public String ContentType { get; init; }
	}
}
