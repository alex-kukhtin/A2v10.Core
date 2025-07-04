﻿/*
Copyright © 2008-2025 Oleksandr Kukhtin

Last updated : 04 jun 2025
module version : 8553
*/

-- SECURITY
------------------------------------------------
create or alter procedure a2security.FindUserById
@Id bigint
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	select * from a2security.ViewUsers where Id=@Id;
end
go
------------------------------------------------
create or alter procedure a2security.FindUserByEmail
@Email nvarchar(255)
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	select * from a2security.ViewUsers where Email=@Email;
end
go
------------------------------------------------
create or alter procedure a2security.FindUserByName
@UserName nvarchar(255)
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	select * from a2security.ViewUsers where UserName=@UserName;
end
go
------------------------------------------------
create or alter procedure a2security.FindUserByPhoneNumber
@PhoneNumber nvarchar(255)
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	select * from a2security.ViewUsers where PhoneNumber=@PhoneNumber;
end
go
------------------------------------------------
create or alter procedure a2security.UpdateUserLogin
@Id bigint,
@LastLoginHost nvarchar(255)
as
begin
	set nocount on;
	set transaction isolation level read committed;

	update a2security.ViewUsers set LastLoginDate = getutcdate(), LastLoginHost = @LastLoginHost 
	where Id = @Id;
end
go
------------------------------------------------
create or alter procedure a2security.[User.SetSetPassword]
@Id bigint,
@Set bit = 0
as
begin
	set nocount on;
	set transaction isolation level read committed;

	update a2security.ViewUsers set SetPassword = @Set where Id = @Id;
end
go
------------------------------------------------
create or alter procedure a2security.[User.SetAccessFailedCount]
@Id bigint,
@AccessFailedCount int
as
begin
	set nocount on;
	set transaction isolation level read committed;

	update a2security.ViewUsers set AccessFailedCount = @AccessFailedCount
	where Id=@Id;
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
create or alter procedure a2security.[User.SetLockoutEndDate]
@Id bigint,
@LockoutEndDate datetimeoffset
as
begin
	set nocount on;
	set transaction isolation level read committed;

	update a2security.ViewUsers set LockoutEndDateUtc = @LockoutEndDate where Id=@Id;
end
go

------------------------------------------------
create or alter procedure a2security.[User.SetPasswordHash]
@Id bigint,
@PasswordHash nvarchar(max)
as
begin
	set nocount on;
	set transaction isolation level read committed;

	update a2security.ViewUsers set PasswordHash2 = @PasswordHash where Id = @Id;
end
go
------------------------------------------------
create or alter procedure a2security.[User.SetSecurityStamp]
@Id bigint,
@SecurityStamp nvarchar(max)
as
begin
	set nocount on;
	set transaction isolation level read committed;

	update a2security.ViewUsers set SecurityStamp2 = @SecurityStamp where Id = @Id;
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
create or alter procedure a2security.FindApiUserByApiKey
@Host nvarchar(255) = null,
@ApiKey nvarchar(1023) = null
as
begin
	set nocount on;
	set transaction isolation level read committed;

	declare @status nvarchar(255);
	declare @code int;

	set @status = N'ApiKey=' + @ApiKey;
	set @code = 65; /*fail*/

	declare @user table(Id bigint, [Name] nvarchar(255), ClientId nvarchar(255), AllowIP nvarchar(255), Locale nvarchar(32));
	insert into @user(Id, [Name], ClientId, AllowIP, Locale)
	select top(1) u.Id, [Name]=u.UserName, s.ClientId, s.AllowIP, u.Locale 
	from a2security.Users u inner join a2security.ApiUserLogins s on u.Id = s.[User]
	where u.Void=0 and s.Mode = N'ApiKey' and s.ApiKey=@ApiKey;
	
	if @@rowcount > 0 
	begin
		set @code = 64 /*sucess*/;
		update a2security.Users set LastLoginDate=getutcdate(), LastLoginHost=@Host
		  from @user t inner join a2security.Users u on t.Id = u.Id;
	end

	--insert into a2security.[Log] (UserId, Severity, Code, Host, [Message])
		--values (0, N'I', @code, @Host, @status);

	select * from @user;
end
go
------------------------------------------------
create or alter procedure a2security.FindUserByExternalLogin
@LoginProvider nvarchar(255),
@ProviderKey nvarchar(1024)
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	declare @userId bigint;
	select @userId = [User] from a2security.ExternalUserLogins e
	where LoginProvider=@LoginProvider and ProviderKey = @ProviderKey;

	update a2security.Users set LastLoginDate = getutcdate() where Id = @userId;

	select u.* from a2security.ViewUsers u
	where Id = @userId
end
go
------------------------------------------------
create or alter procedure a2security.[User.AddExternalLogin]
@Id bigint,
@LoginProvider nvarchar(255),
@ProviderKey nvarchar(1024)
as
begin
	set nocount on;
	set transaction isolation level read committed;
	set xact_abort on;

	begin tran;
	delete from a2security.ExternalUserLogins where [User] = @Id and LoginProvider = @LoginProvider;

	insert into a2security.ExternalUserLogins([User], LoginProvider, ProviderKey)
	values (@Id, @LoginProvider, @ProviderKey);
	commit tran;
end
go
------------------------------------------------
create or alter procedure a2security.CreateUser 
@UserName nvarchar(255),
@PasswordHash nvarchar(max) = null,
@SecurityStamp nvarchar(max),
@Email nvarchar(255) = null,
@PhoneNumber nvarchar(255) = null,
@PersonName nvarchar(255) = null,
@Memo nvarchar(255) = null,
@Locale nvarchar(255) = null,
@RetId bigint output
as
begin
	set nocount on;
	set transaction isolation level read committed;
	set xact_abort on;

	set @Locale = isnull(@Locale, N'uk-UA');

	declare @tenants table(Id int);
	declare @users table(Id bigint);
	declare @userId bigint;
	declare @tenantCreated bit = 0;

	begin tran;

	insert into a2security.Users(UserName, PersonName, Email, PhoneNumber, SecurityStamp, PasswordHash,
		Locale, Memo)
	output inserted.Id into @users(Id)
	values (@UserName, @PersonName, @Email, @PhoneNumber, @SecurityStamp, @PasswordHash, 
		@Locale, @Memo);
	select top(1) @userId = Id from @users;
	set @RetId = @userId;
	commit tran;
end
go
------------------------------------------------
create or alter procedure a2security.[User.RegisterComplete]
@Id bigint
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	update a2security.Users set EmailConfirmed = 1, LastLoginDate = getutcdate()
	where Id = @Id;

	select * from a2security.ViewUsers where Id = @Id;
end
go
------------------------------------------------
create or alter procedure a2security.GetUserGroups
@UserId bigint
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;
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
create or alter procedure a2security.[User.SetAuthenticatorKey]
@Id bigint,
@AuthenticatorKey nvarchar(64)
as
begin
	set nocount on;
	set transaction isolation level read committed;

	update a2security.Users set AuthenticatorKey = @AuthenticatorKey  where Id = @Id;
end
go
------------------------------------------------
create or alter procedure a2security.[User.SetTwoFactorEnabled]
@Id bigint,
@TwoFactorEnabled bit
as
begin
	set nocount on;
	set transaction isolation level read committed;

	if @TwoFactorEnabled = 1
		update a2security.Users set TwoFactorEnabled = 1 where Id = @Id;
	else
		update a2security.Users set TwoFactorEnabled = 0, AuthenticatorKey = null where Id = @Id;
end
go
------------------------------------------------
create or alter procedure a2security.[User.CreateApiUser]
@UserId bigint,
@ApiKey nvarchar(1023),
@Name nvarchar(255) = null,
@PersonName nvarchar(255) = null,
@Memo nvarchar(255) = null
as
begin
	set nocount on;
	set transaction isolation level read committed;
	set xact_abort on;

	declare @users table(Id bigint);
	declare @newid bigint;

	begin tran;
		insert into a2security.Users(IsApiUser, UserName, PersonName, Memo, Locale, SecurityStamp, SecurityStamp2)
		output inserted.Id into @users(Id)
		select 1, @Name, @PersonName, @Memo, u.Locale, N'', N''
		from a2security.Users u where Id = @UserId;

		select top(1) @newid = Id from @users;

		insert into a2security.ApiUserLogins([User], Mode, ApiKey) values
			(@newid, N'ApiKey', @ApiKey);
	commit tran;

	select Id, UserName, PersonName, Memo, Locale from a2security.ViewUsers where Id = @newid;
end
go
------------------------------------------------
create or alter procedure a2security.[User.DeleteApiUser]
@UserId bigint,
@Id bigint
as
begin
	set nocount on;
	set transaction isolation level read committed;
	set xact_abort on;

	-- TODO: isTenantAdmin

	begin tran
	update a2security.Users set Void = 1
		where Id = @Id;
	delete from a2security.ApiUserLogins where [User] = @Id;
	commit tran;

	-- We can't use ViewUsers (Void = 0)
	select Id from a2security.Users where Id = @Id;
end
go
------------------------------------------------
create or alter procedure a2security.[User.DeleteUser]
@UserId bigint = null,
@Id bigint
as
begin
	set nocount on;
	set transaction isolation level read committed;
	set xact_abort on;

	declare @guid nvarchar(50);
	set @guid = cast(newid() as nvarchar(50));
	begin tran;

	insert into a2security.DeletedUsers(Id, UserName, DomainUser, Email, PhoneNumber, DeletedByUser)
	select Id, UserName, DomainUser, Email, PhoneNumber, @UserId
	from a2security.Users where Id = @Id;

	update a2security.Users set Void = 1, SecurityStamp = N'', SecurityStamp2 = N'', PasswordHash = null, PasswordHash2 = null,
		EmailConfirmed = 0, PhoneNumberConfirmed = 0,
		UserName = @guid, Email = @guid, PhoneNumber = @guid, DomainUser = @guid
	where Id = @Id;

	-- We can't use ViewUsers (Void = 0)
	select Id, UserName, Email, PhoneNumber, DomainUser from a2security.Users where Id = @Id;

	commit tran;

end
go
------------------------------------------------
create or alter procedure a2security.[User.EditUser]
@UserId bigint,
@Id bigint,
@PersonName nvarchar(255) = null,
@PhoneNumber nvarchar(255) = null,
@Memo nvarchar(255) = null
as
begin
	set nocount on;
	set transaction isolation level read committed;

	update a2security.Users set PersonName = @PersonName, Memo = @Memo, PhoneNumber = @PhoneNumber
		where Id = @Id;

	select Id, PersonName, PhoneNumber, Memo from a2security.ViewUsers where Id = @Id;
end
go
------------------------------------------------
create or alter procedure a2security.[AddToken]
@Id bigint,
@Provider nvarchar(64),
@Token nvarchar(255),
@Expires datetime,
@Remove nvarchar(255) = null
as
begin
	set nocount on;
	set transaction isolation level read committed;
	set xact_abort on;
	begin tran;
	insert into a2security.RefreshTokens(UserId, [Provider], Token, Expires)
		values (@Id, @Provider, @Token, @Expires);
	if @Remove is not null
		delete from a2security.RefreshTokens 
		where UserId = @Id and [Provider] = @Provider and Token = @Remove;
	commit tran;
end
go
------------------------------------------------
create or alter procedure a2security.[GetToken]
@Id bigint,
@Provider nvarchar(255),
@Token nvarchar(255)
as
begin
	set nocount on;
	set transaction isolation level read committed;

	select [Token], UserId, Expires from a2security.RefreshTokens
	where UserId = @Id and [Provider] = @Provider and Token = @Token;
end
go

------------------------------------------------
create or alter procedure a2security.[RemoveToken]
@Id bigint,
@Provider nvarchar(255),
@Token nvarchar(511)
as
begin
	set nocount on;
	set transaction isolation level read committed;
	set xact_abort on;

	delete from a2security.RefreshTokens 
	where UserId = @Id and [Provider] = @Provider and Token = @Token;
end
go

------------------------------------------------
create or alter procedure a2security.[KeyVault.Load]
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	select [Value] from a2security.KeyVaults;
end
go
------------------------------------------------
create or alter procedure a2security.[KeyVault.Update]
@Key nvarchar(32),
@Value nvarchar(max),
@Expired datetime = null
as
begin
	set nocount on;
	set transaction isolation level read uncommitted;

	with TV as (
		select [Key] = @Key, [Value] = @Value, Expired = @Expired
	)
	merge a2security.KeyVaults as t
	using TV as s
	on t.[Key] = s.[Key]
	when matched then update set
		t.[Value] = s.[Value],
		t.Expired = s.[Expired]
	when not matched then insert
		([Key], [Value], [Expired]) values
		([Key], [Value], [Expired]);
end
go
