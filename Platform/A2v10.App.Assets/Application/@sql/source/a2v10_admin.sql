/*
Copyright © 2008-2023 Oleksandr Kukhtin

Last updated : 24 may 2023
module version : 8100
*/


------------------------------------------------
if not exists(select * from a2security.Tenants where Id <> 0)
	insert into a2security.Tenants(Id) values (1);
go
------------------------------------------------
if not exists(select * from a2security.Tenants where Id = 0)
	insert into a2security.Tenants(Id) values (0);
go
------------------------------------------------
if not exists(select * from a2security.Users)
begin
	set nocount on;
	set transaction isolation level read committed;

	insert into a2security.Users(Id, Tenant, UserName, SecurityStamp, PasswordHash, PersonName, EmailConfirmed)
	values (99, 1, N'admin@admin.com', N'c9bb451a-9d2b-4b26-9499-2d7d408ce54e', N'AJcfzvC7DCiRrfPmbVoigR7J8fHoK/xdtcWwahHDYJfKSKSWwX5pu9ChtxmE7Rs4Vg==',
		N'System administrator', 1);
end
go
