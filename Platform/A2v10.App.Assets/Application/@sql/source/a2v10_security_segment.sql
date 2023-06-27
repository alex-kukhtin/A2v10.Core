/*
Copyright © 2008-2023 Oleksandr Kukhtin

Last updated : 27 jun 2023
module version : 8102
*/

-- SECURITY SEGMENT
------------------------------------------------
create or alter procedure a2security.[Segment.CreateUser]
as
begin
	set nocount on;
	set transaction isolation level read committed;
end
go