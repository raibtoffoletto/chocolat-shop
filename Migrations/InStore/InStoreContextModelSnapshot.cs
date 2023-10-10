﻿// <auto-generated />
using System;
using ChocolateStores.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ChocolateStores.Migrations.InStore
{
    [DbContext(typeof(InStoreContext))]
    partial class InStoreContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasDefaultSchema("public")
                .HasAnnotation("ProductVersion", "7.0.11")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("ChocolateStores.Models.InStore.Inventory", b =>
                {
                    b.Property<string>("Code")
                        .HasColumnType("text")
                        .HasColumnName("code");

                    b.Property<DateTime>("LastOrder")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("last_order");

                    b.Property<decimal>("Price")
                        .HasColumnType("numeric")
                        .HasColumnName("price");

                    b.Property<int>("Stock")
                        .HasColumnType("integer")
                        .HasColumnName("stock");

                    b.HasKey("Code");

                    b.ToTable("inventory", "public");
                });

            modelBuilder.Entity("ChocolateStores.Models.Product", b =>
                {
                    b.Property<string>("Code")
                        .HasColumnType("text")
                        .HasColumnName("code");

                    b.Property<bool>("IsDiscontinued")
                        .HasColumnType("boolean")
                        .HasColumnName("discontinued");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("name");

                    b.Property<decimal>("Price")
                        .HasColumnType("numeric")
                        .HasColumnName("price");

                    b.Property<string>("Type")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("type");

                    b.HasKey("Code");

                    b.ToTable("products", "hq", t =>
                        {
                            t.ExcludeFromMigrations();
                        });
                });
#pragma warning restore 612, 618
        }
    }
}
