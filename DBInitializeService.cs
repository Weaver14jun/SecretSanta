using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmartAnalytics.SecretSanta.Services.Services.Base;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Data.SQLite;

namespace SmartAnalytics.SecretSanta.Services.Services
{
    public class DBInitializeService : BaseService<DBInitializeService>
    {
        public DBInitializeService(IConfiguration configuration, ILogger<DBInitializeService> logger)
            : base(configuration, logger)
        {
        }
        public void InitializeeDB()
        {
            string dbPath = _configuration.GetSection("DBPath").Get<string>();
            if (!File.Exists(dbPath))
            {
                SQLiteConnection.CreateFile(dbPath);
            }
            SQLiteConnection dbConnection = new SQLiteConnection($"Data Source={dbPath};");
            dbConnection.Open();
            string createMembersSql = @"CREATE TABLE ""Members"" (
    ""Id""    INTEGER NOT NULL UNIQUE,
    ""UserName""  TEXT,
	""TargetUser""    INTEGER,
	""IsAdmin""   INTEGER,
	""Wishes""    TEXT,
	""AntiWishes""    TEXT,
	""UserId""    TEXT,
	""UserStatus""    INTEGER,
	""ImgUrl""    TEXT,
	""TargetUserStatus""  INTEGER NOT NULL,
	PRIMARY KEY(""Id"")
)";
            SQLiteCommand command = new SQLiteCommand(createMembersSql, dbConnection);
            command.ExecuteNonQuery();
            string createNotificationsSql = @"CREATE TABLE ""Notifications"" (
    ""Id""    INTEGER NOT NULL,
	""UserId""    INTEGER NOT NULL,
	""Messege""   TEXT NOT NULL,
	""Viewed""    INTEGER,
	""Created""   TEXT,
	""Title"" TEXT,
	PRIMARY KEY(""Id""),
	FOREIGN KEY(""UserId"") REFERENCES ""Members""(""Id"")
)";
            command = new SQLiteCommand(createNotificationsSql, dbConnection);
            command.ExecuteNonQuery();
            dbConnection.Close();
        }
    }
}
