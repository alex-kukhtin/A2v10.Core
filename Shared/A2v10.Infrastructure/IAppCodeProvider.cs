// Copyright © 2015-2020 Alex Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;

namespace A2v10.Infrastructure
{
	public interface IAppCodeProvider
	{
		String MakeFullPath(String path, String fileName);
		Task<String> ReadTextFileAsync(String path, String fileName);
	}
}
