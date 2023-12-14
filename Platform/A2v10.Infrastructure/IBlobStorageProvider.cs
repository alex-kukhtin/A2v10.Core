// Copyright © 2021-2023 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.Infrastructure;

public interface IBlobStorageProvider
{
	IBlobStorage FindBlobStorage(String name);
}
