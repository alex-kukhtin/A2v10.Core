// Copyright © 2021 Oleksandr Kukhtin. All rights reserved.

namespace A2v10.Web.Identity;

public sealed class IdentityCoreException : Exception
{
    public IdentityCoreException(String message)
        : base(message) { 
    }
}
