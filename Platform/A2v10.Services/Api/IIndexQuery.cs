// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

namespace A2v10.Services.Api;

public sealed record IndexQuery
{
    public Int32 Take { get; init; }
    public Int32 Skip { get; init; }
    public String? Sort { get; init; }  
    // null - the index's own order, as a screen opens; a caller that sorts says the direction
    public Boolean? Desc { get; init; }
}

