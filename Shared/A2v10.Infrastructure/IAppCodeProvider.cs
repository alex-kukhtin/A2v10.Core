// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace A2v10.Infrastructure
{
	public interface IAppCodeProvider
	{
		String MakeFullPath(String path, String fileName);

		Boolean FileExists(String fullPath);
		Boolean DirectoryExists(String fullPath);

		Task<String> ReadTextFileAsync(String path, String fileName);
		String ReadTextFile(String path, String fileName);

		Stream FileStreamFullPathRO(String fullPath);
		String FileReadAllText(String fullPath);
		IEnumerable<String> FileReadAllLines(String fullPath);

		IEnumerable<String> EnumerateFiles(String path, String searchPattern);

		// replaces
		String ReplaceFileName(String baseFullName, String relativeName);
		String GetExtension(String fullName);
		String ChangeExtension(String fullName, String extension);

		// new
		String MapHostingPath(String path);
	}
}
