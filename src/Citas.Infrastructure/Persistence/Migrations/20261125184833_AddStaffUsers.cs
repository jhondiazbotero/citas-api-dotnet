using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Citas.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "staff_users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    professional_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_staff_users", x => x.id);
                    table.CheckConstraint("ck_staff_users_professional_link", "(role = 'Professional') = (professional_id IS NOT NULL)");
                    table.CheckConstraint("ck_staff_users_role", "role IN ('Admin', 'Reception', 'Professional')");
                    table.ForeignKey(
                        name: "fk_staff_users_professionals_professional_id",
                        column: x => x.professional_id,
                        principalTable: "professionals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_staff_users_email",
                table: "staff_users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_staff_users_professional_id",
                table: "staff_users",
                column: "professional_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "staff_users");
        }
    }
}
