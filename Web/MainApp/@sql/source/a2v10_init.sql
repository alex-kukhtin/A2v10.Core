/*
Copyright © 2008-2023 Oleksandr Kukhtin

Last updated : 02 jul 2023
module version : 8110
*/


------------------------------------------------
if not exists(select * from a2security.Tenants where Id <> 0)
begin
	set nocount on;
	insert into a2security.Tenants(Id) values (1);
end
go
------------------------------------------------
if not exists(select * from a2security.Tenants where Id = 0)
begin
	set nocount on;
	insert into a2security.Tenants(Id) values (0);
end
go
