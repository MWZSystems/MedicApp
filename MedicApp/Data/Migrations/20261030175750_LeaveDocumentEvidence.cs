using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedicApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class LeaveDocumentEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SY_LeaveDocuments",
                schema: "medic",
                columns: table => new
                {
                    PKLeaveDocument = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FKMedicalLeave = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_SY_LeaveDocuments", x => x.PKLeaveDocument)
                        .Annotation("SqlServer:Clustered", true);
                    table.CheckConstraint("CK_LeaveDocuments_Ordinal", "[Ordinal] BETWEEN 0 AND 4");
                    table.CheckConstraint("CK_LeaveDocuments_Size", "[ByteCount] BETWEEN 1 AND 10485760");
                    table.ForeignKey(
                        name: "FK_SY_LeaveDocuments_SC_Users_FKUserUpdater",
                        column: x => x.FKUserUpdater,
                        principalSchema: "dbo",
                        principalTable: "SC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SY_LeaveDocuments_SY_MedicalLeaves_FKMedicalLeave",
                        column: x => x.FKMedicalLeave,
                        principalSchema: "medic",
                        principalTable: "SY_MedicalLeaves",
                        principalColumn: "PKMedicalLeave",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SY_LeaveDocuments_FKMedicalLeave_LastUpdated",
                schema: "medic",
                table: "SY_LeaveDocuments",
                columns: new[] { "FKMedicalLeave", "LastUpdated" });

            migrationBuilder.CreateIndex(
                name: "IX_SY_LeaveDocuments_FKMedicalLeave_RequestKey_Ordinal",
                schema: "medic",
                table: "SY_LeaveDocuments",
                columns: new[] { "FKMedicalLeave", "RequestKey", "Ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SY_LeaveDocuments_FKUserUpdater",
                schema: "medic",
                table: "SY_LeaveDocuments",
                column: "FKUserUpdater");

            migrationBuilder.CreateIndex(
                name: "IX_SY_LeaveDocuments_StorageKey",
                schema: "medic",
                table: "SY_LeaveDocuments",
                column: "StorageKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SY_LeaveDocuments",
                schema: "medic");
        }
    }
}
