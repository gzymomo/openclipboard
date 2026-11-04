using System;
using Common.SQLitelib;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations.Builders;

namespace Clipboard.Desktop.Migrations;

[Migration("20201225093659_InitialCreate")]
[DbContext(typeof(SQLiteDBContext))]
public class InitialCreate : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.CreateTable("ClipboardData", (ColumnsBuilder table) => new
		{
			ID = table.Column<string>(),
			DataType = table.Column<int>(),
			Data = table.Column<string>(null, null, null, rowVersion: false, null, nullable: true),
			IsFavorite = table.Column<bool>(),
			IsDeleted = table.Column<bool>(),
			CreateTime = table.Column<DateTime>(),
			LastTime = table.Column<DateTime>(),
			DestructionTime = table.Column<DateTime>(),
			UsageCounter = table.Column<int>()
		}, null, table =>
		{
			table.PrimaryKey("PK_ClipboardData", x => x.ID);
		});
		migrationBuilder.CreateTable("RecycleBin", (ColumnsBuilder table) => new
		{
			ID = table.Column<string>(),
			DataType = table.Column<int>(),
			Data = table.Column<string>(null, null, null, rowVersion: false, null, nullable: true),
			IsFavorite = table.Column<bool>(),
			IsDeleted = table.Column<bool>(),
			CreateTime = table.Column<DateTime>(),
			LastTime = table.Column<DateTime>(),
			DestructionTime = table.Column<DateTime>(),
			UsageCounter = table.Column<int>()
		}, null, table =>
		{
			table.PrimaryKey("PK_RecycleBin", x => x.ID);
		});
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropTable("ClipboardData");
		migrationBuilder.DropTable("RecycleBin");
	}

	protected override void BuildTargetModel(ModelBuilder modelBuilder)
	{
		modelBuilder.HasAnnotation("ProductVersion", "3.1.10");
		modelBuilder.Entity("Common.SQLitelib.DataModels.ClipboardData", delegate(EntityTypeBuilder b)
		{
			b.Property<string>("ID").HasColumnType("TEXT");
			b.Property<DateTime>("CreateTime").HasColumnType("TEXT");
			b.Property<string>("Data").HasColumnType("TEXT");
			b.Property<int>("DataType").HasColumnType("INTEGER");
			b.Property<DateTime>("DestructionTime").HasColumnType("TEXT");
			b.Property<bool>("IsDeleted").HasColumnType("INTEGER");
			b.Property<bool>("IsFavorite").HasColumnType("INTEGER");
			b.Property<DateTime>("LastTime").HasColumnType("TEXT");
			b.Property<int>("UsageCounter").HasColumnType("INTEGER");
			b.HasKey("ID");
			b.ToTable("ClipboardData");
		});
		modelBuilder.Entity("Common.SQLitelib.DataModels.RecycleBin", delegate(EntityTypeBuilder b)
		{
			b.Property<string>("ID").HasColumnType("TEXT");
			b.Property<DateTime>("CreateTime").HasColumnType("TEXT");
			b.Property<string>("Data").HasColumnType("TEXT");
			b.Property<int>("DataType").HasColumnType("INTEGER");
			b.Property<DateTime>("DestructionTime").HasColumnType("TEXT");
			b.Property<bool>("IsDeleted").HasColumnType("INTEGER");
			b.Property<bool>("IsFavorite").HasColumnType("INTEGER");
			b.Property<DateTime>("LastTime").HasColumnType("TEXT");
			b.Property<int>("UsageCounter").HasColumnType("INTEGER");
			b.HasKey("ID");
			b.ToTable("RecycleBin");
		});
	}
}
