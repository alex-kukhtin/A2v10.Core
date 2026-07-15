// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

namespace A2v10.Services.Api;

public sealed record IndexQuery
{
    public Int32 Take { get; init; }
    public Int32 Skip { get; init; }
    public String? Sort { get; init; }  
    public Boolean Desc { get; init; }
}

