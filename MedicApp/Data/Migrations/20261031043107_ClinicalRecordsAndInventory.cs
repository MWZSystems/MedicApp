using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedicApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class ClinicalRecordsAndInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ManageClinicalRecords",
                schema: "medic",
                table: "SC_AccessProfiles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ManageInventory",
                schema: "medic",
                table: "SC_AccessProfiles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "CT_StockItems",
                schema: "medic",
                columns: table => new
                {
                    PKStockItem = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Lot = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ExpiresOn = table.Column<DateOnly>(type: "date", nullable: true),
                    MinimumStock = table.Column<decimal>(type: "decimal(12,3)", precision: 12, scale: 3, nullable: false),
                    CurrentStock = table.Column<decimal>(type: "decimal(12,3)", precision: 12, scale: 3, nullable: false),
                    Available = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    FKUserUpdater = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CT_StockItems", x => x.PKStockItem)
                        .Annotation("SqlServer:Clustered", true);
                    table.CheckConstraint("CK_StockItems_Stock", "[MinimumStock] >= 0 AND [CurrentStock] >= 0");
                    table.ForeignKey(
                        name: "FK_CT_StockItems_SC_Users_FKUserUpdater",
                        column: x => x.FKUserUpdater,
                        principalSchema: "dbo",
                        principalTable: "SC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SY_ClinicalVisits",
                schema: "medic",
                columns: table => new
                {
                    PKClinicalVisit = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FKPerson = table.Column<int>(type: "int", nullable: false),
                    VisitedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    RequestKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Findings = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Diagnosis = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Treatment = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Prescription = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Weight = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: true),
                    Height = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: true),
                    Temperature = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: true),
                    Systolic = table.Column<int>(type: "int", nullable: true),
                    Diastolic = table.Column<int>(type: "int", nullable: true),
                    Pulse = table.Column<int>(type: "int", nullable: true),
                    RespiratoryRate = table.Column<int>(type: "int", nullable: true),
                    OxygenSaturation = table.Column<int>(type: "int", nullable: true),
                    Glucose = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: true),
                    Available = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    FKUserUpdater = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SY_ClinicalVisits", x => x.PKClinicalVisit)
                        .Annotation("SqlServer:Clustered", true);
                    table.CheckConstraint("CK_ClinicalVisits_Vitals", "([Weight] IS NULL OR [Weight] BETWEEN 0.1 AND 700) AND ([Height] IS NULL OR [Height] BETWEEN 1 AND 300) AND ([Temperature] IS NULL OR [Temperature] BETWEEN 20 AND 50) AND ([Systolic] IS NULL OR [Systolic] BETWEEN 20 AND 300) AND ([Diastolic] IS NULL OR [Diastolic] BETWEEN 10 AND 200) AND ([Pulse] IS NULL OR [Pulse] BETWEEN 1 AND 300) AND ([RespiratoryRate] IS NULL OR [RespiratoryRate] BETWEEN 1 AND 150) AND ([OxygenSaturation] IS NULL OR [OxygenSaturation] BETWEEN 1 AND 100) AND ([Glucose] IS NULL OR [Glucose] BETWEEN 1 AND 1500)");
                    table.ForeignKey(
                        name: "FK_SY_ClinicalVisits_SC_Users_FKUserUpdater",
                        column: x => x.FKUserUpdater,
                        principalSchema: "dbo",
                        principalTable: "SC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SY_ClinicalVisits_SY_People_FKPerson",
                        column: x => x.FKPerson,
                        principalSchema: "dbo",
                        principalTable: "SY_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IX_StockMovements",
                schema: "medic",
                columns: table => new
                {
                    PKStockMovement = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FKStockItem = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(12,3)", precision: 12, scale: 3, nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(12,3)", precision: 12, scale: 3, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RequestKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Available = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    FKUserUpdater = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IX_StockMovements", x => x.PKStockMovement)
                        .Annotation("SqlServer:Clustered", true);
                    table.CheckConstraint("CK_StockMovements_Kind", "[Kind] BETWEEN 0 AND 2");
                    table.CheckConstraint("CK_StockMovements_Quantity", "[Quantity] > 0 AND [Balance] >= 0");
                    table.ForeignKey(
                        name: "FK_IX_StockMovements_CT_StockItems_FKStockItem",
                        column: x => x.FKStockItem,
                        principalSchema: "medic",
                        principalTable: "CT_StockItems",
                        principalColumn: "PKStockItem",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IX_StockMovements_SC_Users_FKUserUpdater",
                        column: x => x.FKUserUpdater,
                        principalSchema: "dbo",
                        principalTable: "SC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SY_PatientDocuments",
                schema: "medic",
                columns: table => new
                {
                    PKPatientDocument = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FKPerson = table.Column<int>(type: "int", nullable: false),
                    FKClinicalVisit = table.Column<int>(type: "int", nullable: true),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    StorageKey = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    OriginalName = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    ContentType = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: false),
                    ByteCount = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    RequestKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ordinal = table.Column<int>(type: "int", nullable: false),
                    Available = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    FKUserUpdater = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SY_PatientDocuments", x => x.PKPatientDocument)
                        .Annotation("SqlServer:Clustered", true);
                    table.CheckConstraint("CK_PatientDocuments_Kind", "[Kind] BETWEEN 0 AND 4");
                    table.CheckConstraint("CK_PatientDocuments_Ordinal", "[Ordinal] BETWEEN 0 AND 4");
                    table.CheckConstraint("CK_PatientDocuments_Size", "[ByteCount] BETWEEN 1 AND 10485760");
                    table.ForeignKey(
                        name: "FK_SY_PatientDocuments_SC_Users_FKUserUpdater",
                        column: x => x.FKUserUpdater,
                        principalSchema: "dbo",
                        principalTable: "SC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SY_PatientDocuments_SY_ClinicalVisits_FKClinicalVisit",
                        column: x => x.FKClinicalVisit,
                        principalSchema: "medic",
                        principalTable: "SY_ClinicalVisits",
                        principalColumn: "PKClinicalVisit",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SY_PatientDocuments_SY_People_FKPerson",
                        column: x => x.FKPerson,
                        principalSchema: "dbo",
                        principalTable: "SY_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CT_StockItems_Code",
                schema: "medic",
                table: "CT_StockItems",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CT_StockItems_FKUserUpdater",
                schema: "medic",
                table: "CT_StockItems",
                column: "FKUserUpdater");

            migrationBuilder.CreateIndex(
                name: "IX_IX_StockMovements_FKStockItem_PKStockMovement",
                schema: "medic",
                table: "IX_StockMovements",
                columns: new[] { "FKStockItem", "PKStockMovement" });

            migrationBuilder.CreateIndex(
                name: "IX_IX_StockMovements_FKUserUpdater",
                schema: "medic",
                table: "IX_StockMovements",
                column: "FKUserUpdater");

            migrationBuilder.CreateIndex(
                name: "IX_IX_StockMovements_RequestKey",
                schema: "medic",
                table: "IX_StockMovements",
                column: "RequestKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SY_ClinicalVisits_FKPerson_VisitedAt",
                schema: "medic",
                table: "SY_ClinicalVisits",
                columns: new[] { "FKPerson", "VisitedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SY_ClinicalVisits_FKUserUpdater",
                schema: "medic",
                table: "SY_ClinicalVisits",
                column: "FKUserUpdater");

            migrationBuilder.CreateIndex(
                name: "IX_SY_ClinicalVisits_RequestKey",
                schema: "medic",
                table: "SY_ClinicalVisits",
                column: "RequestKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SY_PatientDocuments_FKClinicalVisit",
                schema: "medic",
                table: "SY_PatientDocuments",
                column: "FKClinicalVisit");

            migrationBuilder.CreateIndex(
                name: "IX_SY_PatientDocuments_FKPerson_LastUpdated",
                schema: "medic",
                table: "SY_PatientDocuments",
                columns: new[] { "FKPerson", "LastUpdated" });

            migrationBuilder.CreateIndex(
                name: "IX_SY_PatientDocuments_FKPerson_RequestKey_Ordinal",
                schema: "medic",
                table: "SY_PatientDocuments",
                columns: new[] { "FKPerson", "RequestKey", "Ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SY_PatientDocuments_FKUserUpdater",
                schema: "medic",
                table: "SY_PatientDocuments",
                column: "FKUserUpdater");

            migrationBuilder.CreateIndex(
                name: "IX_SY_PatientDocuments_StorageKey",
                schema: "medic",
                table: "SY_PatientDocuments",
                column: "StorageKey",
                unique: true);
            migrationBuilder.Sql("IF DATABASE_PRINCIPAL_ID(N'MedicAppRuntime') IS NOT NULL BEGIN DENY UPDATE, DELETE ON OBJECT::medic.SY_ClinicalVisits TO MedicAppRuntime; DENY UPDATE, DELETE ON OBJECT::medic.SY_PatientDocuments TO MedicAppRuntime; DENY UPDATE, DELETE ON OBJECT::medic.IX_StockMovements TO MedicAppRuntime; END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IX_StockMovements",
                schema: "medic");

            migrationBuilder.DropTable(
                name: "SY_PatientDocuments",
                schema: "medic");

            migrationBuilder.DropTable(
                name: "CT_StockItems",
                schema: "medic");

            migrationBuilder.DropTable(
                name: "SY_ClinicalVisits",
                schema: "medic");

            migrationBuilder.DropColumn(
                name: "ManageClinicalRecords",
                schema: "medic",
                table: "SC_AccessProfiles");

            migrationBuilder.DropColumn(
                name: "ManageInventory",
                schema: "medic",
                table: "SC_AccessProfiles");
        }
    }
}
