using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedicApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialMedical : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "medic");

            migrationBuilder.CreateTable(
                name: "AR_AuditEntries",
                schema: "medic",
                columns: table => new
                {
                    PKAuditEntry = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    EntityId = table.Column<int>(type: "int", nullable: false),
                    Operation = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Available = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    FKUserUpdater = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AR_AuditEntries", x => x.PKAuditEntry)
                        .Annotation("SqlServer:Clustered", true);
                    table.ForeignKey(
                        name: "FK_AR_AuditEntries_SC_Users_FKUserUpdater",
                        column: x => x.FKUserUpdater,
                        principalSchema: "dbo",
                        principalTable: "SC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CT_Doctors",
                schema: "medic",
                columns: table => new
                {
                    PKDoctor = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FKPerson = table.Column<int>(type: "int", nullable: false),
                    Specialty = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ProfessionalLicense = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Available = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    FKUserUpdater = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CT_Doctors", x => x.PKDoctor)
                        .Annotation("SqlServer:Clustered", true);
                    table.ForeignKey(
                        name: "FK_CT_Doctors_SC_Users_FKUserUpdater",
                        column: x => x.FKUserUpdater,
                        principalSchema: "dbo",
                        principalTable: "SC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CT_Doctors_SY_People_FKPerson",
                        column: x => x.FKPerson,
                        principalSchema: "dbo",
                        principalTable: "SY_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SC_AccessProfiles",
                schema: "medic",
                columns: table => new
                {
                    PKAccessProfile = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FKUser = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ManageDoctors = table.Column<bool>(type: "bit", nullable: false),
                    ManageAccess = table.Column<bool>(type: "bit", nullable: false),
                    ReadIntegration = table.Column<bool>(type: "bit", nullable: false),
                    Available = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    FKUserUpdater = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SC_AccessProfiles", x => x.PKAccessProfile)
                        .Annotation("SqlServer:Clustered", true);
                    table.ForeignKey(
                        name: "FK_SC_AccessProfiles_SC_Users_FKUser",
                        column: x => x.FKUser,
                        principalSchema: "dbo",
                        principalTable: "SC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SC_AccessProfiles_SC_Users_FKUserUpdater",
                        column: x => x.FKUserUpdater,
                        principalSchema: "dbo",
                        principalTable: "SC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SY_AvailabilityWindows",
                schema: "medic",
                columns: table => new
                {
                    PKAvailabilityWindow = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FKDoctor = table.Column<int>(type: "int", nullable: false),
                    StartsAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    SlotMinutes = table.Column<int>(type: "int", nullable: false),
                    Available = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    FKUserUpdater = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SY_AvailabilityWindows", x => x.PKAvailabilityWindow)
                        .Annotation("SqlServer:Clustered", true);
                    table.CheckConstraint("CK_Windows_Dates", "[EndsAt] > [StartsAt]");
                    table.CheckConstraint("CK_Windows_Slot", "[SlotMinutes] BETWEEN 5 AND 120");
                    table.ForeignKey(
                        name: "FK_SY_AvailabilityWindows_CT_Doctors_FKDoctor",
                        column: x => x.FKDoctor,
                        principalSchema: "medic",
                        principalTable: "CT_Doctors",
                        principalColumn: "PKDoctor",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SY_AvailabilityWindows_SC_Users_FKUserUpdater",
                        column: x => x.FKUserUpdater,
                        principalSchema: "dbo",
                        principalTable: "SC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SY_Appointments",
                schema: "medic",
                columns: table => new
                {
                    PKAppointment = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FKPerson = table.Column<int>(type: "int", nullable: false),
                    FKDoctor = table.Column<int>(type: "int", nullable: false),
                    FKAvailabilityWindow = table.Column<int>(type: "int", nullable: false),
                    StartsAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RequestKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Available = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    FKUserUpdater = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SY_Appointments", x => x.PKAppointment)
                        .Annotation("SqlServer:Clustered", true);
                    table.CheckConstraint("CK_Appointments_Dates", "[EndsAt] > [StartsAt]");
                    table.CheckConstraint("CK_Appointments_Status", "[Status] BETWEEN 1 AND 4");
                    table.ForeignKey(
                        name: "FK_SY_Appointments_CT_Doctors_FKDoctor",
                        column: x => x.FKDoctor,
                        principalSchema: "medic",
                        principalTable: "CT_Doctors",
                        principalColumn: "PKDoctor",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SY_Appointments_SC_Users_FKUserUpdater",
                        column: x => x.FKUserUpdater,
                        principalSchema: "dbo",
                        principalTable: "SC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SY_Appointments_SY_AvailabilityWindows_FKAvailabilityWindow",
                        column: x => x.FKAvailabilityWindow,
                        principalSchema: "medic",
                        principalTable: "SY_AvailabilityWindows",
                        principalColumn: "PKAvailabilityWindow",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SY_Appointments_SY_People_FKPerson",
                        column: x => x.FKPerson,
                        principalSchema: "dbo",
                        principalTable: "SY_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SY_MedicalLeaves",
                schema: "medic",
                columns: table => new
                {
                    PKMedicalLeave = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FKPerson = table.Column<int>(type: "int", nullable: false),
                    FKDoctor = table.Column<int>(type: "int", nullable: false),
                    FKAppointment = table.Column<int>(type: "int", nullable: true),
                    StartsAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    Available = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    FKUserUpdater = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SY_MedicalLeaves", x => x.PKMedicalLeave)
                        .Annotation("SqlServer:Clustered", true);
                    table.CheckConstraint("CK_Leaves_Dates", "[EndsAt] > [StartsAt]");
                    table.CheckConstraint("CK_Leaves_Revision", "[Revision] > 0");
                    table.CheckConstraint("CK_Leaves_Status", "[Status] BETWEEN 1 AND 3");
                    table.ForeignKey(
                        name: "FK_SY_MedicalLeaves_CT_Doctors_FKDoctor",
                        column: x => x.FKDoctor,
                        principalSchema: "medic",
                        principalTable: "CT_Doctors",
                        principalColumn: "PKDoctor",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SY_MedicalLeaves_SC_Users_FKUserUpdater",
                        column: x => x.FKUserUpdater,
                        principalSchema: "dbo",
                        principalTable: "SC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SY_MedicalLeaves_SY_Appointments_FKAppointment",
                        column: x => x.FKAppointment,
                        principalSchema: "medic",
                        principalTable: "SY_Appointments",
                        principalColumn: "PKAppointment",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SY_MedicalLeaves_SY_People_FKPerson",
                        column: x => x.FKPerson,
                        principalSchema: "dbo",
                        principalTable: "SY_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IX_LaborEvents",
                schema: "medic",
                columns: table => new
                {
                    PKLaborEvent = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FKMedicalLeave = table.Column<int>(type: "int", nullable: false),
                    FKPerson = table.Column<int>(type: "int", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    StartsAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Available = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    FKUserUpdater = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IX_LaborEvents", x => x.PKLaborEvent)
                        .Annotation("SqlServer:Clustered", true);
                    table.CheckConstraint("CK_LaborEvents_Dates", "[EndsAt] > [StartsAt]");
                    table.CheckConstraint("CK_LaborEvents_Status", "[Status] BETWEEN 1 AND 3");
                    table.ForeignKey(
                        name: "FK_IX_LaborEvents_SC_Users_FKUserUpdater",
                        column: x => x.FKUserUpdater,
                        principalSchema: "dbo",
                        principalTable: "SC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IX_LaborEvents_SY_MedicalLeaves_FKMedicalLeave",
                        column: x => x.FKMedicalLeave,
                        principalSchema: "medic",
                        principalTable: "SY_MedicalLeaves",
                        principalColumn: "PKMedicalLeave",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IX_LaborEvents_SY_People_FKPerson",
                        column: x => x.FKPerson,
                        principalSchema: "dbo",
                        principalTable: "SY_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AR_AuditEntries_EntityType_EntityId_LastUpdated",
                schema: "medic",
                table: "AR_AuditEntries",
                columns: new[] { "EntityType", "EntityId", "LastUpdated" });

            migrationBuilder.CreateIndex(
                name: "IX_AR_AuditEntries_FKUserUpdater",
                schema: "medic",
                table: "AR_AuditEntries",
                column: "FKUserUpdater");

            migrationBuilder.CreateIndex(
                name: "IX_CT_Doctors_FKPerson",
                schema: "medic",
                table: "CT_Doctors",
                column: "FKPerson",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CT_Doctors_FKUserUpdater",
                schema: "medic",
                table: "CT_Doctors",
                column: "FKUserUpdater");

            migrationBuilder.CreateIndex(
                name: "IX_IX_LaborEvents_FKMedicalLeave_Revision",
                schema: "medic",
                table: "IX_LaborEvents",
                columns: new[] { "FKMedicalLeave", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IX_LaborEvents_FKPerson",
                schema: "medic",
                table: "IX_LaborEvents",
                column: "FKPerson");

            migrationBuilder.CreateIndex(
                name: "IX_IX_LaborEvents_FKUserUpdater",
                schema: "medic",
                table: "IX_LaborEvents",
                column: "FKUserUpdater");

            migrationBuilder.CreateIndex(
                name: "IX_SC_AccessProfiles_FKUser",
                schema: "medic",
                table: "SC_AccessProfiles",
                column: "FKUser",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SC_AccessProfiles_FKUserUpdater",
                schema: "medic",
                table: "SC_AccessProfiles",
                column: "FKUserUpdater");

            migrationBuilder.CreateIndex(
                name: "IX_SY_Appointments_FKAvailabilityWindow",
                schema: "medic",
                table: "SY_Appointments",
                column: "FKAvailabilityWindow");

            migrationBuilder.CreateIndex(
                name: "IX_SY_Appointments_FKDoctor_StartsAt_EndsAt",
                schema: "medic",
                table: "SY_Appointments",
                columns: new[] { "FKDoctor", "StartsAt", "EndsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SY_Appointments_FKPerson_StartsAt_EndsAt",
                schema: "medic",
                table: "SY_Appointments",
                columns: new[] { "FKPerson", "StartsAt", "EndsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SY_Appointments_FKUserUpdater",
                schema: "medic",
                table: "SY_Appointments",
                column: "FKUserUpdater");

            migrationBuilder.CreateIndex(
                name: "IX_SY_Appointments_RequestKey",
                schema: "medic",
                table: "SY_Appointments",
                column: "RequestKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SY_AvailabilityWindows_FKDoctor_StartsAt_EndsAt",
                schema: "medic",
                table: "SY_AvailabilityWindows",
                columns: new[] { "FKDoctor", "StartsAt", "EndsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SY_AvailabilityWindows_FKUserUpdater",
                schema: "medic",
                table: "SY_AvailabilityWindows",
                column: "FKUserUpdater");

            migrationBuilder.CreateIndex(
                name: "IX_SY_MedicalLeaves_FKAppointment",
                schema: "medic",
                table: "SY_MedicalLeaves",
                column: "FKAppointment");

            migrationBuilder.CreateIndex(
                name: "IX_SY_MedicalLeaves_FKDoctor",
                schema: "medic",
                table: "SY_MedicalLeaves",
                column: "FKDoctor");

            migrationBuilder.CreateIndex(
                name: "IX_SY_MedicalLeaves_FKPerson_StartsAt_EndsAt",
                schema: "medic",
                table: "SY_MedicalLeaves",
                columns: new[] { "FKPerson", "StartsAt", "EndsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SY_MedicalLeaves_FKUserUpdater",
                schema: "medic",
                table: "SY_MedicalLeaves",
                column: "FKUserUpdater");

            migrationBuilder.CreateIndex(
                name: "IX_SY_MedicalLeaves_Reference",
                schema: "medic",
                table: "SY_MedicalLeaves",
                column: "Reference",
                unique: true);
            migrationBuilder.Sql("""
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
                """);
            migrationBuilder.Sql("""
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
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW medic.vw_LaborEvents;");
            migrationBuilder.Sql("DROP VIEW medic.vw_ApprovedLaborRestrictions;");
            migrationBuilder.DropTable(
                name: "AR_AuditEntries",
                schema: "medic");

            migrationBuilder.DropTable(
                name: "IX_LaborEvents",
                schema: "medic");

            migrationBuilder.DropTable(
                name: "SC_AccessProfiles",
                schema: "medic");

            migrationBuilder.DropTable(
                name: "SY_MedicalLeaves",
                schema: "medic");

            migrationBuilder.DropTable(
                name: "SY_Appointments",
                schema: "medic");

            migrationBuilder.DropTable(
                name: "SY_AvailabilityWindows",
                schema: "medic");

            migrationBuilder.DropTable(
                name: "CT_Doctors",
                schema: "medic");
        }
    }
}
