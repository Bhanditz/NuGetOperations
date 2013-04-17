﻿using System;
using System.Data.SqlClient;
using Dapper;

namespace NuGetGallery.Operations
{
    [Command("backupdatabase", "Backs up the database", AltName = "bdb", MaxArgs = 0)]
    public class BackupDatabaseTask : DatabaseTask, IBackupDatabase
    {
        [Option("Backup should occur if the database is older than X minutes (default 30 minutes)")]
        public int IfOlderThan { get; set; } 

        public string BackupName { get; private set; }

        public bool SkippingBackup { get; private set; }

        public BackupDatabaseTask()
        {
            IfOlderThan = 30;
        }

        public override void ExecuteCommand()
        {
            var dbServer = ConnectionString.DataSource;
            var dbName = ConnectionString.InitialCatalog;
            var masterConnectionString = Util.GetMasterConnectionString(ConnectionString.ConnectionString);

            Log.Trace("Connecting to server '{0}' to back up database '{1}'.", dbServer, dbName);

            SkippingBackup = false;

            using (var db = new SqlConnection(masterConnectionString))
            {
                db.Open();

                Log.Trace("Checking for a backup in progress.");
                if (Util.BackupIsInProgress(db))
                {
                    Log.Trace("Found a backup in progress; exiting.");
                    return;
                }

                Log.Trace("Found no backup in progress.");

                Log.Trace("Getting last backup time.");
                var lastBackupTime = Util.GetLastBackupTime(db);
                if (lastBackupTime >= DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(IfOlderThan)))
                {
                    Log.Info("Skipping Backup. Last Backup was less than {0} minutes ago", IfOlderThan);

                    SkippingBackup = true;

                    return;
                }
                Log.Trace("Last backup time is more than {0} minutes ago. Starting new backup.", IfOlderThan);

                var timestamp = Util.GetTimestamp();

                BackupName = string.Format("Backup_{0}", timestamp);

                db.Execute(string.Format("CREATE DATABASE {0} AS COPY OF {1}", BackupName, dbName));

                Log.Info("Starting '{0}'", BackupName);
            }
        }
    }
}
