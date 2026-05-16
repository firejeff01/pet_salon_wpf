using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetSalon.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPhysicalExaminationNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "owners",
                columns: table => new
                {
                    OwnerId = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    NationalId = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Address = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    EmergencyContactName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    EmergencyContactPhone = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    EmergencyContactRelationship = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    PreferredAnimalHospitalName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PreferredAnimalHospitalPhone = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    PreferredAnimalHospitalAddress = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    IsStoredValueCustomer = table.Column<bool>(type: "INTEGER", nullable: false),
                    StoredValueBalance = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_owners", x => x.OwnerId);
                });

            migrationBuilder.CreateTable(
                name: "pets",
                columns: table => new
                {
                    PetId = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    OwnerId = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Species = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Breed = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Gender = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Age = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    IsNeutered = table.Column<bool>(type: "INTEGER", nullable: false),
                    ChipNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    UnregisteredIdMethod = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Personality = table.Column<string>(type: "TEXT", nullable: false),
                    phys_eyes = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    phys_ears = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    phys_teeth = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    phys_limbs = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    phys_skin = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    phys_fur = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    phys_eyes_note = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    phys_ears_note = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    phys_teeth_note = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    phys_limbs_note = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    phys_skin_note = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    phys_fur_note = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    MedicalHistory = table.Column<string>(type: "TEXT", nullable: false),
                    MedicalHistoryOther = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pets", x => x.PetId);
                    table.ForeignKey(
                        name: "FK_pets_owners_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "owners",
                        principalColumn: "OwnerId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "appointments",
                columns: table => new
                {
                    AppointmentId = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    OwnerId = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    PetId = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Time = table.Column<TimeOnly>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CancelReason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_appointments", x => x.AppointmentId);
                    table.ForeignKey(
                        name: "FK_appointments_owners_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "owners",
                        principalColumn: "OwnerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_appointments_pets_PetId",
                        column: x => x.PetId,
                        principalTable: "pets",
                        principalColumn: "PetId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "grooming_records",
                columns: table => new
                {
                    GroomingRecordId = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    AppointmentId = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    ServiceDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    ServiceTime = table.Column<TimeOnly>(type: "TEXT", nullable: false),
                    Services = table.Column<string>(type: "TEXT", nullable: false),
                    TotalCost = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    StoredValueDeduction = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    CashPayment = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    phys_eyes = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    phys_ears = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    phys_teeth = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    phys_limbs = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    phys_skin = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    phys_fur = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    phys_eyes_note = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    phys_ears_note = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    phys_teeth_note = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    phys_limbs_note = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    phys_skin_note = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    phys_fur_note = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    Personality = table.Column<string>(type: "TEXT", nullable: false),
                    MedicalHistory = table.Column<string>(type: "TEXT", nullable: false),
                    MedicalHistoryOther = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    OwnerNotes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    ShopNotes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    OtherNotes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    ContractPaths = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grooming_records", x => x.GroomingRecordId);
                    table.ForeignKey(
                        name: "FK_grooming_records_appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "appointments",
                        principalColumn: "AppointmentId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_appointments_Date",
                table: "appointments",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_appointments_OwnerId",
                table: "appointments",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_appointments_PetId_Date",
                table: "appointments",
                columns: new[] { "PetId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_grooming_records_AppointmentId",
                table: "grooming_records",
                column: "AppointmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_owners_Name",
                table: "owners",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_pets_Name",
                table: "pets",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_pets_OwnerId",
                table: "pets",
                column: "OwnerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "grooming_records");

            migrationBuilder.DropTable(
                name: "appointments");

            migrationBuilder.DropTable(
                name: "pets");

            migrationBuilder.DropTable(
                name: "owners");
        }
    }
}
