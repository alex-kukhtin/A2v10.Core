// Copyright © 2015-2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;

namespace A2v10.Infrastructure;
public interface IBlobStorage
{
	Task<ReadOnlyMemory<Byte>> LoadAsync(String? source, String? container, String blobName);
	Task SaveAsync(String? source, String? container, IBlobUpdateInfo blobInfo);
	Task DeleteAsync(String? source, String? container, String blobName);
}

