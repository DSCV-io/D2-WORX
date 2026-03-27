// -----------------------------------------------------------------------
// <copyright file="20260327052650_AddContactIANAIdentifier.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Infra.Repository.Migrations;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

/// <inheritdoc />
public partial class AddContactIANAIdentifier : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "iana_identifier",
            table: "contacts",
            type: "character varying(64)",
            maxLength: 64,
            nullable: false,
            defaultValue: "America/New_York");

        migrationBuilder.CreateIndex(
            name: "IX_contacts_iana_identifier",
            table: "contacts",
            column: "iana_identifier");

        migrationBuilder.AddForeignKey(
            name: "FK_contacts_timezones_iana_identifier",
            table: "contacts",
            column: "iana_identifier",
            principalTable: "timezones",
            principalColumn: "iana_identifier",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_contacts_timezones_iana_identifier",
            table: "contacts");

        migrationBuilder.DropIndex(
            name: "IX_contacts_iana_identifier",
            table: "contacts");

        migrationBuilder.DropColumn(
            name: "iana_identifier",
            table: "contacts");
    }
}
