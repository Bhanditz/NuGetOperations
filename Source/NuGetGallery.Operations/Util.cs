﻿using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Web;
using Dapper;
using Microsoft.WindowsAzure.Storage.Blob;

namespace NuGetGallery.Operations
{
    public static class Util
    {
        public const byte CopyingState = 7;
        public const byte OnlineState = 0;
        
        public static bool BackupIsInProgress(SqlConnection db)
        {
            return db.Query<Database>(
                "SELECT name, state FROM sys.databases WHERE name LIKE 'Backup_%' AND state = @state",
                new { state = CopyingState })
                .Any();
        }

        public static string DownloadPackage(
            CloudBlobContainer container,
            string id,
            string version,
            string folder)
        {
            var fileName = string.Format(
                "{0}.{1}.nupkg",
                id,
                version);
            var path = Path.Combine(folder, fileName);

            var blob = container.GetBlockBlobReference(fileName);
            blob.DownloadToFile(path);

            return path;
        }

        public static string GetDatabaseNameTimestamp(Database database)
        {
            return GetDatabaseNameTimestamp(database.Name);
        }

        public static string GetDatabaseNameTimestamp(string databaseName)
        {
            if (databaseName == null) throw new ArgumentNullException("databaseName");
            
            if (databaseName.Length < 14)
                throw new InvalidOperationException("Database name isn't long enough to contain a timestamp.");

            return databaseName.Substring(databaseName.Length - 14);
        }

        public static DateTime GetDateTimeFromTimestamp(string timestamp)
        {
            DateTime result;
            if (!DateTime.TryParseExact(timestamp, "yyyyMMddHHmmss", CultureInfo.CurrentCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out result))
            {
                result = DateTime.MinValue;
            }
            return result;
        }

        public static string GetDbName(string connectionString)
        {
            var connectionStringBuilder = new SqlConnectionStringBuilder(connectionString);
            return connectionStringBuilder.InitialCatalog;
        }

        public static string GetDbServer(string connectionString)
        {
            var connectionStringBuilder = new SqlConnectionStringBuilder(connectionString);
            return connectionStringBuilder.DataSource;
        }

        public static bool DatabaseExistsAndIsOnline(
            SqlConnection db,
            string restoreName)
        {
            var backupDbs = db.Query<Database>(
                "SELECT name, state FROM sys.databases WHERE name = @restoreName AND state = @state",
                new { restoreName, state = OnlineState })
                .OrderByDescending(database => database.Name);

            return backupDbs.FirstOrDefault() != null;
        }

        public static Database GetLastBackup(SqlConnection db)
        {
            var backupDbs = db.Query<Database>(
                "SELECT name, state FROM sys.databases WHERE name LIKE 'Backup_%' AND state = @state",
                new { state = OnlineState })
                .OrderByDescending(database => database.Name);

            return backupDbs.FirstOrDefault();
        }

        public static DateTime GetLastBackupTime(SqlConnection db)
        {
            var lastBackup = GetLastBackup(db);

            if (lastBackup == null)
                return DateTime.MinValue;

            var timestamp = lastBackup.Name.Substring(7);
            
            return GetDateTimeFromTimestamp(timestamp);
        }

        public static string GetMasterConnectionString(string connectionString)
        {
            var connectionStringBuilder = new SqlConnectionStringBuilder(connectionString) {InitialCatalog = "master"};
            return connectionStringBuilder.ToString();   
        }

        public static string GetConnectionString(string connectionString, string databaseName)
        {
            var connectionStringBuilder = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = databaseName };
            return connectionStringBuilder.ToString();
        }

        public static string GetOpsConnectionString(string connectionString)
        {
            var connectionStringBuilder = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "NuGetGalleryOps" };
            return connectionStringBuilder.ToString();
        }

        public static string GetTimestamp()
        {
            return DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        }

        internal static CloudBlobContainer GetPackageBackupsBlobContainer(CloudBlobClient blobClient)
        {
            var container = blobClient.GetContainerReference("packagebackups");
            container.CreateIfNotExists();
            container.SetPermissions(new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Off });
            return container;
        }

        internal static CloudBlobContainer GetPackagesBlobContainer(CloudBlobClient blobClient)
        {
            var container = blobClient.GetContainerReference("packages");
            return container;
        }

        internal static string GetPackageFileName(
            string id, 
            string version)
        {
            return string.Format(
                "{0}.{1}.nupkg",
                id.ToLowerInvariant(),
                version.ToLowerInvariant());
        }

        internal static string GetTempFolder()
        {
            string ret = Path.Combine(Path.GetTempPath(), "NuGetGallery.Operations");
            if (!Directory.Exists(ret))
            {
                Directory.CreateDirectory(ret);
            }

            return ret;
        }

        internal static string GetPackageBackupFileName(
            string id, 
            string version, 
            string hash)
        {
            var hashBytes = Convert.FromBase64String(hash);
            
            return string.Format(
                "{0}.{1}.{2}.nupkg",
                id,
                version,
                HttpServerUtility.UrlTokenEncode(hashBytes));
        }

        internal static ICloudBlob GetPackageFileBlob(
            CloudBlobContainer packagesBlobContainer, 
            string id, 
            string version)
        {
            var packageFileName = GetPackageFileName(
                id,
                version);
            return packagesBlobContainer.GetBlockBlobReference(packageFileName);
        }

        internal static Package GetPackage(
            SqlConnection db, 
            string id, 
            string version)
        {
            return db.Query<Package>(
                "SELECT p.[Key], pr.Id, p.Version, p.Hash FROM Packages p JOIN PackageRegistrations pr ON pr.[Key] = p.PackageRegistrationKey WHERE pr.Id = @id AND p.Version = @version",
                new { id, version }).SingleOrDefault();
        }

        internal static PackageRegistration GetPackageRegistration(
            SqlConnection db, 
            string id)
        {
            return db.Query<PackageRegistration>(
                "SELECT [Key], Id FROM PackageRegistrations WHERE Id = @id",
                new { id }).SingleOrDefault();
        }

        internal static IEnumerable<Package> GetPackages(
            SqlConnection db,
            int packageRegistrationKey)
        {
            return db.Query<Package>(
                "SELECT pr.Id, p.Version FROM Packages p JOIN PackageRegistrations PR on pr.[Key] = p.PackageRegistrationKey WHERE pr.[Key] = @packageRegistrationKey",
                new { packageRegistrationKey });
        }

        internal static User GetUser(
            SqlConnection db,
            string username)
        {
            var user = db.Query<User>(
                "SELECT u.[Key], u.Username, u.EmailAddress, u.UnconfirmedEmailAddress FROM Users u WHERE u.Username = @username",
                new { username }).SingleOrDefault();

            if (user != null)
            {
                user.PackageRegistrationIds = db.Query<string>(
                    "SELECT r.[Id] FROM PackageRegistrations r INNER JOIN PackageRegistrationOwners o ON o.PackageRegistrationKey = r.[Key] WHERE o.UserKey = @userKey AND NOT EXISTS(SELECT * FROM PackageRegistrationOwners other WHERE other.PackageRegistrationKey = r.[Key] AND other.UserKey != @userKey)",
                    new { userkey = user.Key });
            }

            return user;
        }

        public static string GenerateHash(byte[] input)
        {
            byte[] hashBytes;

            using (var hashAlgorithm = HashAlgorithm.Create("SHA512"))
            {
                hashBytes = hashAlgorithm.ComputeHash(input);
            }
            
            var hash = Convert.ToBase64String(hashBytes);
            return hash;
        }

        public static string GetDatabaseServerName(SqlConnectionStringBuilder connectionStringBuilder)
        {
            var dataSource = connectionStringBuilder.DataSource;
            if (dataSource.StartsWith("tcp:"))
                dataSource = dataSource.Substring(4);
            var indexOfFirstPeriod = dataSource.IndexOf(".", StringComparison.Ordinal);
            
            if (indexOfFirstPeriod > -1)
                return dataSource.Substring(0, indexOfFirstPeriod);

            return dataSource;
        }

        public static Database GetDatabase(
            SqlConnection db,
            string databaseName)
        {
            var dbs = db.Query<Database>(
                "SELECT name, state FROM sys.databases WHERE name = @databaseName",
                new { databaseName });

            return dbs.SingleOrDefault();
        }
    }
}
