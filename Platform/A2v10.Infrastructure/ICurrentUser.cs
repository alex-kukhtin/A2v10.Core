// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace A2v10.Infrastructure;
public interface IUserIdentity
{
    Int64? Id { get; }
    String? Name { get; }
    String? PersonName { get; }

    Int32? Tenant { get; }
    String? Segment { get; }

    Boolean IsAdmin { get; }
    Boolean IsTenantAdmin { get; }
}

public interface IUserState
{
    Int64? Company { get; }
    Boolean IsReadOnly { get; }
}

public interface IUserLocale
{
    String Locale { get; }
    String Language { get; }
}

public interface ICurrentUser
{
    public IUserIdentity Identity { get; }
    public IUserState State { get; }
    public IUserLocale Locale { get; }
    public Boolean IsAdminApplication { get; }

    void SetCompanyId(Int64 id);
    void SetReadOnly(Boolean readOnly);
}

