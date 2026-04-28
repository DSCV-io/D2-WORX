// -----------------------------------------------------------------------
// <copyright file="20260201111836_FixProfessionalCredentialsMapping.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

#nullable disable

namespace D2.Geo.Infra.Repository.Migrations
{
    using System.Collections.Immutable;
    using Microsoft.EntityFrameworkCore.Migrations;

    /// <inheritdoc />
    public partial class FixProfessionalCredentialsMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string[]>(
                name: "personal_professional_credentials",
                table: "contacts",
                type: "text[]",
                nullable: true,
                oldClrType: typeof(ImmutableList<string>),
                oldType: "character varying(50)[]",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<ImmutableList<string>>(
                name: "personal_professional_credentials",
                table: "contacts",
                type: "character varying(50)[]",
                nullable: true,
                oldClrType: typeof(string[]),
                oldType: "text[]",
                oldNullable: true);
        }
    }
}
