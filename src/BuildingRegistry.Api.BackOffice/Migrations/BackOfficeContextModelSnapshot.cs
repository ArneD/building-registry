// <auto-generated />
using BuildingRegistry.Api.BackOffice;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace BuildingRegistry.Api.BackOffice.Migrations
{
    [DbContext(typeof(BackOfficeContext))]
    partial class BackOfficeContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "6.0.3")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder, 1L, 1);

            modelBuilder.Entity("BuildingRegistry.Api.BackOffice.BuildingUnitBuilding", b =>
                {
                    b.Property<int>("BuildingUnitPersistentLocalId")
                        .HasColumnType("int");

                    b.Property<int>("BuildingPersistentLocalId")
                        .HasColumnType("int");

                    b.HasKey("BuildingUnitPersistentLocalId");

                    SqlServerKeyBuilderExtensions.IsClustered(b.HasKey("BuildingUnitPersistentLocalId"));

                    b.ToTable("BuildingUnitBuilding", "BuildingRegistryBackOffice");
                });
#pragma warning restore 612, 618
        }
    }
}