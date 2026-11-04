using System;
using System.IO;
using System.Linq;
using Common.SQLitelib.DataModels;
using Microsoft.EntityFrameworkCore;

namespace Common.SQLitelib;

public class SQLiteDBContext : DbContext
{
	public static readonly string ConnectionString = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OpenClipboard", "database.db");

	public DbSet<ClipboardData> ClipboardData { get; set; }

	public DbSet<RecycleBin> RecycleBin { get; set; }

	public static SQLiteDBContext Instance { get; } = new SQLiteDBContext();


	public async void MigrateAsync()
	{
		await Instance.Database.MigrateAsync();
	}

	/// <summary>
	/// 彻底删除全部历史记录：同时清空 ClipboardData 与 RecycleBin，并用 VACUUM 回收磁盘页。
	/// </summary>
	public void ClearAllClipboardData()
	{
		// 先解除跟踪，避免后续操作与被 SQL 删除的行冲突
		foreach (var entry in ChangeTracker.Entries().ToList())
		{
			entry.State = EntityState.Detached;
		}

		Database.ExecuteSqlRaw("DELETE FROM \"ClipboardData\";");
		Database.ExecuteSqlRaw("DELETE FROM \"RecycleBin\";");
		Database.ExecuteSqlRaw("VACUUM;");
	}

	public SQLiteDBContext()
	{
		Database.MigrateAsync();
	}

	protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
	{
		DirectoryInfo directory = new FileInfo(ConnectionString).Directory;
		if (!directory.Exists)
		{
			directory.Create();
		}
		optionsBuilder.UseSqlite("Data Source=" + ConnectionString).UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
		base.OnConfiguring(optionsBuilder);
	}
}
