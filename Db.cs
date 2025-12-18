using Microsoft.Data.Sqlite;
using System;
using System.IO;

namespace SubscriptionTracker.AvaloniaApp;

public static class Db
{
    public static readonly string CurrentDbPath =
        Path.Combine(Environment.CurrentDirectory, "subscriptions.db");

    private static readonly string _cs = $"Data Source={CurrentDbPath}";

    public static SqliteConnection Open()
    {
        var con = new SqliteConnection(_cs);
        con.Open();
        return con;
    }

    public static void Init()
    {
        using var con = Open();

        using (var cmd = con.CreateCommand())
        {
            cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS Members (
    MemberId TEXT PRIMARY KEY,
    Name     TEXT NOT NULL,
    JoinDate TEXT NOT NULL,
    Plan     TEXT NOT NULL,
    NameKey  TEXT
);

CREATE TABLE IF NOT EXISTS Payments (
    PaymentId   INTEGER PRIMARY KEY AUTOINCREMENT,
    MemberId    TEXT NOT NULL,
    PaymentDate TEXT NOT NULL,
    FOREIGN KEY (MemberId) REFERENCES Members(MemberId)
);

CREATE TABLE IF NOT EXISTS Appointments (
    AppointmentId INTEGER PRIMARY KEY AUTOINCREMENT,
    MemberId TEXT NOT NULL,
    ApptAt   TEXT NOT NULL, -- ISO: yyyy-MM-dd HH:mm
    FOREIGN KEY (MemberId) REFERENCES Members(MemberId)
);

CREATE INDEX IF NOT EXISTS IX_Payments_MemberId_Date
ON Payments(MemberId, PaymentDate);

CREATE INDEX IF NOT EXISTS IX_Appointments_ApptAt
ON Appointments(ApptAt);
";
            cmd.ExecuteNonQuery();
        }

        // Ensure NameKey column exists
        bool hasNameKey = false;
        using (var pragma = con.CreateCommand())
        {
            pragma.CommandText = "PRAGMA table_info(Members);";
            using var r = pragma.ExecuteReader();
            while (r.Read())
            {
                var col = r.GetString(1);
                if (string.Equals(col, "NameKey", StringComparison.OrdinalIgnoreCase))
                {
                    hasNameKey = true;
                    break;
                }
            }
        }

        if (!hasNameKey)
        {
            using var alter = con.CreateCommand();
            alter.CommandText = "ALTER TABLE Members ADD COLUMN NameKey TEXT;";
            alter.ExecuteNonQuery();
        }

        // Backfill NameKey for any existing rows without it
        using (var sel = con.CreateCommand())
        {
            sel.CommandText = "SELECT MemberId, Name FROM Members WHERE NameKey IS NULL OR NameKey = '';";

            using var r = sel.ExecuteReader();
            while (r.Read())
            {
                var id = r.GetString(0);
                var name = r.GetString(1);
                var key = NameKeyUtil.HashNormalized(name);

                using var upd = con.CreateCommand();
                upd.CommandText = "UPDATE Members SET NameKey=@k WHERE MemberId=@id;";
                upd.Parameters.AddWithValue("@k", key);
                upd.Parameters.AddWithValue("@id", id);
                upd.ExecuteNonQuery();
            }
        }

        using (var ux = con.CreateCommand())
        {
            ux.CommandText = "CREATE UNIQUE INDEX IF NOT EXISTS UX_Members_NameKey ON Members(NameKey);";
            ux.ExecuteNonQuery();
        }
    }
}
