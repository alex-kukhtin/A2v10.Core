﻿/*
Copyright © 2008-2023 Oleksandr Kukhtin

Last updated : 24 may 2023
module version : 8100
*/
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.SCHEMATA where SCHEMA_NAME=N'a2sys')
	exec sp_executesql N'create schema a2sys';
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.SCHEMATA where SCHEMA_NAME=N'a2security')
	exec sp_executesql N'create schema a2security';
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.SCHEMATA where SCHEMA_NAME=N'a2ui')
	exec sp_executesql N'create schema a2ui';
go
------------------------------------------------
grant execute on schema ::a2sys to public;
grant execute on schema ::a2security to public;
grant execute on schema ::a2ui to public;
go

------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2sys' and TABLE_NAME=N'SysParams')
create table a2sys.SysParams
(
	Name sysname not null constraint PK_SysParams primary key,
	StringValue nvarchar(255) null,
	IntValue int null,
	DateValue datetime null,
	GuidValue uniqueidentifier null
);
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.SEQUENCES where SEQUENCE_SCHEMA=N'a2security' and SEQUENCE_NAME=N'SQ_Tenants')
	create sequence a2security.SQ_Tenants as bigint start with 100 increment by 1;
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2security' and TABLE_NAME=N'Tenants')
create table a2security.Tenants
(
	Id	int not null constraint PK_Tenants primary key
		constraint DF_Tenants_PK default(next value for a2security.SQ_Tenants),
	[Admin] bigint null, -- admin user ID
	[Source] nvarchar(255) null,
	[TransactionCount] bigint not null constraint DF_Tenants_TransactionCount default(0),
	LastTransactionDate datetime null,
	UtcDateCreated datetime not null constraint DF_Tenants_UtcDateCreated default(getutcdate()),
	TrialPeriodExpired datetime null,
	DataSize float null,
	[State] nvarchar(128) null,
	UserSince datetime null,
	LastPaymentDate datetime null,
	[Locale] nvarchar(32) not null constraint DF_Tenants_Locale default('uk-UA')
);
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.SEQUENCES where SEQUENCE_SCHEMA=N'a2security' and SEQUENCE_NAME=N'SQ_Users')
	create sequence a2security.SQ_Users as bigint start with 100 increment by 1;
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2security' and TABLE_NAME=N'Users')
create table a2security.Users
(
	Id	bigint not null
		constraint DF_Users_PK default(next value for a2security.SQ_Users),
	Tenant int not null 
		constraint FK_Users_Tenant_Tenants foreign key references a2security.Tenants(Id),
	UserName nvarchar(255) not null constraint UNQ_Users_UserName unique,
	DomainUser nvarchar(255) null,
	Void bit not null constraint DF_Users_Void default(0),
	SecurityStamp nvarchar(max) not null,
	PasswordHash nvarchar(max) null,
	/*for .net core compatibility*/
	SecurityStamp2 nvarchar(max) null,
	PasswordHash2 nvarchar(max) null,
	TwoFactorEnabled bit not null constraint DF_Users_TwoFactorEnabled default(0),
	Email nvarchar(255) null,
	EmailConfirmed bit not null constraint DF_Users_EmailConfirmed default(0),
	PhoneNumber nvarchar(255) null,
	PhoneNumberConfirmed bit not null constraint DF_Users_PhoneNumberConfirmed default(0),
	LockoutEnabled	bit	not null constraint DF_Users_LockoutEnabled default(1),
	LockoutEndDateUtc datetimeoffset null,
	AccessFailedCount int not null constraint DF_Users_AccessFailedCount default(0),
	[Locale] nvarchar(32) not null constraint DF_Users_Locale2 default('uk-UA'),
	PersonName nvarchar(255) null,
	LastLoginDate datetime null, /*UTC*/
	LastLoginHost nvarchar(255) null,
	Memo nvarchar(255) null,
	ChangePasswordEnabled bit not null constraint DF_Users_ChangePasswordEnabled default(1),
	RegisterHost nvarchar(255) null,
	Segment nvarchar(32) null,
	SetPassword bit,
	UtcDateCreated datetime not null constraint DF_Users_UtcDateCreated default(getutcdate()),
	constraint PK_Users primary key (Tenant, Id)
);
go
------------------------------------------------
create or alter view a2security.ViewUsers
as
	select Id, UserName, DomainUser, PasswordHash, SecurityStamp, Email, PhoneNumber,
		LockoutEnabled, AccessFailedCount, LockoutEndDateUtc, TwoFactorEnabled, [Locale],
		PersonName, Memo, Void, LastLoginDate, LastLoginHost, Tenant, EmailConfirmed,
		PhoneNumberConfirmed, RegisterHost, ChangePasswordEnabled, Segment,
		SecurityStamp2, PasswordHash2, SetPassword
	from a2security.Users u
	where Void = 0 and Id <> 0;
go
------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2ui' and TABLE_NAME=N'Modules')
create table a2ui.Modules
(
	TenantId int not null,
	Id uniqueidentifier not null,
	Parent uniqueidentifier null,
	[Name] nvarchar(255),
	[Memo] nvarchar(255),
	constraint PK_Modules primary key (TenantId, Id),
	constraint FK_Modules_Parent_Modules foreign key (TenantId, Parent) references a2ui.Modules(TenantId, Id)
);
go

------------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2ui' and TABLE_NAME=N'Menu')
create table a2ui.Menu
(
	TenantId int not null,
	Id uniqueidentifier not null,
	Module uniqueidentifier not null,
	Parent uniqueidentifier,
	[Name] nvarchar(255),
	[Url] nvarchar(255),
	CreateName nvarchar(255),
	CreateUrl nvarchar(255),
	Icon nvarchar(255),
	[Order] int not null constraint DF_Menu_Order default(0),
	[ClassName] nvarchar(255) null,
	constraint PK_Menu primary key (TenantId, Id),
	constraint FK_Menu_Parent_Menu foreign key (TenantId, Parent) references a2ui.Menu(TenantId, Id),
	constraint FK_Menu_Module_Modules foreign key (TenantId, Module) references a2ui.Modules(TenantId, Id)
);
go