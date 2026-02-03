using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SafeCare.Migrations
{
    /// <inheritdoc />
    public partial class AddIncidentReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IncidentReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Surname = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Phone = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    Email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PatientName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PatientSurname = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PatientDob = table.Column<DateTime>(type: "date", nullable: true),
                    PatientGender = table.Column<string>(type: "text", nullable: false),
                    DateFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DateTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DepartmentId = table.Column<int>(type: "integer", nullable: false),
                    OtherIncidentDefinition = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    IncidentDescription = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncidentReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IncidentReports_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IncidentDefinitionIncidentReport",
                columns: table => new
                {
                    IncidentDefinitionsId = table.Column<int>(type: "integer", nullable: false),
                    ReportsWithIncidentId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncidentDefinitionIncidentReport", x => new { x.IncidentDefinitionsId, x.ReportsWithIncidentId });
                    table.ForeignKey(
                        name: "FK_IncidentDefinitionIncidentReport_IncidentDefinitions_Incide~",
                        column: x => x.IncidentDefinitionsId,
                        principalTable: "IncidentDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IncidentDefinitionIncidentReport_IncidentReports_ReportsWit~",
                        column: x => x.ReportsWithIncidentId,
                        principalTable: "IncidentReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IncidentDefinitionIncidentReport_ReportsWithIncidentId",
                table: "IncidentDefinitionIncidentReport",
                column: "ReportsWithIncidentId");

            migrationBuilder.CreateIndex(
                name: "IX_IncidentReports_DepartmentId",
                table: "IncidentReports",
                column: "DepartmentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IncidentDefinitionIncidentReport");

            migrationBuilder.DropTable(
                name: "IncidentReports");
        }
    }
}
