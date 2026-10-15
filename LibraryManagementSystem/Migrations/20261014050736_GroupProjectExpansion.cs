using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LibraryManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class GroupProjectExpansion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BorrowBranchId",
                table: "Loans",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BorrowChannel",
                table: "Loans",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FinePaidDate",
                table: "Loans",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReturnBranchId",
                table: "Loans",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrentBranchId",
                table: "Items",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HomeBranchId",
                table: "Items",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CardNumber",
                table: "Borrowers",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FailedPinAttempts",
                table: "Borrowers",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "HomeBranchId",
                table: "Borrowers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NotificationPreference",
                table: "Borrowers",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PinHash",
                table: "Borrowers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PinLockedUntil",
                table: "Borrowers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Branches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Address = table.Column<string>(type: "TEXT", maxLength: 250, nullable: true),
                    PhoneNumber = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    OpeningTime = table.Column<TimeOnly>(type: "TEXT", nullable: false),
                    ClosingTime = table.Column<TimeOnly>(type: "TEXT", nullable: false),
                    OpenOnWeekends = table.Column<bool>(type: "INTEGER", nullable: false),
                    OperatingStatus = table.Column<string>(type: "TEXT", nullable: false),
                    StatusNote = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Branches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BorrowerId = table.Column<int>(type: "INTEGER", nullable: true),
                    LoanId = table.Column<int>(type: "INTEGER", nullable: true),
                    ReservationId = table.Column<int>(type: "INTEGER", nullable: true),
                    Type = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Channel = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Recipient = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Subject = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DeliveryStatus = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    StatusDetail = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationLogs_Borrowers_BorrowerId",
                        column: x => x.BorrowerId,
                        principalTable: "Borrowers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "BranchTransfers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    FromBranchId = table.Column<int>(type: "INTEGER", nullable: false),
                    ToBranchId = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: true),
                    ReservationId = table.Column<int>(type: "INTEGER", nullable: true),
                    RequestedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RequestedBy = table.Column<string>(type: "TEXT", nullable: false),
                    DispatchedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DispatchedBy = table.Column<string>(type: "TEXT", nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ReceivedBy = table.Column<string>(type: "TEXT", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BranchTransfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BranchTransfers_Branches_FromBranchId",
                        column: x => x.FromBranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BranchTransfers_Branches_ToBranchId",
                        column: x => x.ToBranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BranchTransfers_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Reservations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    BorrowerId = table.Column<int>(type: "INTEGER", nullable: false),
                    PickupBranchId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    ReadyAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    HoldExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reservations_Borrowers_BorrowerId",
                        column: x => x.BorrowerId,
                        principalTable: "Borrowers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reservations_Branches_PickupBranchId",
                        column: x => x.PickupBranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reservations_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Loans_BorrowBranchId",
                table: "Loans",
                column: "BorrowBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Loans_ReturnBranchId",
                table: "Loans",
                column: "ReturnBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_CurrentBranchId",
                table: "Items",
                column: "CurrentBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_HomeBranchId",
                table: "Items",
                column: "HomeBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Borrowers_CardNumber",
                table: "Borrowers",
                column: "CardNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Borrowers_HomeBranchId",
                table: "Borrowers",
                column: "HomeBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Branches_Code",
                table: "Branches",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BranchTransfers_FromBranchId",
                table: "BranchTransfers",
                column: "FromBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_BranchTransfers_ItemId",
                table: "BranchTransfers",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_BranchTransfers_ToBranchId",
                table: "BranchTransfers",
                column: "ToBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationLogs_BorrowerId",
                table: "NotificationLogs",
                column: "BorrowerId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationLogs_Type_LoanId_CreatedAt",
                table: "NotificationLogs",
                columns: new[] { "Type", "LoanId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_BorrowerId",
                table: "Reservations",
                column: "BorrowerId");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_ItemId_Status_CreatedAt",
                table: "Reservations",
                columns: new[] { "ItemId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_PickupBranchId",
                table: "Reservations",
                column: "PickupBranchId");

            migrationBuilder.AddForeignKey(
                name: "FK_Borrowers_Branches_HomeBranchId",
                table: "Borrowers",
                column: "HomeBranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Items_Branches_CurrentBranchId",
                table: "Items",
                column: "CurrentBranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Items_Branches_HomeBranchId",
                table: "Items",
                column: "HomeBranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Loans_Branches_BorrowBranchId",
                table: "Loans",
                column: "BorrowBranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Loans_Branches_ReturnBranchId",
                table: "Loans",
                column: "ReturnBranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Borrowers_Branches_HomeBranchId",
                table: "Borrowers");

            migrationBuilder.DropForeignKey(
                name: "FK_Items_Branches_CurrentBranchId",
                table: "Items");

            migrationBuilder.DropForeignKey(
                name: "FK_Items_Branches_HomeBranchId",
                table: "Items");

            migrationBuilder.DropForeignKey(
                name: "FK_Loans_Branches_BorrowBranchId",
                table: "Loans");

            migrationBuilder.DropForeignKey(
                name: "FK_Loans_Branches_ReturnBranchId",
                table: "Loans");

            migrationBuilder.DropTable(
                name: "BranchTransfers");

            migrationBuilder.DropTable(
                name: "NotificationLogs");

            migrationBuilder.DropTable(
                name: "Reservations");

            migrationBuilder.DropTable(
                name: "Branches");

            migrationBuilder.DropIndex(
                name: "IX_Loans_BorrowBranchId",
                table: "Loans");

            migrationBuilder.DropIndex(
                name: "IX_Loans_ReturnBranchId",
                table: "Loans");

            migrationBuilder.DropIndex(
                name: "IX_Items_CurrentBranchId",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_Items_HomeBranchId",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_Borrowers_CardNumber",
                table: "Borrowers");

            migrationBuilder.DropIndex(
                name: "IX_Borrowers_HomeBranchId",
                table: "Borrowers");

            migrationBuilder.DropColumn(
                name: "BorrowBranchId",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "BorrowChannel",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "FinePaidDate",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "ReturnBranchId",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "CurrentBranchId",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "HomeBranchId",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "CardNumber",
                table: "Borrowers");

            migrationBuilder.DropColumn(
                name: "FailedPinAttempts",
                table: "Borrowers");

            migrationBuilder.DropColumn(
                name: "HomeBranchId",
                table: "Borrowers");

            migrationBuilder.DropColumn(
                name: "NotificationPreference",
                table: "Borrowers");

            migrationBuilder.DropColumn(
                name: "PinHash",
                table: "Borrowers");

            migrationBuilder.DropColumn(
                name: "PinLockedUntil",
                table: "Borrowers");
        }
    }
}
