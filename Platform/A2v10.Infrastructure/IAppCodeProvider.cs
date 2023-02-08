// Copyright © 2015-2023 Alex Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace A2v10.Infrastructure;
public interface IAppCodeProvider
{
	Boolean IsFileSystem { get; }

	String MakeFullPath(String path, String fileName, Boolean admin);
	Task<String?> ReadTextFileAsync(String path, String fileName, Boolean admin);
	String? ReadTextFile(String path, String fileName, Boolean admin);
	Boolean FileExists(String fullPath);
	Boolean DirectoryExists(String fullPath);
	Stream FileStreamFullPathRO(String fullPath);

	IEnumerable<String> EnumerateFiles(String? path, String searchPattern, Boolean admin);

	// replaces
	String ReplaceFileName(String baseFullName, String relativeName);
	String GetExtension(String fullName);
	String ChangeExtension(String fullName, String extension);
}

