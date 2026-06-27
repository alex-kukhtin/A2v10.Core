/*
Copyright © 2008-2023 Oleksandr Kukhtin

Last updated : 10 jul 2023
module version : 8100
*/

begin
	set nocount on;
	exec a2ui.[InvokeTenantInitProcedures.All];
end
go