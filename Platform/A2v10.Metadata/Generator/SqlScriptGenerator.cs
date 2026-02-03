// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.IO;
using System.Threading.Tasks;

namespace A2v10.Metadata;

internal class SqlScriptGenerator(AppMetadata _metadata, String _metaPath)
{
    public async Task GenerateSqlScriptsAsync()
    {
        if (!Directory.Exists(_metaPath))
            Directory.CreateDirectory(_metaPath);
        await GenerateTables();
        await GenerateForeignKeys();
        await GenerateMetdataInit();
    }   

    Task GenerateTables()
    {
        foreach (var table in _metadata.Tables)
        {
        }
        String filePath = Path.Combine(_metaPath, "_tables.sql"); 
        return Task.CompletedTask;
    }

    Task GenerateForeignKeys()
    {
        String filePath = Path.Combine(_metaPath, "_foreignkeys.sql");
        return Task.CompletedTask;
    }

    Task GenerateMetdataInit()
    {
        // TABLES / COLUMNS
        String filePath = Path.Combine(_metaPath, "_metadata_init.sql");
        var sql = $"""
         -- METADATA INIT
         -- TABLES
         begin
         set nocount on;
         declare @tables table([Id] uniqueidentifier, [Parent] uniqueidentifier, [ParentTable] uniqueidentifier,
            IsFolder bit, [Order] int null, [Schema] nvarchar(32),  [Name] nvarchar(128), [Kind] nvarchar(32), ItemsName nvarchar(128),
            ItemName nvarchar(128), TypeName nvarchar(128), EditWith nvarchar(16),ItemsLabel nvarchar(255),
            ItemLabel nvarchar(128),UseFolders bit, FolderMode nvarchar(16),[Type] nvarchar(32)
         );

         -- insert into @tables values

         merge a2meta.[Catalog] as t
         using @tables as s
         on t.Id = s.Id
         when matched then update set
            t.Parent = s.Parent,
            t.ParentTable = s.ParentTable,
            t.IsFolder = s.IsFolder,
            t.[Order] = s.[Order],
            t.[Schema] = s.[Schema],
            t.[Name] = s.[Name],
            t.[Kind] = s.[Kind],
            t.ItemsName = s.ItemsName,
            t.ItemName = s.ItemName,
            t.TypeName = s.TypeName,
            t.EditWith = s.EditWith,
            t.ItemsLabel = s.ItemsLabel,
            t.ItemLabel = s.ItemLabel,
            t.UseFolders = s.UseFolders,
            t.FolderMode = s.FolderMode,
            t.[Type] = s.[Type]
         when not matched then
            insert (Id, Parent, ParentTable, IsFolder, [Order], [Schema], [Name], [Kind], ItemsName, ItemName, TypeName, EditWith, ItemsLabel, ItemLabel, UseFolders, FolderMode, [Type])
            values (s.Id, s.Parent, s.ParentTable, s.IsFolder, s.[Order], s.[Schema], s.[Name], s.[Kind], s.ItemsName, s.ItemName, s.TypeName, s.EditWith, s.ItemsLabel, s.ItemLabel, s.UseFolders, s.FolderMode, s.[Type]);
         end
         go
         """;
        return File.WriteAllTextAsync(filePath, sql);
    }
}
