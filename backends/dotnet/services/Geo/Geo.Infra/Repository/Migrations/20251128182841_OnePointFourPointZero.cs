// -----------------------------------------------------------------------
// <copyright file="20251128182841_OnePointFourPointZero.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

#nullable disable

namespace D2.Geo.Infra.Repository.Migrations
{
    using System;
    using Microsoft.EntityFrameworkCore.Migrations;

    /// <inheritdoc />
    public partial class OnePointFourPointZero : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "reference_data_version",
                keyColumn: "id",
                keyValue: 0,
                columns: new[] { "updated_at", "version" },
                values: new object[] { new DateTime(2025, 11, 28, 0, 0, 0, 0, DateTimeKind.Utc), "1.4.0" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "reference_data_version",
                keyColumn: "id",
                keyValue: 0,
                columns: new[] { "updated_at", "version" },
                values: new object[] { new DateTime(2025, 11, 24, 10, 0, 0, 0, DateTimeKind.Utc), "1.2.0" });
        }
    }
}
