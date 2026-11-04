using System;
using Common.SQLitelib;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clipboard.Desktop.Migrations;

[DbContext(typeof(SQLiteDBContext))]
internal class SQLiteDBContextModelSnapshot : ModelSnapshot
{
	protected override void BuildModel(ModelBuilder modelBuilder)
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
