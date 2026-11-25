using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Citas.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // btree_gist lets a GiST index compare plain equality (uuid) and ranges in the same constraint.
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");

            migrationBuilder.CreateTable(
                name: "patients",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    document_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    phone = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_patients", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "professionals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    specialty = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_professionals", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "appointments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    professional_id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reason = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    cancellation_reason = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    cancelled_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    end_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    start_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_appointments", x => x.id);
                    table.CheckConstraint("ck_appointments_range", "end_at > start_at");
                    table.CheckConstraint("ck_appointments_status", "status IN ('Scheduled', 'Cancelled', 'Completed', 'NoShow')");
                    table.ForeignKey(
                        name: "fk_appointments_patients_patient_id",
                        column: x => x.patient_id,
                        principalTable: "patients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_appointments_professionals_professional_id",
                        column: x => x.professional_id,
                        principalTable: "professionals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "professional_working_hours",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    day_of_week = table.Column<int>(type: "integer", nullable: false),
                    from_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    to_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    professional_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_professional_working_hours", x => x.id);
                    table.CheckConstraint("ck_professional_working_hours_range", "from_time < to_time");
                    table.ForeignKey(
                        name: "fk_professional_working_hours_professionals_professional_id",
                        column: x => x.professional_id,
                        principalTable: "professionals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_appointments_patient_id",
                table: "appointments",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "ix_appointments_professional_id",
                table: "appointments",
                column: "professional_id");

            migrationBuilder.CreateIndex(
                name: "ix_patients_document_number",
                table: "patients",
                column: "document_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_professional_working_hours_professional_id",
                table: "professional_working_hours",
                column: "professional_id");

            // Agenda queries: one professional, start date between two days. EF cannot index complex type properties.
            migrationBuilder.Sql(
                "CREATE INDEX ix_appointments_professional_id_start_at ON appointments (professional_id, start_at);");

            // Protection against concurrent double booking: the database itself refuses two SCHEDULED appointments
            // of the same professional (or the same patient) whose ranges overlap. '[)' means a range that ends
            // exactly when another starts does not collide, the same rule as TimeRange.Overlaps in the domain.
            // Cancelled, completed and no-show appointments are outside the WHERE and never block anyone.
            migrationBuilder.Sql(
                """
                ALTER TABLE appointments
                    ADD CONSTRAINT ex_appointments_professional_overlap
                    EXCLUDE USING gist (professional_id WITH =, tsrange(start_at, end_at, '[)') WITH &&)
                    WHERE (status = 'Scheduled');
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE appointments
                    ADD CONSTRAINT ex_appointments_patient_overlap
                    EXCLUDE USING gist (patient_id WITH =, tsrange(start_at, end_at, '[)') WITH &&)
                    WHERE (status = 'Scheduled');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The exclusion constraints and the agenda index go away with the table.
            // btree_gist stays installed: other objects in the database may use it.
            migrationBuilder.DropTable(
                name: "appointments");

            migrationBuilder.DropTable(
                name: "professional_working_hours");

            migrationBuilder.DropTable(
                name: "patients");

            migrationBuilder.DropTable(
                name: "professionals");
        }
    }
}
