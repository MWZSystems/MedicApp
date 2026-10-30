using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedicApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class RecurringDoctorCalendar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Generated",
                schema: "medic",
                table: "SY_AvailabilityWindows",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "UseRecurringSchedule",
                schema: "medic",
                table: "CT_Doctors",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Generated",
                schema: "medic",
                table: "SY_AvailabilityWindows");

            migrationBuilder.DropColumn(
                name: "UseRecurringSchedule",
                schema: "medic",
                table: "CT_Doctors");
        }
    }
}
