/*
Copyright © 2026 Oleksandr Kukhtin

Last updated : 19 sep 2026
module version : 8663
*/
------------------------------------------------
set nocount on;

if not exists(select * from INFORMATION_SCHEMA.SCHEMATA where SCHEMA_NAME=N'a2meta')
	exec sp_executesql N'create schema a2meta authorization dbo';
go
------------------------------------------------
alter authorization on schema::a2meta to dbo;
go
------------------------------------------------
grant execute on schema ::a2meta to public;
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2meta' and TABLE_NAME=N'SysParams')
create table a2meta.SysParams
(
	[name] sysname,
	[value] sysname,
	constraint PK_SysParams primary key ([name])
);
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2meta' and TABLE_NAME=N'Tables')
create table a2meta.Tables
(
	[schema] sysname,
	[table] sysname,
	[xtra] nvarchar(64), /* fingerprint of everything about this table that is NOT its schema -
	                        today the declared values of an enum. The seed is what the deploy hash
	                        is taken from, so a change here is what makes a changed declaration
	                        reach the database at all. */
	/* The table this one is PART of, and the column here that links back to it. Null for a table
	   that hangs under nobody, which is most of them. */
	[master_schema] nvarchar(128),
	[master_table] nvarchar(128),
	[master_column] nvarchar(128),
	constraint PK_Tables primary key ([schema], [table])
);
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.COLUMNS where TABLE_SCHEMA = N'a2meta' and TABLE_NAME = N'Tables' and COLUMN_NAME = N'xtra')
	alter table a2meta.Tables add [xtra] nvarchar(64) null;
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.COLUMNS where TABLE_SCHEMA = N'a2meta' and TABLE_NAME = N'Tables' and COLUMN_NAME = N'master_table')
	alter table a2meta.Tables add [master_schema] nvarchar(128) null,
		[master_table] nvarchar(128) null, [master_column] nvarchar(128) null;
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2meta' and TABLE_NAME=N'Columns')
create table a2meta.Columns
(
	[schema] sysname,
	[table] sysname,
	[column] sysname,
	[datatype] sysname,      /* isnull(DOMAIN_NAME, DATA_TYPE) */
	[length] int,            /* CHARACTER_MAXIMUM_LENGTH: in characters, -1 means max */
	[precision] tinyint,     /* NUMERIC_PRECISION */
	[scale] tinyint,         /* NUMERIC_SCALE */
	[nullable] bit,          /* IS_NULLABLE */
	[ref_schema] nvarchar(128), /* foreign key target. Not sysname: sysname is nvarchar(128) NOT NULL */
	[ref_table] nvarchar(128),
	[default] nvarchar(128), /* default value for add column; takes no part in comparison.
	                            The constraint name is not stored - it is always DF_{table}_{column} */
	constraint PK_Columns primary key ([schema], [table], [column])
);
go
------------------------------------------------
create or alter function a2meta.fn_getUtcDate()
returns datetime as
begin
	declare @offset int = datediff(minute, getdate(), getutcdate());
	declare @date datetime = cast(cast(getutcdate() as date) as datetime);
	return dateadd(minute, @offset, @date);
end
go
------------------------------------------------
create or alter procedure a2meta.[GetDbHash]
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;
	select [Hash] = [value] from a2meta.SysParams where [name] = 'dbhash';
end
go
------------------------------------------------
create or alter procedure a2meta.[SetDbHash]
@Hash sysname
as
begin
	set nocount on;
	set transaction isolation level read committed;

	merge a2meta.SysParams t 
	using (select [name] = 'dbhash', [hash] = @Hash) s
	on t.[name] = s.[name]
	when matched then update set 
		t.[value] = s.[hash]
	when not matched then insert ([name], [value]) 
	values (s.[name], s.[hash]);

end
go
------------------------------------------------
create or alter procedure a2meta.[GetPlatformIdType]
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;
	/* the base the platformid alias rests on. The database is the fact here, not a
	   declaration: it answers with the type the alias was actually created from.
	   No rows means the alias does not exist yet - that is an error, not a default. */
	select [DataType] = DATA_TYPE from INFORMATION_SCHEMA.DOMAINS where DOMAIN_NAME = N'platformid';
end
go
------------------------------------------------
create or alter procedure a2meta.[GetFkReferrers]
@Schema sysname,
@Table sysname
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	/* Aliased to the PROPERTY names of TableReferrer: the list loader matches them ordinally, so
	   a lower-case one fills nothing and the row arrives empty instead of failing.
	   left, not inner - a missing Tables row must not DROP the referrer, which would be a
	   permitted delete of something already referenced. */
	select [Schema] = c.[schema], [Table] = c.[table], [Column] = c.[column],
		[MasterSchema] = t.[master_schema], [MasterTable] = t.[master_table],
		[MasterColumn] = t.[master_column]
	from a2meta.Columns c
		left join a2meta.Tables t on t.[schema] = c.[schema] and t.[table] = c.[table]
	where c.ref_schema = @Schema and c.ref_table = @Table and c.datatype = N'platformid'
	and c.[schema] not in (N'jrn', N'rep');
end
go
------------------------------------------------
create or alter procedure a2meta.[SyncSchema]
as
begin
	set nocount on;
	set transaction isolation level read committed;
	set xact_abort on;

	/* What the declaration says the schema is, against what the database has. a2meta.Columns is the
	   DESIRED state and not the previous one: the metadata seed is the first batch of the deploy
	   script, and its 'not matched by source then delete' makes that table exactly the declaration.
	   INFORMATION_SCHEMA is the fact. So a row missing there is a column to add, and this procedure
	   needs no parameters and keeps no state of its own.

	   Stage 1 adds columns and does nothing else: a changed type, a dropped column and the indexes
	   are not its business yet.

	   No transaction around the walk. 'add column' is additive and autonomous - a run that fails
	   halfway leaves what it added and the next one adds the rest - while one transaction over all
	   of it would hold schema locks for the whole deploy and disagree with the rest of the script,
	   where every batch stands alone. */

	declare @alters table([schema] sysname, [table] sysname, [stmt] nvarchar(max));

	/* The type is rendered here from the three facets, by the same fork SqlDbTypeInfo.SqlFullName
	   has: whichever facet is present decides the spelling. Not a stored full name - a fact enters
	   the seed only when SQL must answer it for the whole application at once, and this one follows
	   from facts already lying there. The price, named: the fork now lives in two languages, and a
	   divergence shows up as wrong DDL on this path alone.

	   Nullability and the default are one decision, as they are in the declaration: a default is the
	   only road to NOT NULL (DeployNullable), so a not-null add always carries one. The two columns
	   that carry none - Id and Master - can reach here only on a table that is not supposed to gain
	   them, and the ALTER then fails on a non-empty table. Deliberately not filtered out: silence
	   there is the very bug this procedure exists to end. The constraint name is never stored - it
	   is always DF_{table}_{column}, the name CreateTable writes.

	   The join to TABLES is not for the deploy, where create table has already run over the same
	   walk: it is what lets anyone exec this procedure on its own without an ALTER on a table that
	   is not there. */

	insert into @alters([schema], [table], [stmt])
	select d.[schema], d.[table],
		N'alter table [' + d.[schema] + N'].[' + d.[table] + N'] add ' +
			string_agg(d.[def], N', ') within group (order by d.[column])
	from (
		select c.[schema], c.[table], c.[column],
			[def] = cast(N'[' + c.[column] + N'] ' + c.[datatype] +
				case
					when c.[length] = -1 then N'(max)'
					when c.[length] is not null then N'(' + cast(c.[length] as nvarchar(16)) + N')'
					when c.[precision] is not null then
						N'(' + cast(c.[precision] as nvarchar(8)) + N', ' + cast(c.[scale] as nvarchar(8)) + N')'
					else N''
				end +
				case when c.[nullable] = 1 then N'' else N' not null' end +
				case when c.[default] is null then N''
					else N' constraint DF_' + c.[table] + N'_' + c.[column] + N' default(' + c.[default] + N')'
				end as nvarchar(max))
		from a2meta.Columns c
			inner join INFORMATION_SCHEMA.TABLES t on t.TABLE_SCHEMA = c.[schema] and t.TABLE_NAME = c.[table]
				and t.TABLE_TYPE = N'BASE TABLE'
		where not exists(select * from INFORMATION_SCHEMA.COLUMNS ic
			where ic.TABLE_SCHEMA = c.[schema] and ic.TABLE_NAME = c.[table] and ic.COLUMN_NAME = c.[column])
	) d
	group by d.[schema], d.[table];

	/* Printed and not returned: the deploy runs this through ExecuteNonQuery and would drop a result
	   set, while deploydatabase.sql is an artifact applied by hand at the customer site - that
	   reader is the one a print has. */

	declare @stmt nvarchar(max);
	declare #crs cursor local fast_forward read_only for
		select [stmt] from @alters order by [schema], [table];
	open #crs;
	fetch next from #crs into @stmt;
	while @@fetch_status = 0
	begin
		print @stmt;
		exec sp_executesql @stmt;
		fetch next from #crs into @stmt;
	end
	close #crs;
	deallocate #crs;
end
go
