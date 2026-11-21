using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AutoCheck.Api.Infrastructure.Data.Migrations;

[DbContext(typeof(AutoCheckDb))]
[Migration("20261120200100_AddOutboxFailureState")]
public sealed class AddOutboxFailureState : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE autocheck."OutboxMessage"
            ADD COLUMN IF NOT EXISTS "FailedAt" timestamp with time zone,
            ADD COLUMN IF NOT EXISTS "FailureCode" text;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Outbox failure evidence requires a forward migration.");
}
