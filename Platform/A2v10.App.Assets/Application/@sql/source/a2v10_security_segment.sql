/*
Copyright © 2008-2023 Oleksandr Kukhtin

Last updated : 04 jul 2023
module version : 8110
*/

-- SECURITY SEGMENT
------------------------------------------------
create or alter procedure a2security.SetTenantId
@TenantId int
as
begin
	exec sp_set_session_context @key=N'TenantId', @value=@TenantId, @read_only=0;   
end
go
------------------------------------------------
create or alter procedure a2security.[User.CreateTenant]
@Id bigint,
@Tenant int,
@UserName nvarchar(255),
@Email nvarchar(255) = null,
@PhoneNumber nvarchar(255) = null,
@PersonName nvarchar(255) = null,
@Memo nvarchar(255) = null,
@Locale nvarchar(255) = null
as
begin
	set nocount on;
	set transaction isolation level read committed;
	set xact_abort on;

	begin tran;

	insert into a2security.Tenants (Id, [Admin], Locale)
	values (@Tenant, null, @Locale);

	insert into a2security.Users(Id, Tenant, UserName, PersonName, Email, EmailConfirmed, PhoneNumber,
		Locale, Memo, SecurityStamp, SecurityStamp2, PasswordHash, PasswordHash2)
	values (@Id, @Tenant, @UserName, @PersonName, @Email, 1, @PhoneNumber, 
		@Locale, @Memo, N'', N'', N'', N'');

	update a2security.Tenants set [Admin] = @Id where Id = @Tenant;

	-- INIT TENANT
	declare @tenants a2sys.[Id.TableType];
	insert into @tenants(Id) values (@Tenant);

	exec a2ui.[InvokeTenantInitProcedures] @Tenants = @tenants;

	commit tran;
end
go

------------------------------------------------
create or alter procedure a2security.[User.InviteComplete]
@Id bigint,
@Tenant int,
@UserName nvarchar(255),
@Email nvarchar(255) = null,
@PhoneNumber nvarchar(255) = null,
@PersonName nvarchar(255) = null,
@Memo nvarchar(255) = null,
@Locale nvarchar(255) = null
as
begin
	set nocount on;
	set transaction isolation level read committed;
	set xact_abort on;

	insert into a2security.Users(Id, Tenant, UserName, PersonName, Email, EmailConfirmed, PhoneNumber,
		Locale, Memo, SecurityStamp, SecurityStamp2, PasswordHash, PasswordHash2)
	values (@Id, @Tenant, @UserName, @PersonName, @Email, 1, @PhoneNumber, 
		@Locale, @Memo, N'', N'', N'', N'');
end
go
