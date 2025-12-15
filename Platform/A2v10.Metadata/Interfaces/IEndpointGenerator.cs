// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System.Threading.Tasks;

namespace A2v10.Metadata;

internal interface IEndpointGenerator
{
    Task BuildEndpointAsync(TableMetadata table);
}
