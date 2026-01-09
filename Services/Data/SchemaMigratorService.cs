using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SLSKDONET.Data;
using SLSKDONET.Services.Models;

namespace SLSKDONET.Services;

public class SchemaMigratorService
{
    private readonly ILogger<SchemaMigratorService> _logger;

    public SchemaMigratorService(ILogger<SchemaMigratorService> logger)
    {
        _logger = logger;
    }

    public async Task InitializeDatabaseAsync()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _logger.LogInformation("[{Ms}ms] Database Init: Starting", sw.ElapsedMilliseconds);
        
        using var context = new AppDbContext();
        var db = context.Database;

        // Phase 12: Transition to EF Core Migrations
        // Detect legacy database (created by EnsureCreated) and bootstrap history if needed
        bool legacyDbExists = false;
        try 
        {
            var conn = db.GetDbConnection();
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name='Tracks';";
            var result = await cmd.ExecuteScalarAsync();
            legacyDbExists = (long)(result ?? 0) > 0;
            await conn.CloseAsync();
        } catch {}

        if (legacyDbExists)
        {
            bool historyExists = false;
            try 
            {
                var conn = db.GetDbConnection();
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name='__EFMigrationsHistory';";
                var result = await cmd.ExecuteScalarAsync();
                historyExists = (long)(result ?? 0) > 0;
                await conn.CloseAsync();
            } catch {}

            if (!historyExists)
            {
                _logger.LogWarning("Legacy manually-patched database detected. Bootstrapping EF migrations history.");
                
                await db.ExecuteSqlRawAsync(@"
                    CREATE TABLE IF NOT EXISTS ""LibraryFolders"" (
                        ""Id"" TEXT NOT NULL PRIMARY KEY,
                        ""FolderPath"" TEXT NOT NULL,
                        ""IsEnabled"" INTEGER NOT NULL DEFAULT 1,
                        ""AddedAt"" TEXT NOT NULL,
                        ""LastScannedAt"" TEXT NULL,
                        ""TracksFound"" INTEGER NOT NULL DEFAULT 0
                    );");

                await db.ExecuteSqlRawAsync(@"
                    CREATE TABLE IF NOT EXISTS ""__EFMigrationsHistory"" (
                        ""MigrationId"" TEXT NOT NULL PRIMARY KEY,
                        ""ProductVersion"" TEXT NOT NULL
                    );");

                await db.ExecuteSqlRawAsync(@"
                    INSERT OR IGNORE INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"")
                    VALUES ('20260107122524_InitialStructure', '9.0.0');");
            }
        }

        // Apply EF Migrations
        await db.MigrateAsync();
        _logger.LogInformation("[{Ms}ms] Database Init: Migrations applied", sw.ElapsedMilliseconds);

        // SQLite Optimizations (WAL mode etc)
        var connection = db.GetDbConnection();
        if (connection != null)
        {
            context.ConfigureSqliteOptimizations(connection);
            await ApplySchemaPatchesAsync(context, connection);
        }

        // Index Audit (DEBUG builds only)
#if DEBUG
        try
        {
            var auditReport = await AuditDatabaseIndexesAsync();
            if (auditReport.MissingIndexes.Any())
            {
                _logger.LogWarning("⚠️ Found {Count} missing indexes. Auto-applying...", 
                    auditReport.MissingIndexes.Count);
                await ApplyIndexRecommendationsAsync(auditReport);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Index audit failed (non-fatal)");
        }
#endif

        _logger.LogInformation("[{Ms}ms] Database initialization completed successfully", sw.ElapsedMilliseconds);
    }

    public async Task<IndexAuditReport> AuditDatabaseIndexesAsync()
    {
        var report = new IndexAuditReport
        {
            AuditDate = DateTime.Now,
            ExistingIndexes = new List<string>(),
            MissingIndexes = new List<IndexRecommendation>(),
            UnusedIndexes = new List<string>()
        };

        try
        {
            using var context = new AppDbContext();
            var connection = context.Database.GetDbConnection() as SqliteConnection;
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync();

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT name, tbl_name, sql 
                    FROM sqlite_master 
                    WHERE type='index' AND sql IS NOT NULL
                    ORDER BY tbl_name, name;";

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var indexName = reader.GetString(0);
                    var tableName = reader.GetString(1);
                    report.ExistingIndexes.Add($"{tableName}.{indexName}");
                }
            }

            var recommendations = GetDefaultIndexRecommendations();

            foreach (var rec in recommendations)
            {
                var indexKey = $"{rec.TableName}.{string.Join("_", rec.ColumnNames)}";
                var exists = report.ExistingIndexes.Any(idx => 
                    idx.Contains(rec.TableName, StringComparison.OrdinalIgnoreCase) && 
                    rec.ColumnNames.All(col => idx.Contains(col, StringComparison.OrdinalIgnoreCase)));

                if (!exists)
                {
                    report.MissingIndexes.Add(rec);
                }
            }

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Index audit failed");
            throw;
        }
    }

    private List<IndexRecommendation> GetDefaultIndexRecommendations()
    {
        return new List<IndexRecommendation>
        {
            new()
            {
                TableName = "PlaylistTracks",
                ColumnNames = new[] { "PlaylistId", "Status" },
                Reason = "Composite index for filtered playlist queries",
                EstimatedImpact = "High",
                CreateIndexSql = "CREATE INDEX IF NOT EXISTS IX_PlaylistTrack_PlaylistId_Status ON PlaylistTracks(PlaylistId, Status);"
            },
            new()
            {
                TableName = "LibraryEntries",
                ColumnNames = new[] { "UniqueHash" },
                Reason = "Global library lookups for cross-project deduplication",
                EstimatedImpact = "High",
                CreateIndexSql = "CREATE INDEX IF NOT EXISTS IX_LibraryEntry_UniqueHash ON LibraryEntries(UniqueHash);"
            },
            new()
            {
                TableName = "LibraryEntries",
                ColumnNames = new[] { "Artist", "Title" },
                Reason = "Search and filtering in All Tracks view",
                EstimatedImpact = "Medium",
                CreateIndexSql = "CREATE INDEX IF NOT EXISTS IX_LibraryEntry_Artist_Title ON LibraryEntries(Artist, Title);"
            },
            new()
            {
                TableName = "Projects",
                ColumnNames = new[] { "IsDeleted", "CreatedAt" },
                Reason = "Filtered project listing",
                EstimatedImpact = "Medium",
                CreateIndexSql = "CREATE INDEX IF NOT EXISTS IX_Project_IsDeleted_CreatedAt ON Projects(IsDeleted, CreatedAt);"
            },
        };
    }

    public async Task ApplyIndexRecommendationsAsync(IndexAuditReport report)
    {
        using var context = new AppDbContext();
        var connection = context.Database.GetDbConnection() as SqliteConnection;
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        foreach (var rec in report.MissingIndexes)
        {
            try
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = rec.CreateIndexSql;
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create index: {Sql}", rec.CreateIndexSql);
            }
        }
    }

    private async Task ApplySchemaPatchesAsync(AppDbContext context, System.Data.Common.DbConnection connection)
    {
        try
        {
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync();

            using var command = connection.CreateCommand();

            // Helper to check if column exists
            bool ColumnExists(string tableName, string columnName)
            {
                using var checkCmd = connection.CreateCommand();
                checkCmd.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{tableName}') WHERE name='{columnName}'";
                var result = checkCmd.ExecuteScalar();
                return Convert.ToInt32(result) > 0;
            }

            // Helper to check if table exists
            bool TableExists(string tableName)
            {
                using var checkCmd = connection.CreateCommand();
                checkCmd.CommandText = $"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='{tableName}'";
                var result = checkCmd.ExecuteScalar();
                return Convert.ToInt32(result) > 0;
            }

            // 1. TechnicalDetails Table
            if (!TableExists("TechnicalDetails"))
            {
                _logger.LogInformation("Patching Schema: Creating TechnicalDetails table...");
                command.CommandText = @"
                    CREATE TABLE ""TechnicalDetails"" (
                        ""Id"" TEXT NOT NULL CONSTRAINT ""PK_TechnicalDetails"" PRIMARY KEY,
                        ""PlaylistTrackId"" TEXT NOT NULL,
                        ""WaveformData"" BLOB NULL,
                        ""RmsData"" BLOB NULL,
                        ""LowData"" BLOB NULL,
                        ""MidData"" BLOB NULL,
                        ""HighData"" BLOB NULL,
                        ""AiEmbeddingJson"" TEXT NULL,
                        ""CuePointsJson"" TEXT NULL,
                        ""AudioFingerprint"" TEXT NULL,
                        ""SpectralHash"" TEXT NULL,
                        ""LastUpdated"" TEXT NOT NULL,
                        ""IsPrepared"" INTEGER NOT NULL DEFAULT 0,
                        ""PrimaryGenre"" TEXT NULL,
                        CONSTRAINT ""FK_TechnicalDetails_PlaylistTracks_PlaylistTrackId"" FOREIGN KEY (""PlaylistTrackId"") REFERENCES ""PlaylistTracks"" (""Id"") ON DELETE CASCADE
                    );
                    CREATE UNIQUE INDEX ""IX_TechnicalDetails_PlaylistTrackId"" ON ""TechnicalDetails"" (""PlaylistTrackId"");
                ";
                await command.ExecuteNonQueryAsync();
            }

            // 2. PlaylistTracks Columns
            if (!ColumnExists("PlaylistTracks", "PrimaryGenre"))
            {
                _logger.LogInformation("Patching Schema: Adding PrimaryGenre to PlaylistTracks...");
                command.CommandText = @"ALTER TABLE ""PlaylistTracks"" ADD COLUMN ""PrimaryGenre"" TEXT NULL;";
                await command.ExecuteNonQueryAsync();
            }
            if (!ColumnExists("PlaylistTracks", "IsPrepared"))
            {
                _logger.LogInformation("Patching Schema: Adding IsPrepared to PlaylistTracks...");
                command.CommandText = @"ALTER TABLE ""PlaylistTracks"" ADD COLUMN ""IsPrepared"" INTEGER NOT NULL DEFAULT 0;";
                await command.ExecuteNonQueryAsync();
            }

            // 3. LibraryEntries Columns
            if (!ColumnExists("LibraryEntries", "PrimaryGenre"))
            {
                _logger.LogInformation("Patching Schema: Adding PrimaryGenre to LibraryEntries...");
                command.CommandText = @"ALTER TABLE ""LibraryEntries"" ADD COLUMN ""PrimaryGenre"" TEXT NULL;";
                await command.ExecuteNonQueryAsync();
            }
            if (!ColumnExists("LibraryEntries", "IsPrepared"))
            {
                _logger.LogInformation("Patching Schema: Adding IsPrepared to LibraryEntries...");
                command.CommandText = @"ALTER TABLE ""LibraryEntries"" ADD COLUMN ""IsPrepared"" INTEGER NOT NULL DEFAULT 0;";
                await command.ExecuteNonQueryAsync();
            }
            if (!ColumnExists("LibraryEntries", "CuePointsJson"))
            {
                _logger.LogInformation("Patching Schema: Adding CuePointsJson to LibraryEntries...");
                command.CommandText = @"ALTER TABLE ""LibraryEntries"" ADD COLUMN ""CuePointsJson"" TEXT NULL;";
                await command.ExecuteNonQueryAsync();
            }
            
            // 4. TechnicalDetails Table Columns (for existing tables)
            if (TableExists("TechnicalDetails"))
            {
                if (!ColumnExists("TechnicalDetails", "IsPrepared"))
                {
                    _logger.LogInformation("Patching Schema: Adding IsPrepared to TechnicalDetails...");
                    command.CommandText = @"ALTER TABLE ""TechnicalDetails"" ADD COLUMN ""IsPrepared"" INTEGER NOT NULL DEFAULT 0;";
                    await command.ExecuteNonQueryAsync();
                }
                if (!ColumnExists("TechnicalDetails", "CurationConfidence"))
                {
                    _logger.LogInformation("Patching Schema: Adding CurationConfidence to TechnicalDetails...");
                    command.CommandText = @"ALTER TABLE ""TechnicalDetails"" ADD COLUMN ""CurationConfidence"" INTEGER NOT NULL DEFAULT 0;";
                    await command.ExecuteNonQueryAsync();
                }
                if (!ColumnExists("TechnicalDetails", "ProvenanceJson"))
                {
                    _logger.LogInformation("Patching Schema: Adding ProvenanceJson to TechnicalDetails...");
                    command.CommandText = @"ALTER TABLE ""TechnicalDetails"" ADD COLUMN ""ProvenanceJson"" TEXT NULL;";
                    await command.ExecuteNonQueryAsync();
                }
                if (!ColumnExists("TechnicalDetails", "IsReviewNeeded"))
                {
                    _logger.LogInformation("Patching Schema: Adding IsReviewNeeded to TechnicalDetails...");
                    command.CommandText = @"ALTER TABLE ""TechnicalDetails"" ADD COLUMN ""IsReviewNeeded"" INTEGER NOT NULL DEFAULT 0;";
                    await command.ExecuteNonQueryAsync();
                }
                if (!ColumnExists("TechnicalDetails", "PrimaryGenre"))
                {
                    _logger.LogInformation("Patching Schema: Adding PrimaryGenre to TechnicalDetails...");
                    command.CommandText = @"ALTER TABLE ""TechnicalDetails"" ADD COLUMN ""PrimaryGenre"" TEXT NULL;";
                    await command.ExecuteNonQueryAsync();
                }
            }
            
            // 5. AudioFeatures Table Columns - Force attempt (table may not exist yet during cold start)
            try
            {
                _logger.LogInformation("Attempting to add AiEmbeddingJson column to AudioFeatures...");
                command.CommandText = @"ALTER TABLE ""AudioFeatures"" ADD COLUMN ""AiEmbeddingJson"" TEXT NULL;";
                await command.ExecuteNonQueryAsync();
                _logger.LogInformation("✅ AiEmbeddingJson column added successfully");
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.Message.Contains("duplicate column"))
            {
                _logger.LogInformation("AiEmbeddingJson column already exists, skipping");
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.Message.Contains("no such table"))
            {
                _logger.LogInformation("AudioFeatures table doesn't exist yet, skipping (will be created with column)");
            }

            // 6. Phase 17: EDM Specialist Columns for AudioFeatures
            // Using force-add pattern - try to add and catch duplicate errors
            _logger.LogInformation("Phase 17: Checking EDM specialist columns for AudioFeatures...");
            
            var edmColumns = new[]
            {
                ("Arousal", "REAL NOT NULL DEFAULT 5"),
                ("Valence", "REAL NOT NULL DEFAULT 5"),
                ("ElectronicSubgenre", "TEXT NULL DEFAULT ''"),
                ("ElectronicSubgenreConfidence", "REAL NOT NULL DEFAULT 0"),
                ("IsDjTool", "INTEGER NOT NULL DEFAULT 0"),
                ("TonalProbability", "REAL NOT NULL DEFAULT 0.5"),
                ("Intensity", "REAL NOT NULL DEFAULT 0")
            };

            foreach (var (columnName, columnDef) in edmColumns)
            {
                try
                {
                    command.CommandText = $@"ALTER TABLE ""AudioFeatures"" ADD COLUMN ""{columnName}"" {columnDef};";
                    await command.ExecuteNonQueryAsync();
                    _logger.LogInformation("✅ Added column {Column} to AudioFeatures", columnName);
                }
                catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.Message.Contains("duplicate column"))
                {
                    // Column already exists, skip silently
                }
                catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.Message.Contains("no such table"))
                {
                    _logger.LogWarning("AudioFeatures table doesn't exist yet, will be created by EF Core");
                    break; // No point continuing if table doesn't exist
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to add column {Column} to AudioFeatures", columnName);
                }
            }

            // 7. Phase 17: TrackPhrases Table
            if (!TableExists("TrackPhrases"))
            {
                _logger.LogInformation("Patching Schema: Creating TrackPhrases table...");
                command.CommandText = @"
                    CREATE TABLE ""TrackPhrases"" (
                        ""Id"" INTEGER PRIMARY KEY AUTOINCREMENT,
                        ""TrackUniqueHash"" TEXT NOT NULL,
                        ""Type"" INTEGER NOT NULL,
                        ""StartTimeSeconds"" REAL NOT NULL,
                        ""EndTimeSeconds"" REAL NOT NULL,
                        ""EnergyLevel"" REAL NOT NULL DEFAULT 0,
                        ""Confidence"" REAL NOT NULL DEFAULT 0,
                        ""OrderIndex"" INTEGER NOT NULL DEFAULT 0,
                        ""Label"" TEXT NULL
                    );
                    CREATE INDEX ""IX_TrackPhrases_TrackUniqueHash"" ON ""TrackPhrases"" (""TrackUniqueHash"");
                ";
                await command.ExecuteNonQueryAsync();
            }

            // 8. Phase 17: GenreCueTemplates Table
            if (!TableExists("GenreCueTemplates"))
            {
                _logger.LogInformation("Patching Schema: Creating GenreCueTemplates table...");
                command.CommandText = @"
                    CREATE TABLE ""GenreCueTemplates"" (
                        ""Id"" INTEGER PRIMARY KEY AUTOINCREMENT,
                        ""GenreName"" TEXT NOT NULL,
                        ""DisplayName"" TEXT NULL,
                        ""IsBuiltIn"" INTEGER NOT NULL DEFAULT 0,
                        ""Cue1Target"" INTEGER NOT NULL DEFAULT 0,
                        ""Cue1OffsetBars"" INTEGER NOT NULL DEFAULT 0,
                        ""Cue1Color"" TEXT NOT NULL DEFAULT '#FF0000',
                        ""Cue1Label"" TEXT NULL,
                        ""Cue2Target"" INTEGER NOT NULL DEFAULT 0,
                        ""Cue2OffsetBars"" INTEGER NOT NULL DEFAULT 0,
                        ""Cue2Color"" TEXT NOT NULL DEFAULT '#00FF00',
                        ""Cue2Label"" TEXT NULL,
                        ""Cue3Target"" INTEGER NOT NULL DEFAULT 0,
                        ""Cue3OffsetBars"" INTEGER NOT NULL DEFAULT 0,
                        ""Cue3Color"" TEXT NOT NULL DEFAULT '#0000FF',
                        ""Cue3Label"" TEXT NULL,
                        ""Cue4Target"" INTEGER NOT NULL DEFAULT 0,
                        ""Cue4OffsetBars"" INTEGER NOT NULL DEFAULT 0,
                        ""Cue4Color"" TEXT NOT NULL DEFAULT '#FFFF00',
                        ""Cue4Label"" TEXT NULL,
                        ""Cue5Target"" INTEGER NULL,
                        ""Cue5OffsetBars"" INTEGER NULL,
                        ""Cue5Color"" TEXT NULL,
                        ""Cue5Label"" TEXT NULL,
                        ""Cue6Target"" INTEGER NULL,
                        ""Cue6OffsetBars"" INTEGER NULL,
                        ""Cue6Color"" TEXT NULL,
                        ""Cue6Label"" TEXT NULL,
                        ""Cue7Target"" INTEGER NULL,
                        ""Cue7OffsetBars"" INTEGER NULL,
                        ""Cue7Color"" TEXT NULL,
                        ""Cue7Label"" TEXT NULL,
                        ""Cue8Target"" INTEGER NULL,
                        ""Cue8OffsetBars"" INTEGER NULL,
                        ""Cue8Color"" TEXT NULL,
                        ""Cue8Label"" TEXT NULL
                    );
                    CREATE INDEX ""IX_GenreCueTemplates_GenreName"" ON ""GenreCueTemplates"" (""GenreName"");
                ";
                await command.ExecuteNonQueryAsync();
            }

            _logger.LogInformation("Schema patching completed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply schema patches. Application may be unstable.");
        }
    }
}
