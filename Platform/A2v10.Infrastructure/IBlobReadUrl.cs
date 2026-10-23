// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.Infrastructure;

// A read-only link to a blob for an outside service, valid for lifetime; implemented by a storage
// that can sign one (Azure: SAS) and registered by AddBlobStorages under the storage's singleton
public interface IBlobReadUrl
{
	Uri GetReadUrl(String? source, String? container, String blobName, TimeSpan lifetime);
}
