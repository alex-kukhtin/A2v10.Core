// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.Metadata;

/* A dimension of the boundary: the catalog app.json names (skill permissions.md, "Межа"), and the table
 * of the users' values in it - {catalog schema}.[{Model}$Boundary] (UserId, {Model}). Named as the tag
 * entries are, a table the platform adds to an owner; columns as UserRoles, each named for what it holds.
 * The Path is the dimension's key wherever one is needed - app.json checked it unique, a Model may repeat
 * across aliased folders.
 */
internal sealed record BoundaryDimension(String Path, TableMetadata Catalog)
{
    internal String Table => $"{Catalog.Model}$Boundary";
    internal String SqlTableName => $"{Catalog.SqlSchema}.[{Table}]";
    internal String Column => Catalog.Model;
}
