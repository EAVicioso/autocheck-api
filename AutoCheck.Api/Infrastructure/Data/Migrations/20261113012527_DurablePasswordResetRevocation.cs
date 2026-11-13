using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoCheck.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class DurablePasswordResetRevocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PasswordResetPending",
                schema: "autocheck",
                table: "UserAccount",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "SessionVersion",
                schema: "autocheck",
                table: "UserAccount",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "SecurityVersion",
                schema: "autocheck",
                table: "AppSession",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "PasswordResetOperation",
                schema: "autocheck",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Started = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Finished = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Correlation = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasswordResetOperation", x => x.Id);
                    table.CheckConstraint("valid_password_reset_state", "(\"State\" = 'PENDING' AND \"Finished\" IS NULL) OR (\"State\" IN ('COMPLETED', 'REJECTED', 'REVOKED_UNCERTAIN') AND \"Finished\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_PasswordResetOperation_UserAccount_UserId",
                        column: x => x.UserId,
                        principalSchema: "autocheck",
                        principalTable: "UserAccount",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetOperation_UserId",
                schema: "autocheck",
                table: "PasswordResetOperation",
                column: "UserId",
                unique: true,
                filter: "\"State\" = 'PENDING'");
            migrationBuilder.Sql("""
                DO $reset_permissions$ BEGIN
                  IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'autocheck_api') THEN
                    GRANT SELECT, INSERT, UPDATE ON autocheck."PasswordResetOperation" TO autocheck_api;
                    REVOKE DELETE ON autocheck."PasswordResetOperation" FROM autocheck_api;
                  END IF;
                END $reset_permissions$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Session revocation barriers require a reviewed forward migration.");
        }
    }
}
