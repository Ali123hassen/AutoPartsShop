using AutoPartsShop.Application.Interfaces;
using AutoPartsShop.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;

namespace AutoPartsShop.Infrastructure.Services;

/// <summary>
/// Executes SQL Server BACKUP DATABASE and RESTORE DATABASE commands.
/// Compatible with SQL Server Express, SQL Server Standard/Enterprise, and LocalDB.
/// </summary>
public class SqlServerBackupExecutor : IDatabaseBackupExecutor
{
    private readonly IServiceProvider _serviceProvider;

    public SqlServerBackupExecutor(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task ExecuteBackupAsync(string backupFilePath)
    {
        string connectionString;
        string databaseName;

        using (var scope = _serviceProvider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            connectionString = dbContext.Database.GetConnectionString()
                ?? throw new InvalidOperationException("Cannot determine database connection string.");
            databaseName = dbContext.Database.GetDbConnection().Database;
        }

        // Normalize the user's target path (remove any .\ or double-backslash issues)
        backupFilePath = Path.GetFullPath(backupFilePath);

        // Ensure the target backup directory exists
        var targetDirectory = Path.GetDirectoryName(backupFilePath);
        if (!string.IsNullOrEmpty(targetDirectory) && !Directory.Exists(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        // Build connection string to master database for executing BACKUP
        var masterBuilder = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = "master"
        };

        await using var connection = new SqlConnection(masterBuilder.ConnectionString);
        await connection.OpenAsync();

        // Get the SQL Server default backup directory (SQL Server service has write access there)
        // Always use a clean, English-only path for the actual BACKUP command
        var sqlBackupDir = await GetSqlBackupDirectoryAsync(connection);

        // Build the actual backup file path in SQL Server's accessible directory
        var fileName = Path.GetFileName(backupFilePath);
        var sqlBackupFilePath = Path.Combine(sqlBackupDir, fileName);

        // Ensure the SQL Server backup directory exists by having SQL Server create it via sp_configure
        // and also try from the application side
        if (!Directory.Exists(sqlBackupDir))
        {
            try
            {
                Directory.CreateDirectory(sqlBackupDir);
            }
            catch
            {
                // If we can't create it from the application, try using a simpler path
                // Fall back to just the SQL Server backup root directory
                var sqlBackupRoot = await GetSqlBackupRootDirectoryAsync(connection);
                if (!string.IsNullOrEmpty(sqlBackupRoot) && Directory.Exists(sqlBackupRoot))
                {
                    sqlBackupDir = sqlBackupRoot;
                    sqlBackupFilePath = Path.Combine(sqlBackupDir, fileName);
                }
            }
        }

        // Double-check the SQL Server backup path doesn't contain problematic characters
        // If it does, fall back to the SQL Server backup root
        if (sqlBackupFilePath.Any(c => c > 127))
        {
            var sqlBackupRoot = await GetSqlBackupRootDirectoryAsync(connection);
            if (!string.IsNullOrEmpty(sqlBackupRoot))
            {
                sqlBackupDir = sqlBackupRoot;
                sqlBackupFilePath = Path.Combine(sqlBackupDir, fileName);
            }
        }

        var backupSql = $"""
            BACKUP DATABASE [{databaseName}]
            TO DISK = @FilePath
            WITH FORMAT, MEDIANAME = 'AutoPartsShopBackup',
            NAME = 'Full Backup of {databaseName}'
            """;

        await using var command = new SqlCommand(backupSql, connection);
        command.Parameters.AddWithValue("@FilePath", sqlBackupFilePath);
        command.CommandTimeout = 300; // 5 minutes timeout for large databases

        await command.ExecuteNonQueryAsync();

        // Copy the backup file from SQL Server directory to the user's requested directory
        if (!string.Equals(sqlBackupFilePath, backupFilePath, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                File.Copy(sqlBackupFilePath, backupFilePath, overwrite: true);
            }
            catch (Exception ex)
            {
                // If copy fails, the backup still exists in SQL Server directory
                // Return the SQL Server path so the user knows where it is
                throw new InvalidOperationException(
                    $"تم إنشاء النسخة الاحتياطية في: {sqlBackupFilePath}\n" +
                    $"لكن فشل نسخها إلى: {backupFilePath}\n" +
                    $"السبب: {ex.Message}\n\n" +
                    $"يمكنك نسخ الملف يدوياً من المسار أعلاه.", ex);
            }

            // Clean up the original file from SQL Server directory after successful copy
            try
            {
                File.Delete(sqlBackupFilePath);
            }
            catch
            {
                // Ignore cleanup failure - the backup is safely in the user's directory
            }
        }
    }

    public async Task ExecuteRestoreAsync(string backupFilePath)
    {
        string connectionString;
        string databaseName;

        using (var scope = _serviceProvider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            connectionString = dbContext.Database.GetConnectionString()
                ?? throw new InvalidOperationException("Cannot determine database connection string.");
            databaseName = dbContext.Database.GetDbConnection().Database;
        }

        // Build connection string to master database
        var masterBuilder = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = "master"
        };

        await using var connection = new SqlConnection(masterBuilder.ConnectionString);
        await connection.OpenAsync();

        // Copy the backup file to SQL Server's accessible directory if it can't read from the user's path
        var sqlRestoreFilePath = await CopyToSqlAccessiblePathAsync(connection, backupFilePath);

        // Get the logical file names from the backup to handle MOVE operations
        // This is important for SQL Express where file paths may differ from the original backup
        var logicalFiles = await GetLogicalFileNamesAsync(connection, sqlRestoreFilePath);

        // Get the default data and log directories for the SQL Express instance
        var (defaultDataDir, defaultLogDir) = await GetDefaultDirectoriesAsync(connection);

        // Set the database to SINGLE_USER to close all active connections
        var setSingleUserSql = $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE";
        await using (var singleUserCmd = new SqlCommand(setSingleUserSql, connection))
        {
            singleUserCmd.CommandTimeout = 60;
            await singleUserCmd.ExecuteNonQueryAsync();
        }

        try
        {
            // Build the RESTORE command with MOVE options to ensure files go to the correct location
            var restoreSql = new System.Text.StringBuilder();
            restoreSql.Append($"RESTORE DATABASE [{databaseName}] FROM DISK = @FilePath WITH REPLACE");

            // Add MOVE clauses for each logical file to the default SQL Server directories
            if (logicalFiles.Count > 0 && !string.IsNullOrEmpty(defaultDataDir))
            {
                foreach (var logicalFile in logicalFiles)
                {
                    var targetDir = logicalFile.Type == "D" ? defaultDataDir : defaultLogDir;
                    var extension = logicalFile.Type == "D" ? ".mdf" : ".ldf";
                    var targetPath = Path.Combine(targetDir, $"{databaseName}{extension}");
                    restoreSql.Append($", MOVE '{logicalFile.Name}' TO '{targetPath}'");
                }
            }

            await using var command = new SqlCommand(restoreSql.ToString(), connection);
            command.Parameters.AddWithValue("@FilePath", sqlRestoreFilePath);
            command.CommandTimeout = 300;

            await command.ExecuteNonQueryAsync();

            // Set back to MULTI_USER
            var setMultiUserSql = $"ALTER DATABASE [{databaseName}] SET MULTI_USER";
            await using var multiUserCmd = new SqlCommand(setMultiUserSql, connection);
            multiUserCmd.CommandTimeout = 60;
            await multiUserCmd.ExecuteNonQueryAsync();

            // Clean up the copied file from SQL Server directory (if we copied it)
            if (!string.Equals(sqlRestoreFilePath, backupFilePath, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    File.Delete(sqlRestoreFilePath);
                }
                catch
                {
                    // Ignore cleanup failure
                }
            }
        }
        catch
        {
            // Try to set back to MULTI_USER even if restore fails
            try
            {
                var setMultiUserSql = $"ALTER DATABASE [{databaseName}] SET MULTI_USER";
                await using var multiUserCmd = new SqlCommand(setMultiUserSql, connection);
                multiUserCmd.CommandTimeout = 60;
                await multiUserCmd.ExecuteNonQueryAsync();
            }
            catch { /* ignore */ }

            throw;
        }
    }

    /// <summary>
    /// Gets the logical file names from a backup file using FILELISTONLY.
    /// </summary>
    private async Task<List<LogicalFileInfo>> GetLogicalFileNamesAsync(SqlConnection connection, string backupFilePath)
    {
        var result = new List<LogicalFileInfo>();

        try
        {
            await using var command = new SqlCommand("RESTORE FILELISTONLY FROM DISK = @FilePath", connection);
            command.Parameters.AddWithValue("@FilePath", backupFilePath);
            command.CommandTimeout = 60;

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new LogicalFileInfo
                {
                    Name = reader["LogicalName"].ToString() ?? "",
                    Type = reader["Type"].ToString() ?? "D"
                });
            }
        }
        catch
        {
            // If FILELISTONLY fails, we'll proceed without MOVE clauses
            // The RESTORE will still work if the original file paths are valid
        }

        return result;
    }

    /// <summary>
    /// Gets the default data and log directories for the SQL Server instance.
    /// </summary>
    private async Task<(string dataDir, string logDir)> GetDefaultDirectoriesAsync(SqlConnection connection)
    {
        string? dataDir = null;
        string? logDir = null;

        try
        {
            await using var command = new SqlCommand(
                "SELECT SERVERPROPERTY('InstanceDefaultDataPath') AS DataDir, SERVERPROPERTY('InstanceDefaultLogPath') AS LogDir",
                connection);
            await using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                dataDir = reader["DataDir"]?.ToString();
                logDir = reader["LogDir"]?.ToString();
            }
        }
        catch
        {
            // Fallback: try getting from registry or use default SQL Express paths
        }

        // Fallback: try to detect the actual SQL Server instance from the connection
        if (string.IsNullOrEmpty(dataDir))
        {
            try
            {
                await using var cmd = new SqlCommand(
                    "SELECT SUBSTRING(physical_name, 1, LEN(physical_name) - CHARINDEX('\\', REVERSE(physical_name))) AS DataDir FROM sys.master_files WHERE database_id = 1 AND type = 0",
                    connection);
                var result = await cmd.ExecuteScalarAsync();
                if (result != null)
                {
                    dataDir = result.ToString();
                }
            }
            catch { }
        }

        // Fallback paths for SQL Server Express (try MSSQL17 first, then MSSQL16)
        if (string.IsNullOrEmpty(dataDir))
        {
            var sqlInstanceDirs = new[]
            {
                @"C:\Program Files\Microsoft SQL Server\MSSQL17.SQLEXPRESS\MSSQL\DATA",
                @"C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\DATA",
                @"C:\Program Files\Microsoft SQL Server\MSSQL15.SQLEXPRESS\MSSQL\DATA",
                @"C:\Program Files\Microsoft SQL Server\MSSQL14.SQLEXPRESS\MSSQL\DATA"
            };

            foreach (var dir in sqlInstanceDirs)
            {
                if (Directory.Exists(dir))
                {
                    dataDir = dir;
                    break;
                }
            }

            dataDir ??= @"C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\DATA";
        }

        logDir ??= dataDir;

        return (NormalizePath(dataDir), NormalizePath(logDir ?? dataDir));
    }

    /// <summary>
    /// Gets a backup directory that SQL Server service has write access to.
    /// Uses the SQL Server instance's default backup directory if available,
    /// otherwise falls back to the data directory with a Backup subfolder.
    /// Always returns a clean, English-only path without Arabic or special characters.
    /// </summary>
    private async Task<string> GetSqlBackupDirectoryAsync(SqlConnection connection)
    {
        // Try to get the SQL Server instance default backup directory
        try
        {
            await using var command = new SqlCommand(
                "SELECT SERVERPROPERTY('InstanceDefaultBackupPath') AS BackupDir",
                connection);
            await using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                var backupDir = reader["BackupDir"]?.ToString();
                if (!string.IsNullOrEmpty(backupDir))
                {
                    // Normalize the path and add our app subfolder
                    var normalizedDir = NormalizePath(backupDir);
                    var appBackupDir = Path.Combine(normalizedDir, "AutoPartsShop_Backups");
                    return appBackupDir;
                }
            }
        }
        catch
        {
            // InstanceDefaultBackupPath not available in all SQL Server versions/editions
        }

        // Fallback: use the default data directory with a Backup subfolder
        try
        {
            var (dataDir, _) = await GetDefaultDirectoriesAsync(connection);
            if (!string.IsNullOrEmpty(dataDir))
            {
                // Go up one level from DATA and create a Backup folder
                var parentDir = Path.GetDirectoryName(dataDir.TrimEnd(Path.DirectorySeparatorChar));
                if (!string.IsNullOrEmpty(parentDir))
                {
                    return Path.Combine(NormalizePath(parentDir), "Backup", "AutoPartsShop_Backups");
                }
            }
        }
        catch
        {
            // Ignore
        }

        // Final fallback: try to detect the actual SQL Server instance name from connection
        try
        {
            var instanceDir = await DetectSqlInstanceDirectoryAsync(connection);
            if (!string.IsNullOrEmpty(instanceDir))
            {
                return Path.Combine(instanceDir, "Backup", "AutoPartsShop_Backups");
            }
        }
        catch
        {
            // Ignore
        }

        // Last resort fallback
        return @"C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\Backup\AutoPartsShop_Backups";
    }

    /// <summary>
    /// Gets just the SQL Server backup root directory (without our app subfolder).
    /// Used as a fallback when the app subfolder path has issues.
    /// </summary>
    private async Task<string?> GetSqlBackupRootDirectoryAsync(SqlConnection connection)
    {
        try
        {
            await using var command = new SqlCommand(
                "SELECT SERVERPROPERTY('InstanceDefaultBackupPath') AS BackupDir",
                connection);
            await using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                var backupDir = reader["BackupDir"]?.ToString();
                if (!string.IsNullOrEmpty(backupDir))
                {
                    return NormalizePath(backupDir);
                }
            }
        }
        catch { }

        return null;
    }

    /// <summary>
    /// Detects the actual SQL Server instance data directory from the server's properties.
    /// This works across different SQL Server versions (MSSQL16, MSSQL17, etc.).
    /// </summary>
    private async Task<string?> DetectSqlInstanceDirectoryAsync(SqlConnection connection)
    {
        try
        {
            await using var command = new SqlCommand(
                "SELECT SERVERPROPERTY('InstanceDefaultDataPath') AS DataDir",
                connection);
            await using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                var dataDir = reader["DataDir"]?.ToString();
                if (!string.IsNullOrEmpty(dataDir))
                {
                    // Data path is like: C:\...\MSSQL17.SQLEXPRESS\MSSQL\DATA
                    // We need: C:\...\MSSQL17.SQLEXPRESS\MSSQL
                    var normalizedDataDir = NormalizePath(dataDir.TrimEnd(Path.DirectorySeparatorChar));
                    var parentDir = Path.GetDirectoryName(normalizedDataDir);
                    return parentDir; // Returns the MSSQL directory (parent of DATA)
                }
            }
        }
        catch { }

        return null;
    }

    /// <summary>
    /// Normalizes a file system path by removing invalid segments like ".", "..",
    /// double backslashes, and trailing separators. Also resolves relative paths.
    /// </summary>
    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;

        try
        {
            // GetFullPath resolves .\, ..\, and double backslashes
            var normalized = Path.GetFullPath(path);

            // Remove any trailing directory separator for consistency
            normalized = normalized.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            return normalized;
        }
        catch
        {
            // If GetFullPath fails, do manual cleanup
            var result = path;

            // Remove .\ segments
            var dotBackslash = "." + Path.DirectorySeparatorChar;
            while (result.Contains(dotBackslash) || result.Contains("/./"))
            {
                result = result.Replace(dotBackslash, Path.DirectorySeparatorChar.ToString()).Replace("/./", "/");
            }

            // Remove double backslashes (but not the UNC \\ prefix)
            if (result.StartsWith("\\\\"))
            {
                result = "\\\\" + result[2..].Replace("\\\\", "\\");
            }
            else
            {
                while (result.Contains("\\\\"))
                {
                    result = result.Replace("\\\\", "\\");
                }
            }

            return result.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }

    /// <summary>
    /// Copies the backup file to SQL Server's accessible directory for restore.
    /// SQL Server service may not have read access to user directories (e.g., Documents).
    /// This method copies the file to a directory SQL Server can access.
    /// </summary>
    private async Task<string> CopyToSqlAccessiblePathAsync(SqlConnection connection, string backupFilePath)
    {
        // Normalize the path first
        backupFilePath = NormalizePath(backupFilePath);

        // First, check if SQL Server can directly access the file by trying FILELISTONLY
        try
        {
            await using var testCmd = new SqlCommand("RESTORE FILELISTONLY FROM DISK = @FilePath", connection);
            testCmd.Parameters.AddWithValue("@FilePath", backupFilePath);
            testCmd.CommandTimeout = 30;

            await using var testReader = await testCmd.ExecuteReaderAsync();
            testReader.Close();

            // SQL Server can read the file directly — no copy needed
            return backupFilePath;
        }
        catch
        {
            // SQL Server can't access the file — need to copy it
        }

        // Copy the file to SQL Server's accessible directory
        var sqlBackupDir = await GetSqlBackupDirectoryAsync(connection);
        var fileName = Path.GetFileName(backupFilePath);
        var sqlFilePath = Path.Combine(sqlBackupDir, fileName);

        // Ensure the directory exists
        if (!Directory.Exists(sqlBackupDir))
        {
            try
            {
                Directory.CreateDirectory(sqlBackupDir);
            }
            catch
            {
                // If we can't create the app subfolder, try the SQL root backup directory
                var sqlBackupRoot = await GetSqlBackupRootDirectoryAsync(connection);
                if (!string.IsNullOrEmpty(sqlBackupRoot))
                {
                    sqlBackupDir = sqlBackupRoot;
                    sqlFilePath = Path.Combine(sqlBackupDir, fileName);
                    if (!Directory.Exists(sqlBackupDir))
                    {
                        Directory.CreateDirectory(sqlBackupDir);
                    }
                }
            }
        }

        // Check for non-ASCII characters in the SQL path (which SQL Server can't handle)
        if (sqlFilePath.Any(c => c > 127))
        {
            var sqlBackupRoot = await GetSqlBackupRootDirectoryAsync(connection);
            if (!string.IsNullOrEmpty(sqlBackupRoot))
            {
                sqlBackupDir = sqlBackupRoot;
                sqlFilePath = Path.Combine(sqlBackupDir, fileName);
            }
        }

        // Copy the file
        File.Copy(backupFilePath, sqlFilePath, overwrite: true);

        return sqlFilePath;
    }

    private class LogicalFileInfo
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "D"; // D = Data, L = Log
    }
}
