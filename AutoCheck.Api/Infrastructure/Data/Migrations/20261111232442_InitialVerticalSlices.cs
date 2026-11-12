using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AutoCheck.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialVerticalSlices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "autocheck");

            migrationBuilder.CreateTable(
                name: "AccountLock",
                schema: "autocheck",
                columns: table => new
                {
                    Email = table.Column<string>(type: "text", nullable: false),
                    Failures = table.Column<int>(type: "integer", nullable: false),
                    LockedUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountLock", x => x.Email);
                });

            migrationBuilder.CreateTable(
                name: "AggregationState",
                schema: "autocheck",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Updated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AggregationState", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnomalyAlert",
                schema: "autocheck",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InspectorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CenterId = table.Column<Guid>(type: "uuid", nullable: false),
                    FinalInspectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    InspectionIds = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    RampIds = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    From = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    To = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Approved = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnomalyAlert", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditEvent",
                schema: "autocheck",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Operation = table.Column<string>(type: "text", nullable: false),
                    Entity = table.Column<string>(type: "text", nullable: false),
                    Outcome = table.Column<string>(type: "text", nullable: false),
                    Correlation = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEvent", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CatalogItem",
                schema: "autocheck",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogItem", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Center",
                schema: "autocheck",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Address = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Center", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DailyMetric",
                schema: "autocheck",
                columns: table => new
                {
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    CenterId = table.Column<Guid>(type: "uuid", nullable: false),
                    RampId = table.Column<Guid>(type: "uuid", nullable: false),
                    InspectorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Approved = table.Column<int>(type: "integer", nullable: false),
                    Rejected = table.Column<int>(type: "integer", nullable: false),
                    Updated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyMetric", x => new { x.Day, x.CenterId, x.RampId, x.InspectorId });
                });

            migrationBuilder.CreateTable(
                name: "IdempotencyRecord",
                schema: "autocheck",
                columns: table => new
                {
                    Key = table.Column<string>(type: "text", nullable: false),
                    Fingerprint = table.Column<string>(type: "text", nullable: false),
                    Json = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Location = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyRecord", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "InspectionRule",
                schema: "autocheck",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<string>(type: "text", nullable: false),
                    MaxMinor = table.Column<int>(type: "integer", nullable: false),
                    Effective = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionRule", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessage",
                schema: "autocheck",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    AggregateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Available = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Completed = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessage", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CenterSchedule",
                schema: "autocheck",
                columns: table => new
                {
                    CenterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Day = table.Column<int>(type: "integer", nullable: false),
                    Opens = table.Column<int>(type: "integer", nullable: false),
                    Closes = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CenterSchedule", x => new { x.CenterId, x.Day });
                    table.ForeignKey(
                        name: "FK_CenterSchedule_Center_CenterId",
                        column: x => x.CenterId,
                        principalSchema: "autocheck",
                        principalTable: "Center",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Ramp",
                schema: "autocheck",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CenterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ramp", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ramp_Center_CenterId",
                        column: x => x.CenterId,
                        principalSchema: "autocheck",
                        principalTable: "Center",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserAccount",
                schema: "autocheck",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Phone = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    Permissions = table.Column<string[]>(type: "text[]", nullable: false),
                    CenterId = table.Column<Guid>(type: "uuid", nullable: true),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAccount", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserAccount_Center_CenterId",
                        column: x => x.CenterId,
                        principalSchema: "autocheck",
                        principalTable: "Center",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BookingBlock",
                schema: "autocheck",
                columns: table => new
                {
                    RampId = table.Column<Guid>(type: "uuid", nullable: false),
                    Starts = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AttemptId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    Expires = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingBlock", x => new { x.RampId, x.Starts });
                    table.ForeignKey(
                        name: "FK_BookingBlock_Ramp_RampId",
                        column: x => x.RampId,
                        principalSchema: "autocheck",
                        principalTable: "Ramp",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppSession",
                schema: "autocheck",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastActivity = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Revoked = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSession", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppSession_UserAccount_UserId",
                        column: x => x.UserId,
                        principalSchema: "autocheck",
                        principalTable: "UserAccount",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Vehicle",
                schema: "autocheck",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Plate = table.Column<string>(type: "text", nullable: false),
                    Vin = table.Column<string>(type: "text", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    ModelId = table.Column<Guid>(type: "uuid", nullable: false),
                    ColorId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vehicle", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Vehicle_CatalogItem_ColorId",
                        column: x => x.ColorId,
                        principalSchema: "autocheck",
                        principalTable: "CatalogItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Vehicle_CatalogItem_ModelId",
                        column: x => x.ModelId,
                        principalSchema: "autocheck",
                        principalTable: "CatalogItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Vehicle_UserAccount_OwnerId",
                        column: x => x.OwnerId,
                        principalSchema: "autocheck",
                        principalTable: "UserAccount",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentAttempt",
                schema: "autocheck",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uuid", nullable: false),
                    CenterId = table.Column<Guid>(type: "uuid", nullable: false),
                    RampId = table.Column<Guid>(type: "uuid", nullable: false),
                    Starts = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Expires = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    Scenario = table.Column<string>(type: "text", nullable: false),
                    PreviousInspectionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReversalId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentAttempt", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentAttempt_Vehicle_VehicleId",
                        column: x => x.VehicleId,
                        principalSchema: "autocheck",
                        principalTable: "Vehicle",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Appointment",
                schema: "autocheck",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uuid", nullable: false),
                    CenterId = table.Column<Guid>(type: "uuid", nullable: false),
                    RampId = table.Column<Guid>(type: "uuid", nullable: false),
                    InspectorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Starts = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    PreviousInspectionId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Appointment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Appointment_PaymentAttempt_AttemptId",
                        column: x => x.AttemptId,
                        principalSchema: "autocheck",
                        principalTable: "PaymentAttempt",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Appointment_UserAccount_InspectorId",
                        column: x => x.InspectorId,
                        principalSchema: "autocheck",
                        principalTable: "UserAccount",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentEvent",
                schema: "autocheck",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    AttemptId = table.Column<Guid>(type: "uuid", nullable: false),
                    Outcome = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentEvent", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentEvent_PaymentAttempt_AttemptId",
                        column: x => x.AttemptId,
                        principalSchema: "autocheck",
                        principalTable: "PaymentAttempt",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Inspection",
                schema: "autocheck",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uuid", nullable: false),
                    CenterId = table.Column<Guid>(type: "uuid", nullable: false),
                    RampId = table.Column<Guid>(type: "uuid", nullable: false),
                    InspectorId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousId = table.Column<Guid>(type: "uuid", nullable: true),
                    Started = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Closed = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    State = table.Column<string>(type: "text", nullable: false),
                    Result = table.Column<string>(type: "text", nullable: true),
                    RuleVersion = table.Column<string>(type: "text", nullable: false),
                    MaxMinor = table.Column<int>(type: "integer", nullable: false),
                    Defects = table.Column<string>(type: "jsonb", nullable: true),
                    Minor = table.Column<int>(type: "integer", nullable: false),
                    Medium = table.Column<int>(type: "integer", nullable: false),
                    Serious = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    MongoId = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    VehicleSnapshot = table.Column<string>(type: "text", nullable: false),
                    CenterSnapshot = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inspection", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Inspection_Appointment_AppointmentId",
                        column: x => x.AppointmentId,
                        principalSchema: "autocheck",
                        principalTable: "Appointment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Inspection_Inspection_PreviousId",
                        column: x => x.PreviousId,
                        principalSchema: "autocheck",
                        principalTable: "Inspection",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Certificate",
                schema: "autocheck",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InspectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Issued = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    Manifest = table.Column<string>(type: "text", nullable: false),
                    ManifestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Pdf = table.Column<byte[]>(type: "bytea", nullable: true),
                    PdfHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Certificate", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Certificate_Inspection_InspectionId",
                        column: x => x.InspectionId,
                        principalSchema: "autocheck",
                        principalTable: "Inspection",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "autocheck",
                table: "CatalogItem",
                columns: new[] { "Id", "Name", "ParentId", "Type" },
                values: new object[,]
                {
                    { new Guid("20000000-0000-0000-0000-000000000001"), "Toyota", null, "marcas" },
                    { new Guid("20000000-0000-0000-0000-000000000002"), "Corolla", new Guid("20000000-0000-0000-0000-000000000001"), "modelos" },
                    { new Guid("20000000-0000-0000-0000-000000000003"), "Blanco", null, "colores" },
                    { new Guid("20000000-0000-0000-0000-000000000004"), "Liviano", null, "categorias" }
                });

            migrationBuilder.InsertData(
                schema: "autocheck",
                table: "InspectionRule",
                columns: new[] { "Id", "ActorId", "Effective", "MaxMinor", "Version" },
                values: new object[] { new Guid("10000000-0000-0000-0000-000000000001"), null, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 3, "V1" });

            migrationBuilder.CreateIndex(
                name: "IX_AnomalyAlert_InspectorId_CenterId_FinalInspectionId",
                schema: "autocheck",
                table: "AnomalyAlert",
                columns: new[] { "InspectorId", "CenterId", "FinalInspectionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Appointment_AttemptId",
                schema: "autocheck",
                table: "Appointment",
                column: "AttemptId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Appointment_CenterId_Starts",
                schema: "autocheck",
                table: "Appointment",
                columns: new[] { "CenterId", "Starts" });

            migrationBuilder.CreateIndex(
                name: "IX_Appointment_InspectorId",
                schema: "autocheck",
                table: "Appointment",
                column: "InspectorId");

            migrationBuilder.CreateIndex(
                name: "IX_AppSession_UserId",
                schema: "autocheck",
                table: "AppSession",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvent_At",
                schema: "autocheck",
                table: "AuditEvent",
                column: "At");

            migrationBuilder.CreateIndex(
                name: "IX_Certificate_InspectionId",
                schema: "autocheck",
                table: "Certificate",
                column: "InspectionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Inspection_AppointmentId",
                schema: "autocheck",
                table: "Inspection",
                column: "AppointmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Inspection_InspectorId_CenterId_Closed_Id",
                schema: "autocheck",
                table: "Inspection",
                columns: new[] { "InspectorId", "CenterId", "Closed", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Inspection_PreviousId",
                schema: "autocheck",
                table: "Inspection",
                column: "PreviousId");

            migrationBuilder.CreateIndex(
                name: "IX_Inspection_VehicleId_Closed",
                schema: "autocheck",
                table: "Inspection",
                columns: new[] { "VehicleId", "Closed" });

            migrationBuilder.CreateIndex(
                name: "IX_InspectionRule_Effective",
                schema: "autocheck",
                table: "InspectionRule",
                column: "Effective",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InspectionRule_Version",
                schema: "autocheck",
                table: "InspectionRule",
                column: "Version",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_Completed_Available",
                schema: "autocheck",
                table: "OutboxMessage",
                columns: new[] { "Completed", "Available" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_Kind_AggregateId",
                schema: "autocheck",
                table: "OutboxMessage",
                columns: new[] { "Kind", "AggregateId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAttempt_UserId",
                schema: "autocheck",
                table: "PaymentAttempt",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAttempt_VehicleId",
                schema: "autocheck",
                table: "PaymentAttempt",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEvent_AttemptId",
                schema: "autocheck",
                table: "PaymentEvent",
                column: "AttemptId");

            migrationBuilder.CreateIndex(
                name: "IX_Ramp_CenterId",
                schema: "autocheck",
                table: "Ramp",
                column: "CenterId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAccount_AuthId",
                schema: "autocheck",
                table: "UserAccount",
                column: "AuthId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserAccount_CenterId",
                schema: "autocheck",
                table: "UserAccount",
                column: "CenterId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAccount_Email",
                schema: "autocheck",
                table: "UserAccount",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vehicle_ColorId",
                schema: "autocheck",
                table: "Vehicle",
                column: "ColorId");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicle_ModelId",
                schema: "autocheck",
                table: "Vehicle",
                column: "ModelId");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicle_OwnerId",
                schema: "autocheck",
                table: "Vehicle",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicle_Plate",
                schema: "autocheck",
                table: "Vehicle",
                column: "Plate",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vehicle_Vin",
                schema: "autocheck",
                table: "Vehicle",
                column: "Vin",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountLock",
                schema: "autocheck");

            migrationBuilder.DropTable(
                name: "AggregationState",
                schema: "autocheck");

            migrationBuilder.DropTable(
                name: "AnomalyAlert",
                schema: "autocheck");

            migrationBuilder.DropTable(
                name: "AppSession",
                schema: "autocheck");

            migrationBuilder.DropTable(
                name: "AuditEvent",
                schema: "autocheck");

            migrationBuilder.DropTable(
                name: "BookingBlock",
                schema: "autocheck");

            migrationBuilder.DropTable(
                name: "CenterSchedule",
                schema: "autocheck");

            migrationBuilder.DropTable(
                name: "Certificate",
                schema: "autocheck");

            migrationBuilder.DropTable(
                name: "DailyMetric",
                schema: "autocheck");

            migrationBuilder.DropTable(
                name: "IdempotencyRecord",
                schema: "autocheck");

            migrationBuilder.DropTable(
                name: "InspectionRule",
                schema: "autocheck");

            migrationBuilder.DropTable(
                name: "OutboxMessage",
                schema: "autocheck");

            migrationBuilder.DropTable(
                name: "PaymentEvent",
                schema: "autocheck");

            migrationBuilder.DropTable(
                name: "Ramp",
                schema: "autocheck");

            migrationBuilder.DropTable(
                name: "Inspection",
                schema: "autocheck");

            migrationBuilder.DropTable(
                name: "Appointment",
                schema: "autocheck");

            migrationBuilder.DropTable(
                name: "PaymentAttempt",
                schema: "autocheck");

            migrationBuilder.DropTable(
                name: "Vehicle",
                schema: "autocheck");

            migrationBuilder.DropTable(
                name: "CatalogItem",
                schema: "autocheck");

            migrationBuilder.DropTable(
                name: "UserAccount",
                schema: "autocheck");

            migrationBuilder.DropTable(
                name: "Center",
                schema: "autocheck");
        }
    }
}
