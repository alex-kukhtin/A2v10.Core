/*
Copyright © 2008-2023 Oleksandr Kukhtin

Last updated : 24 may 2023
module version : 8100
*/
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
@LastLoginDate datetime,
@LastLoginHost nvarchar(255)
as
begin
	set nocount on;
	set transaction isolation level read committed;
	update a2security.ViewUsers set LastLoginDate = @LastLoginDate, LastLoginHost = @LastLoginHost 
	where Id=@Id;
end
go
------------------------------------------------
create or alter procedure a2security.UpdateUserLockout
@Id bigint,
@AccessFailedCount int,
@LockoutEndDateUtc datetimeoffset
as
begin
	set nocount on;
	set transaction isolation level read committed;

	update a2security.ViewUsers set 
		AccessFailedCount = @AccessFailedCount, LockoutEndDateUtc = @LockoutEndDateUtc
	where Id=@Id;
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

