:ON ERROR EXIT
-- Ejecutar en modo SQLCMD, después de 01_InstallMedicApp.sql. Re-ejecutable.
-- Estándar SQL de MWZSystem, sec. 7.4: las vistas se crean WITH SCHEMABINDING. Mismas columnas, mismos nombres y
-- mismo resultado que en 01; CREATE OR ALTER conserva los permisos de 02_RuntimeRoles.sql (WfmMedicalReader).
-- Efecto: una migración que cambie o borre una columna usada aquí (SY_MedicalLeaves o IX_LaborEvents) falla al
-- aplicarse en lugar de romper la vista en tiempo de ejecución; esa migración debe quitar antes el enlace.
-- Reversa: ejecutar el bloque del final de este archivo, que deja las vistas como las creó 01 (sin SCHEMABINDING).
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() <> N'db_acaa35_trainify' AND DB_NAME() NOT LIKE N'MedicApp[_]Test[_]%'
BEGIN
    THROW 50000, N'Destino no autorizado para esta entrega de MedicApp.', 1;
END;
IF OBJECT_ID(N'medic.SY_MedicalLeaves', N'U') IS NULL OR OBJECT_ID(N'medic.IX_LaborEvents', N'U') IS NULL
BEGIN
    THROW 50005, N'Instale primero la migración de MedicApp.', 1;
END;
GO

CREATE OR ALTER VIEW medic.vw_ApprovedLaborRestrictions
WITH SCHEMABINDING
AS
SELECT
      ML.PKMedicalLeave
    , ML.FKPerson
    , ML.StartsAt AS StartsAtUtc
    , ML.EndsAt AS EndsAtUtc
    , ML.Revision
    , ML.LastUpdated
FROM medic.SY_MedicalLeaves AS ML
WHERE ML.Available = 1
    AND ML.Status = 2;
GO

CREATE OR ALTER VIEW medic.vw_LaborEvents
WITH SCHEMABINDING
AS
SELECT
      LE.PKLaborEvent
    , LE.FKMedicalLeave
    , LE.FKPerson
    , LE.Revision
    , LE.StartsAt AS StartsAtUtc
    , LE.EndsAt AS EndsAtUtc
    , LE.Status
    , LE.LastUpdated
FROM medic.IX_LaborEvents AS LE;
GO

-- ---------- Reversa (comentada: no se ejecuta con el script; quitar los -- y correr en modo SQLCMD) ----------
-- CREATE OR ALTER VIEW medic.vw_ApprovedLaborRestrictions
-- AS
-- SELECT
--       ML.PKMedicalLeave
--     , ML.FKPerson
--     , ML.StartsAt AS StartsAtUtc
--     , ML.EndsAt AS EndsAtUtc
--     , ML.Revision
--     , ML.LastUpdated
-- FROM medic.SY_MedicalLeaves AS ML
-- WHERE ML.Available = 1
--     AND ML.Status = 2;
-- GO
-- CREATE OR ALTER VIEW medic.vw_LaborEvents
-- AS
-- SELECT
--       LE.PKLaborEvent
--     , LE.FKMedicalLeave
--     , LE.FKPerson
--     , LE.Revision
--     , LE.StartsAt AS StartsAtUtc
--     , LE.EndsAt AS EndsAtUtc
--     , LE.Status
--     , LE.LastUpdated
-- FROM medic.IX_LaborEvents AS LE;
-- GO
