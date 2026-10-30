:ON ERROR EXIT
-- Ejecutar en modo SQLCMD. Destino confirmado: db_acaa35_trainify.
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

GO
IF OBJECT_ID(N'[medic].[__EFMigrationsHistory]') IS NULL
BEGIN
    IF SCHEMA_ID(N'medic') IS NULL EXEC(N'CREATE SCHEMA [medic];');
    CREATE TABLE [medic].[__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [medic].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030055708_InitialMedical'
)
BEGIN
    IF SCHEMA_ID(N'medic') IS NULL EXEC(N'CREATE SCHEMA [medic];');
END;

IF NOT EXISTS (
    SELECT * FROM [medic].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030055708_InitialMedical'
)
BEGIN
    CREATE TABLE [medic].[AR_AuditEntries] (
        [PKAuditEntry] int NOT NULL IDENTITY,
        [EntityType] nvarchar(80) NOT NULL,
        [EntityId] int NOT NULL,
        [Operation] nvarchar(80) NOT NULL,
        [Reason] nvarchar(500) NOT NULL,
        [Available] bit NOT NULL DEFAULT CAST(1 AS bit),
        [FKUserUpdater] nvarchar(450) NOT NULL,
        [LastUpdated] datetime2(0) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_AR_AuditEntries] PRIMARY KEY CLUSTERED ([PKAuditEntry]),
        CONSTRAINT [FK_AR_AuditEntries_SC_Users_FKUserUpdater] FOREIGN KEY ([FKUserUpdater]) REFERENCES [dbo].[SC_Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [medic].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030055708_InitialMedical'
)
BEGIN
    CREATE TABLE [medic].[CT_Doctors] (
        [PKDoctor] int NOT NULL IDENTITY,
        [FKPerson] int NOT NULL,
        [Specialty] nvarchar(120) NOT NULL,
        [ProfessionalLicense] nvarchar(60) NOT NULL,
        [Available] bit NOT NULL DEFAULT CAST(1 AS bit),
        [FKUserUpdater] nvarchar(450) NOT NULL,
        [LastUpdated] datetime2(0) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_CT_Doctors] PRIMARY KEY CLUSTERED ([PKDoctor]),
        CONSTRAINT [FK_CT_Doctors_SC_Users_FKUserUpdater] FOREIGN KEY ([FKUserUpdater]) REFERENCES [dbo].[SC_Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CT_Doctors_SY_People_FKPerson] FOREIGN KEY ([FKPerson]) REFERENCES [dbo].[SY_People] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [medic].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030055708_InitialMedical'
)
BEGIN
    CREATE TABLE [medic].[SC_AccessProfiles] (
        [PKAccessProfile] int NOT NULL IDENTITY,
        [FKUser] nvarchar(450) NOT NULL,
        [ManageDoctors] bit NOT NULL,
        [ManageAccess] bit NOT NULL,
        [ReadIntegration] bit NOT NULL,
        [Available] bit NOT NULL DEFAULT CAST(1 AS bit),
        [FKUserUpdater] nvarchar(450) NOT NULL,
        [LastUpdated] datetime2(0) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_SC_AccessProfiles] PRIMARY KEY CLUSTERED ([PKAccessProfile]),
        CONSTRAINT [FK_SC_AccessProfiles_SC_Users_FKUser] FOREIGN KEY ([FKUser]) REFERENCES [dbo].[SC_Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SC_AccessProfiles_SC_Users_FKUserUpdater] FOREIGN KEY ([FKUserUpdater]) REFERENCES [dbo].[SC_Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [medic].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030055708_InitialMedical'
)
BEGIN
    CREATE TABLE [medic].[SY_AvailabilityWindows] (
        [PKAvailabilityWindow] int NOT NULL IDENTITY,
        [FKDoctor] int NOT NULL,
        [StartsAt] datetime2(0) NOT NULL,
        [EndsAt] datetime2(0) NOT NULL,
        [SlotMinutes] int NOT NULL,
        [Available] bit NOT NULL DEFAULT CAST(1 AS bit),
        [FKUserUpdater] nvarchar(450) NOT NULL,
        [LastUpdated] datetime2(0) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_SY_AvailabilityWindows] PRIMARY KEY CLUSTERED ([PKAvailabilityWindow]),
        CONSTRAINT [CK_Windows_Dates] CHECK ([EndsAt] > [StartsAt]),
        CONSTRAINT [CK_Windows_Slot] CHECK ([SlotMinutes] BETWEEN 5 AND 120),
        CONSTRAINT [FK_SY_AvailabilityWindows_CT_Doctors_FKDoctor] FOREIGN KEY ([FKDoctor]) REFERENCES [medic].[CT_Doctors] ([PKDoctor]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SY_AvailabilityWindows_SC_Users_FKUserUpdater] FOREIGN KEY ([FKUserUpdater]) REFERENCES [dbo].[SC_Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [medic].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030055708_InitialMedical'
)
BEGIN
    CREATE TABLE [medic].[SY_Appointments] (
        [PKAppointment] int NOT NULL IDENTITY,
        [FKPerson] int NOT NULL,
        [FKDoctor] int NOT NULL,
        [FKAvailabilityWindow] int NOT NULL,
        [StartsAt] datetime2(0) NOT NULL,
        [EndsAt] datetime2(0) NOT NULL,
        [Status] int NOT NULL,
        [RequestKey] uniqueidentifier NOT NULL,
        [CancellationReason] nvarchar(500) NULL,
        [Available] bit NOT NULL DEFAULT CAST(1 AS bit),
        [FKUserUpdater] nvarchar(450) NOT NULL,
        [LastUpdated] datetime2(0) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_SY_Appointments] PRIMARY KEY CLUSTERED ([PKAppointment]),
        CONSTRAINT [CK_Appointments_Dates] CHECK ([EndsAt] > [StartsAt]),
        CONSTRAINT [CK_Appointments_Status] CHECK ([Status] BETWEEN 1 AND 4),
        CONSTRAINT [FK_SY_Appointments_CT_Doctors_FKDoctor] FOREIGN KEY ([FKDoctor]) REFERENCES [medic].[CT_Doctors] ([PKDoctor]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SY_Appointments_SC_Users_FKUserUpdater] FOREIGN KEY ([FKUserUpdater]) REFERENCES [dbo].[SC_Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SY_Appointments_SY_AvailabilityWindows_FKAvailabilityWindow] FOREIGN KEY ([FKAvailabilityWindow]) REFERENCES [medic].[SY_AvailabilityWindows] ([PKAvailabilityWindow]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SY_Appointments_SY_People_FKPerson] FOREIGN KEY ([FKPerson]) REFERENCES [dbo].[SY_People] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [medic].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030055708_InitialMedical'
)
BEGIN
    CREATE TABLE [medic].[SY_MedicalLeaves] (
        [PKMedicalLeave] int NOT NULL IDENTITY,
        [FKPerson] int NOT NULL,
        [FKDoctor] int NOT NULL,
        [FKAppointment] int NULL,
        [StartsAt] datetime2(0) NOT NULL,
        [EndsAt] datetime2(0) NOT NULL,
        [Status] int NOT NULL,
        [Reference] nvarchar(80) NOT NULL,
        [CancellationReason] nvarchar(500) NULL,
        [Revision] int NOT NULL,
        [Available] bit NOT NULL DEFAULT CAST(1 AS bit),
        [FKUserUpdater] nvarchar(450) NOT NULL,
        [LastUpdated] datetime2(0) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_SY_MedicalLeaves] PRIMARY KEY CLUSTERED ([PKMedicalLeave]),
        CONSTRAINT [CK_Leaves_Dates] CHECK ([EndsAt] > [StartsAt]),
        CONSTRAINT [CK_Leaves_Revision] CHECK ([Revision] > 0),
        CONSTRAINT [CK_Leaves_Status] CHECK ([Status] BETWEEN 1 AND 3),
        CONSTRAINT [FK_SY_MedicalLeaves_CT_Doctors_FKDoctor] FOREIGN KEY ([FKDoctor]) REFERENCES [medic].[CT_Doctors] ([PKDoctor]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SY_MedicalLeaves_SC_Users_FKUserUpdater] FOREIGN KEY ([FKUserUpdater]) REFERENCES [dbo].[SC_Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SY_MedicalLeaves_SY_Appointments_FKAppointment] FOREIGN KEY ([FKAppointment]) REFERENCES [medic].[SY_Appointments] ([PKAppointment]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SY_MedicalLeaves_SY_People_FKPerson] FOREIGN KEY ([FKPerson]) REFERENCES [dbo].[SY_People] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [medic].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030055708_InitialMedical'
)
BEGIN
    CREATE TABLE [medic].[IX_LaborEvents] (
        [PKLaborEvent] int NOT NULL IDENTITY,
        [FKMedicalLeave] int NOT NULL,
        [FKPerson] int NOT NULL,
        [Revision] int NOT NULL,
        [StartsAt] datetime2(0) NOT NULL,
        [EndsAt] datetime2(0) NOT NULL,
        [Status] int NOT NULL,
        [Available] bit NOT NULL DEFAULT CAST(1 AS bit),
        [FKUserUpdater] nvarchar(450) NOT NULL,
        [LastUpdated] datetime2(0) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_IX_LaborEvents] PRIMARY KEY CLUSTERED ([PKLaborEvent]),
        CONSTRAINT [CK_LaborEvents_Dates] CHECK ([EndsAt] > [StartsAt]),
        CONSTRAINT [CK_LaborEvents_Status] CHECK ([Status] BETWEEN 1 AND 3),
        CONSTRAINT [FK_IX_LaborEvents_SC_Users_FKUserUpdater] FOREIGN KEY ([FKUserUpdater]) REFERENCES [dbo].[SC_Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_IX_LaborEvents_SY_MedicalLeaves_FKMedicalLeave] FOREIGN KEY ([FKMedicalLeave]) REFERENCES [medic].[SY_MedicalLeaves] ([PKMedicalLeave]) ON DELETE NO ACTION,
        CONSTRAINT [FK_IX_LaborEvents_SY_People_FKPerson] FOREIGN KEY ([FKPerson]) REFERENCES [dbo].[SY_People] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [medic].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030055708_InitialMedical'
)
BEGIN
    CREATE INDEX [IX_AR_AuditEntries_EntityType_EntityId_LastUpdated] ON [medic].[AR_AuditEntries] ([EntityType], [EntityId], [LastUpdated]);
END;

IF NOT EXISTS (
    SELECT * FROM [medic].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030055708_InitialMedical'
)
BEGIN
    CREATE INDEX [IX_AR_AuditEntries_FKUserUpdater] ON [medic].[AR_AuditEntries] ([FKUserUpdater]);
END;

IF NOT EXISTS (
    SELECT * FROM [medic].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030055708_InitialMedical'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CT_Doctors_FKPerson] ON [medic].[CT_Doctors] ([FKPerson]);
END;

IF NOT EXISTS (
    SELECT * FROM [medic].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030055708_InitialMedical'
)
BEGIN
    CREATE INDEX [IX_CT_Doctors_FKUserUpdater] ON [medic].[CT_Doctors] ([FKUserUpdater]);
END;

IF NOT EXISTS (
    SELECT * FROM [medic].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030055708_InitialMedical'
)
BEGIN
    CREATE UNIQUE INDEX [IX_IX_LaborEvents_FKMedicalLeave_Revision] ON [medic].[IX_LaborEvents] ([FKMedicalLeave], [Revision]);
END;

IF NOT EXISTS (
    SELECT * FROM [medic].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030055708_InitialMedical'
)
BEGIN
    CREATE INDEX [IX_IX_LaborEvents_FKPerson] ON [medic].[IX_LaborEvents] ([FKPerson]);
END;

IF NOT EXISTS (
    SELECT * FROM [medic].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030055708_InitialMedical'
)
BEGIN
    CREATE INDEX [IX_IX_LaborEvents_FKUserUpdater] ON [medic].[IX_LaborEvents] ([FKUserUpdater]);
END;

IF NOT EXISTS (
    SELECT * FROM [medic].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030055708_InitialMedical'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SC_AccessProfiles_FKUser] ON [medic].[SC_AccessProfiles] ([FKUser]);
END;

IF NOT EXISTS (
    SELECT * FROM [medic].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030055708_InitialMedical'
)
BEGIN
    CREATE INDEX [IX_SC_AccessProfiles_FKUserUpdater] ON [medic].[SC_AccessProfiles] ([FKUserUpdater]);
END;

IF NOT EXISTS (
    SELECT * FROM [medic].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030055708_InitialMedical'
)
BEGIN
    CREATE INDEX [IX_SY_Appointments_FKAvailabilityWindow] ON [medic].[SY_Appointments] ([FKAvailabilityWindow]);
END;

IF NOT EXISTS (
    SELECT * FROM [medic].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030055708_InitialMedical'
)
BEGIN
    CREATE INDEX [IX_SY_Appointments_FKDoctor_StartsAt_EndsAt] ON [medic].[SY_Appointments] ([FKDoctor], [StartsAt], [EndsAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [medic].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030055708_InitialMedical'
)
BEGIN
    CREATE INDEX [IX_SY_Appointments_FKPerson_StartsAt_EndsAt] ON [medic].[SY_Appointments] ([FKPerson], [StartsAt], [EndsAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [medic].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030055708_InitialMedical'
)
BEGIN
    CREATE INDEX [IX_SY_Appointments_FKUserUpdater] ON [medic].[SY_Appointments] ([FKUserUpdater]);
END;

IF NOT EXISTS (
    SELECT * FROM [medic].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030055708_InitialMedical'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SY_Appointments_RequestKey] ON [medic].[SY_Appointments] ([RequestKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [medic].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030055708_InitialMedical'
)
BEGIN
    CREATE INDEX [IX_SY_AvailabilityWindows_FKDoctor_StartsAt_EndsAt] ON [medic].[SY_AvailabilityWindows] ([FKDoctor], [StartsAt], [EndsAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [medic].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030055708_InitialMedical'
)
BEGIN
    CREATE INDEX [IX_SY_AvailabilityWindows_FKUserUpdater] ON [medic].[SY_AvailabilityWindows] ([FKUserUpdater]);
END;

IF NOT EXISTS (
    SELECT * FROM [medic].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030055708_InitialMedical'
)
BEGIN
    CREATE INDEX [IX_SY_MedicalLeaves_FKAppointment] ON [medic].[SY_MedicalLeaves] ([FKAppointment]);
END;

IF NOT EXISTS (
    SELECT * FROM [medic].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030055708_InitialMedical'
)
BEGIN
    CREATE INDEX [IX_SY_MedicalLeaves_FKDoctor] ON [medic].[SY_MedicalLeaves] ([FKDoctor]);
END;

IF NOT EXISTS (
    SELECT * FROM [medic].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030055708_InitialMedical'
)
BEGIN
    CREATE INDEX [IX_SY_MedicalLeaves_FKPerson_StartsAt_EndsAt] ON [medic].[SY_MedicalLeaves] ([FKPerson], [StartsAt], [EndsAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [medic].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030055708_InitialMedical'
)
BEGIN
    CREATE INDEX [IX_SY_MedicalLeaves_FKUserUpdater] ON [medic].[SY_MedicalLeaves] ([FKUserUpdater]);
END;

IF NOT EXISTS (
    SELECT * FROM [medic].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030055708_InitialMedical'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SY_MedicalLeaves_Reference] ON [medic].[SY_MedicalLeaves] ([Reference]);
END;

IF NOT EXISTS (
    SELECT * FROM [medic].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030055708_InitialMedical'
)
BEGIN
    EXEC(N'CREATE VIEW medic.vw_ApprovedLaborRestrictions
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
        AND ML.Status = 2;');
END;

IF NOT EXISTS (
    SELECT * FROM [medic].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030055708_InitialMedical'
)
BEGIN
    EXEC(N'CREATE VIEW medic.vw_LaborEvents
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
    FROM medic.IX_LaborEvents AS LE;');
END;

IF NOT EXISTS (
    SELECT * FROM [medic].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030055708_InitialMedical'
)
BEGIN
    INSERT INTO [medic].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261030055708_InitialMedical', N'10.0.14');
END;

COMMIT;
GO

