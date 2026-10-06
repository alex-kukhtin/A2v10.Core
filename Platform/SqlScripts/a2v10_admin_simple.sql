/*
Copyright © 2008-2026 Oleksandr Kukhtin

Last updated : 06 oct 2026
module version : 8670
*/
------------------------------------------------
if not exists(select * from a2security.Users)
begin
	set nocount on;
	set transaction isolation level read committed;

	insert into a2security.Users(Id, UserName, Email, SecurityStamp, PasswordHash, PersonName, EmailConfirmed)
	values (99, N'admin@admin.com', N'admin@admin.com', N'c9bb451a-9d2b-4b26-9499-2d7d408ce54e', N'AJcfzvC7DCiRrfPmbVoigR7J8fHoK/xdtcWwahHDYJfKSKSWwX5pu9ChtxmE7Rs4Vg==',
		N'System administrator', 1);
end
go
------------------------------------------------
if not exists(select * from a2security.Roles where Id = N'Admin')
	insert into a2security.Roles(Id, [Name]) values (N'Admin', N'@[Role.Admin]');
go
------------------------------------------------
-- every user holds it without an assignment, so it has no UserRoles rows
if not exists(select * from a2security.Roles where Id = N'Everyone')
	insert into a2security.Roles(Id, [Name]) values (N'Everyone', N'@[Role.Everyone]');
go
------------------------------------------------
if not exists(select * from a2security.UserRoles where UserId = 99 and [Role] = N'Admin')
	insert into a2security.UserRoles(UserId, [Role]) values (99, N'Admin');
go

