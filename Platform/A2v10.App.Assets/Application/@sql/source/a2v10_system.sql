/*
Copyright © 2008-2023 Oleksandr Kukhtin

Last updated : 24 may 2023
module version : 8100
*/
------------------------------------------------
create or alter procedure a2sys.[AppTitle.Load]
as
begin
	set nocount on;
	select [AppTitle], [AppSubTitle]
	from (select [Name], [Value] = StringValue from a2sys.SysParams) as s
		pivot (min(Value) for [Name] in ([AppTitle], [AppSubTitle])) as p;
end
go

------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.DOMAINS where DOMAIN_SCHEMA=N'a2sys' and DOMAIN_NAME=N'Id.TableType' and DATA_TYPE=N'table type')
create type a2sys.[Id.TableType] as table(
	Id bigint null
);
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.DOMAINS where DOMAIN_SCHEMA=N'a2sys' and DOMAIN_NAME=N'GUID.TableType' and DATA_TYPE=N'table type')
create type a2sys.[GUID.TableType] as table(
	Id uniqueidentifier null
);
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.DOMAINS where DOMAIN_SCHEMA=N'a2sys' and DOMAIN_NAME=N'NameValue.TableType' and DATA_TYPE=N'table type')
create type a2sys.[NameValue.TableType] as table(
	[Name] nvarchar(255),
	[Value] nvarchar(max)
);
go

