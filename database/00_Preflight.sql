SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() <> N'db_acaa35_trainify' AND DB_NAME() NOT LIKE N'MedicApp[_]Test[_]%'
BEGIN
    THROW 50000, N'Destino no autorizado para esta entrega de MedicApp.', 1;
END;
IF DB_NAME() IN (N'master', N'model', N'msdb', N'tempdb')
BEGIN
    THROW 50001, N'Seleccione la base compartida de aplicaciones.', 1;
END;
IF OBJECT_ID(N'dbo.SC_Users', N'U') IS NULL OR OBJECT_ID(N'dbo.SY_People', N'U') IS NULL
BEGIN
    THROW 50002, N'Falta el esquema compartido de WFMCore/Trainify. No aplicar MedicApp en la base antigua de MedicApp.', 1;
END;
IF NOT EXISTS
(
    SELECT 1
    FROM sys.columns AS C
    WHERE C.object_id = OBJECT_ID(N'dbo.SC_Users')
        AND C.name = N'Id'
        AND TYPE_NAME(C.user_type_id) = N'nvarchar'
        AND C.max_length = 900
)
BEGIN
    THROW 50003, N'La clave compartida de Identity no coincide con el contrato string(450).', 1;
END;
IF NOT EXISTS
(
    SELECT 1
    FROM sys.columns AS C
    WHERE C.object_id = OBJECT_ID(N'dbo.SY_People')
        AND C.name = N'Id'
        AND TYPE_NAME(C.user_type_id) = N'int'
)
BEGIN
    THROW 50004, N'La clave de personas no coincide con el contrato int.', 1;
END;
SELECT
      DB_NAME() AS DatabaseName
    , D.is_read_committed_snapshot_on AS ReadCommittedSnapshotEnabled
FROM sys.databases AS D
WHERE D.database_id = DB_ID();
