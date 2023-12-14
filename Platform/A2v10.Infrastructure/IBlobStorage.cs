// Copyright © 2015-2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;

namespace A2v10.Infrastructure;
public interface IBlobStorage
{
	Task LoadAsync(String blobName);
	Task SaveAsync(String source, IBlobUpdateInfo blobInfo);
}

