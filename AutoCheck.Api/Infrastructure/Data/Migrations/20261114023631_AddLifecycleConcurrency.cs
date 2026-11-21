using System;
using System.Net;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoCheck.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLifecycleConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ArchivedAt",
                schema: "autocheck",
                table: "Vehicle",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                schema: "autocheck",
                table: "Vehicle",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ArchivedAt",
                schema: "autocheck",
                table: "UserAccount",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                schema: "autocheck",
                table: "UserAccount",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddColumn<string>(
                name: "After",
                schema: "autocheck",
                table: "AuditEvent",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Before",
                schema: "autocheck",
                table: "AuditEvent",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<IPAddress>(
                name: "Ip",
                schema: "autocheck",
                table: "AuditEvent",
                type: "inet",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vehicle_OwnerId_ArchivedAt_Id",
                schema: "autocheck",
                table: "Vehicle",
                columns: new[] { "OwnerId", "ArchivedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Certificate_OwnerId_Issued_Id",
                schema: "autocheck",
                table: "Certificate",
                columns: new[] { "OwnerId", "Issued", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvent_At_Id",
                schema: "autocheck",
                table: "AuditEvent",
                columns: new[] { "At", "Id" });
            migrationBuilder.Sql("""
                DO $acl$ BEGIN
                  IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'autocheck_api') THEN
                    REVOKE DELETE ON autocheck."UserAccount", autocheck."Vehicle" FROM autocheck_api;
                  END IF;
                END $acl$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Lifecycle tombstones and audit evidence require a forward migration.");
        }
    }
}
