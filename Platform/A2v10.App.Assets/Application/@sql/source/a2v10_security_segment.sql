/*
Copyright © 2008-2023 Oleksandr Kukhtin

Last updated : 05 aug 2023
module version : 8186
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
create or alter procedure a2security.[User.Invite]
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

------------------------------------------------
create or alter procedure a2security.[User.SetEMailConfirmed]
@Id bigint,
@Confirmed bit
as
begin
	set nocount on;
	set transaction isolation level read committed;

	update a2security.ViewUsers set EmailConfirmed = @Confirmed
	where Id=@Id;
end
go
------------------------------------------------
create or alter procedure a2security.[User.SetPhoneNumberConfirmed]
@Id bigint,
@PhoneNumber nvarchar(255),
@Confirmed bit
as
begin
	set nocount on;
	set transaction isolation level read committed;
	update a2security.ViewUsers set PhoneNumber = @PhoneNumber, PhoneNumberConfirmed = @Confirmed where Id = @Id;
end
go
------------------------------------------------
create or alter procedure a2security.[User.UpdateParts]
@Id bigint,
@PhoneNumber nvarchar(255) = null,
@PersonName nvarchar(255) = null,
@EmailConfirmed bit = null,
@FirstName nvarchar(255) = null,
@LastName nvarchar(255) = null,
@Locale nvarchar(32) = null
as
begin
	set nocount on;
	set transaction isolation level read committed;

	update a2security.Users set 
		PhoneNumber = isnull(@PhoneNumber, PhoneNumber),
		PersonName = isnull(@PersonName, PersonName),
		EmailConfirmed = isnull(@EmailConfirmed, EmailConfirmed),
		Locale = isnull(@Locale, Locale)
	where Id = @Id;
end
go

------------------------------------------------
create or alter procedure a2security.[User.Tenant.CreateApiUser]
@TenantId int = 1,
@UserName nvarchar(255) = null,
@PersonName nvarchar(255) = null,
@Memo nvarchar(255) = null,
@Locale nvarchar(255) = null
as
begin
	set nocount on;
	set transaction isolation level read committed;


	set @TenantId = isnull(@TenantId, 1);
	set @Locale = isnull(@Locale, N'');

	insert into a2security.Users(Tenant, IsApiUser, UserName, PersonName, Memo, Segment, Locale, SecurityStamp, SecurityStamp2)
	select @TenantId, 1, @UserName, @PersonName, @Memo, N'', @Locale, N'', N'';

end
go
------------------------------------------------
create or alter procedure a2security.[User.Tenant.DeleteApiUser]
@TenantId int = 1,
@UserId bigint = null,
@Id bigint
as
begin
	set nocount on;
	set transaction isolation level read committed;

	update a2security.Users set Void = 1 where Tenant = @TenantId and Id = @Id;
end
go
------------------------------------------------
create or alter procedure a2security.[User.Tenant.DeleteUser]
@TenantId int = 1,
@UserId bigint = null,
@Id bigint,
@UserName nvarchar(255) = null,
@Email nvarchar(255) = null,
@PhoneNumber nvarchar(255) = null,
@DomainUser nvarchar(255) = null
as
begin
	set nocount on;
	set transaction isolation level read committed;

	update a2security.Users set Void = 1, UserName = @UserName, @Email = @Email, PhoneNumber = @PhoneNumber,
		DomainUser = @DomainUser, EmailConfirmed = 0, PhoneNumberConfirmed = 0
	where Tenant = @TenantId and Id = @Id;

end
go
------------------------------------------------
create or alter procedure a2security.[User.Tenant.EditUser]
@TenantId int = 1,
@Id bigint,
@PersonName nvarchar(255) = null,
@PhoneNumber nvarchar(255) = null,
@Memo nvarchar(255) = null
as
begin
	set nocount on;
	set transaction isolation level read committed;

	update a2security.Users set PersonName = @PersonName, Memo = @Memo, PhoneNumber = @PhoneNumber
		where Tenant = @TenantId and Id = @Id;
end
go
