SET NOCOUNT ON;
SET XACT_ABORT ON;
IF OBJECT_ID(N'medic.SY_MedicalLeaves', N'U') IS NULL
BEGIN
    THROW 50005, N'Instale primero la migración de MedicApp.', 1;
END;
IF DATABASE_PRINCIPAL_ID(N'MedicAppRuntime') IS NULL
BEGIN
    CREATE ROLE MedicAppRuntime AUTHORIZATION dbo;
END;
IF DATABASE_PRINCIPAL_ID(N'WfmMedicalReader') IS NULL
BEGIN
    CREATE ROLE WfmMedicalReader AUTHORIZATION dbo;
END;
GRANT SELECT, INSERT, UPDATE ON SCHEMA::medic TO MedicAppRuntime;
DENY DELETE ON SCHEMA::medic TO MedicAppRuntime;
DENY UPDATE, DELETE ON OBJECT::medic.AR_AuditEntries TO MedicAppRuntime;
DENY UPDATE, DELETE ON OBJECT::medic.IX_LaborEvents TO MedicAppRuntime;
GRANT SELECT ON OBJECT::dbo.SY_People TO MedicAppRuntime;
GRANT SELECT, UPDATE ON OBJECT::dbo.SC_Users TO MedicAppRuntime;
GRANT SELECT ON OBJECT::dbo.SC_LoginIdentifiers TO MedicAppRuntime;
GRANT SELECT ON OBJECT::dbo.SC_Roles TO MedicAppRuntime;
GRANT SELECT ON OBJECT::dbo.SC_UserRoles TO MedicAppRuntime;
GRANT SELECT ON OBJECT::dbo.SC_UserClaims TO MedicAppRuntime;
GRANT SELECT ON OBJECT::dbo.SC_RoleClaims TO MedicAppRuntime;
GRANT SELECT ON OBJECT::dbo.SC_UserLogins TO MedicAppRuntime;
GRANT SELECT ON OBJECT::dbo.SC_UserTokens TO MedicAppRuntime;
DENY UPDATE, DELETE ON OBJECT::medic.SY_LeaveDocuments TO MedicAppRuntime;
GRANT SELECT ON OBJECT::medic.vw_ApprovedLaborRestrictions TO WfmMedicalReader;
GRANT SELECT ON OBJECT::medic.vw_LaborEvents TO WfmMedicalReader;
-- Account membership is configured by the installer with the client's existing database users.
-- Identity updates include password hashing, security stamps and failed-login lockout.
