// AutoCheck / INTEC | SRS 1.1 | CU-04 / IC-03 | Version 1.0 | History: Git.
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AutoCheck.Api.Infrastructure.Data.Migrations;

[DbContext(typeof(AutoCheckDb))]
[Migration("20261122220000_PreservePaymentEventResponses")]
public sealed class PreservePaymentEventResponses : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<string>("ResponseJson", "PaymentEvent", type: "text", schema: "autocheck", nullable: true);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Payment event evidence requires a forward migration.");
}
