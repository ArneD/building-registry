﻿using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuildingRegistry.Consumer.Read.Parcel.Migrations
{
    using BuildingRegistry.Infrastructure;
    using Parcel;

    public partial class AddSpatialIndexOnGeometry : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@$"CREATE SPATIAL INDEX [SPATIAL_ParcelItems_Geometry] ON [{Schema.ConsumerReadParcel}].[ParcelItems] ([Geometry])
         USING GEOMETRY_GRID
         WITH (
          BOUNDING_BOX =(22279.17, 153050.23, 258873.3, 244022.31),
          GRIDS =(
           LEVEL_1 = MEDIUM,
           LEVEL_2 = MEDIUM,
           LEVEL_3 = MEDIUM,
           LEVEL_4 = MEDIUM),
         CELLS_PER_OBJECT = 5)");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@$"
            DROP INDEX [SPATIAL_ParcelItems_Geometry] ON [{Schema.ConsumerReadParcel}].[ParcelItems]");
        }
    }
}
