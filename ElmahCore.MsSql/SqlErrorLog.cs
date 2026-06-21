using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace ElmahCore.Sql;

/// <summary>
///     An <see cref="ErrorLog" /> implementation that uses MSSQL
///     as its backing store.
/// </summary>
// ReSharper disable once UnusedType.Global
public class SqlErrorLog : ErrorLog
{
    private const int MaxAppNameLength = 60;
    private readonly bool _logAllXml;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SqlErrorLog" /> class
    ///     using a dictionary of configured settings.
    /// </summary>
    public SqlErrorLog(IOptions<ElmahOptions> option)
        : this(option.Value.ConnectionString, option.Value.SqlServerDatabaseSchemaName,
            option.Value.SqlServerDatabaseTableName, option.Value.CreateTablesIfNotExist,
            option.Value.LogAllXml)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="SqlErrorLog" /> class
    ///     to use a specific connection string for connecting to the database and a specific schema and table name.
    /// </summary>
    public SqlErrorLog(string connectionString, string schemaName = null, string tableName = null,
        bool createTablesIfNotExist = true, bool logAllXml = true)
    {
        if (string.IsNullOrEmpty(connectionString))
            throw new ArgumentNullException(nameof(connectionString));

        ConnectionString = connectionString;
        DatabaseSchemaName = !string.IsNullOrWhiteSpace(schemaName) ? schemaName : "dbo";
        DatabaseTableName = !string.IsNullOrWhiteSpace(tableName) ? tableName : "ELMAH_Error";
        _logAllXml = logAllXml;

        if (createTablesIfNotExist)
            CreateTableIfNotExists();
    }

    /// <summary>
    ///     Gets the name of this error log implementation.
    /// </summary>
    public override string Name => "MSSQL Error Log";

    /// <summary>
    ///     Gets the connection string used by the log to connect to the database.
    /// </summary>
    // ReSharper disable once MemberCanBeProtected.Global
    public virtual string ConnectionString { get; }

    /// <summary>
    /// Gets the Schema name to be used for the error table
    /// </summary>
    protected virtual string DatabaseSchemaName { get; }

    /// <summary>
    /// Gets the Table name to be used for the error table
    /// </summary>
    protected virtual string DatabaseTableName { get; }

    public override string Log(Error error)
    {
        var id = Guid.NewGuid();
        Log(id, error);
        return id.ToString();
    }

    public override void Log(Guid id, Error error)
    {
        try
        {
            var errorXml = _logAllXml
                ? ErrorXml.EncodeString(error)
                : "<error message=\"AllXml logging disabled\" />";

            using var connection = new SqlConnection(ConnectionString);
            using var command = Commands.LogError(id, ApplicationName, error.HostName, error.Type, error.Source,
                error.Message, error.User, error.StatusCode, error.Time, errorXml,
                DatabaseSchemaName, DatabaseTableName, error.IsReviewed);
            command.Connection = connection;
            connection.Open();
            command.ExecuteNonQuery();
        }
        catch
        {
            //guard: silently fail, this can't bubble up or it will create a stack overflow from errors attempting to log errors....
        }
    }

    public override ErrorLogEntry GetError(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        if (id.Length == 0) throw new ArgumentException(null, nameof(id));

        Guid errorGuid;

        try
        {
            errorGuid = new Guid(id);
        }
        catch (FormatException e)
        {
            throw new ArgumentException(e.Message, nameof(id), e);
        }

        string errorXml;

        using (var connection = new SqlConnection(ConnectionString))
        using (var command = Commands.GetErrorXml(ApplicationName, errorGuid,
                   DatabaseSchemaName, DatabaseTableName))
        {
            command.Connection = connection;
            connection.Open();
            errorXml = (string)command.ExecuteScalar();
        }

        if (errorXml == null)
            return null;

        var error = ErrorXml.DecodeString(errorXml);
        return new ErrorLogEntry(this, id, error);
    }

    public override int GetErrors(int errorIndex, int pageSize, ICollection<ErrorLogEntry> errorEntryList)
    {
        if (errorIndex < 0) throw new ArgumentOutOfRangeException(nameof(errorIndex), errorIndex, null);
        if (pageSize < 0) throw new ArgumentOutOfRangeException(nameof(pageSize), pageSize, null);

        using var connection = new SqlConnection(ConnectionString);
        connection.Open();

        using (var command = Commands.GetErrorsXml(ApplicationName, errorIndex, pageSize,
                   DatabaseSchemaName, DatabaseTableName))
        {
            command.Connection = connection;

            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    var id = reader.GetGuid(0);
                    var xml = reader.GetString(1);
                    var isReviewed = reader.GetBoolean(2);
                    var error = ErrorXml.DecodeString(xml);
                    error.IsReviewed = isReviewed;
                    errorEntryList.Add(new ErrorLogEntry(this, id.ToString(), error));
                }
            }
        }

        using (var command = Commands.GetErrorsXmlTotal(ApplicationName,
                   DatabaseSchemaName, DatabaseTableName))
        {
            command.Connection = connection;
            return int.Parse(command.ExecuteScalar().ToString());
        }
    }

    /// <summary>
    ///  Creates the necessary tables and sequences used by this implementation
    /// </summary>
    private void CreateTableIfNotExists()
    {
        using var connection = new SqlConnection(ConnectionString);
        connection.Open();
        using var cmdCheck = Commands.CheckTable(DatabaseSchemaName, DatabaseTableName);
        cmdCheck.Connection = connection;
        // ReSharper disable once PossibleNullReferenceException
        var exists = (int?)cmdCheck.ExecuteScalar();

        if (!exists.HasValue)
        {
            ExecuteBatchNonQuery(Commands.CreateTableSql(DatabaseSchemaName, DatabaseTableName),
                connection);
        }

        // Run migrations (columns, indexes, stored procedures)
        ExecuteBatchNonQuery(Commands.CreateMigrationsSql(DatabaseSchemaName, DatabaseTableName),
            connection);
    }

    private static void ExecuteBatchNonQuery(string sql, SqlConnection conn)
    {
        var sqlBatch = string.Empty;
        using var cmd = new SqlCommand(string.Empty, conn);
        sql += "\nGO"; // make sure the last batch is executed.
        foreach (var line in sql.Split(["\n", "\r"],
                     StringSplitOptions.RemoveEmptyEntries))
            if (line.ToUpperInvariant().Trim() == "GO")
            {
                cmd.CommandText = sqlBatch;
                cmd.ExecuteNonQuery();
                sqlBatch = string.Empty;
            }
            else
            {
                sqlBatch += line + "\n";
            }
    }

    private static class Commands
    {
        public static string CreateTableSql(string schemaName, string tableName)
        {
            return
                $@"
CREATE TABLE [{schemaName}].[{tableName}]
(
    [ErrorId]     UNIQUEIDENTIFIER NOT NULL,
    [Application] NVARCHAR(60)  NOT NULL,
    [Host]        NVARCHAR(50)  NOT NULL,
    [Type]        NVARCHAR(100) NOT NULL,
    [Source]      NVARCHAR(60)  NOT NULL,
    [Message]     NVARCHAR(MAX) NOT NULL,
    [User]        NVARCHAR(50)  NOT NULL,
    [StatusCode]  INT NOT NULL,
    [TimeUtc]     DATETIME NOT NULL,
    [Sequence]    INT IDENTITY (1, 1) NOT NULL,
    [AllXml]      NVARCHAR(MAX) NOT NULL 
) 
GO

ALTER TABLE [{schemaName}].[{tableName}] WITH NOCHECK ADD 
    CONSTRAINT [PK_{tableName}] PRIMARY KEY NONCLUSTERED ([ErrorId]) ON [PRIMARY] 
GO

ALTER TABLE [{schemaName}].[{tableName}] ADD 
    CONSTRAINT [DF_{tableName}_ErrorId] DEFAULT (NEWID()) FOR [ErrorId]
GO

CREATE NONCLUSTERED INDEX [IX_{tableName}_App_Time_Seq] ON [{schemaName}].[{tableName}] 
(
    [Application]   ASC,
    [TimeUtc]       DESC,
    [Sequence]      DESC
) 
ON [PRIMARY]";
        }

        public static SqlCommand CheckTable(string schemaName, string tableName)
        {
            return new SqlCommand
            {
                CommandText = $@"
SELECT 1 
WHERE EXISTS (
   SELECT 1
   FROM   INFORMATION_SCHEMA.TABLES 
   WHERE  TABLE_SCHEMA = '{schemaName}'
   AND    TABLE_NAME = '{tableName}'
   )
"
            };
        }

        public static SqlCommand LogError(
            Guid id,
            string appName,
            string hostName,
            string typeName,
            string source,
            string message,
            string user,
            int statusCode,
            DateTime time,
            string xml,
            string schemaName,
            string tableName,
            bool isReviewed)
        {
            var command = new SqlCommand
            {
                CommandText = $@"
/* elmah */
INSERT INTO [{schemaName}].[{tableName}] (ErrorId, Application, Host, Type, Source, Message, ""User"", StatusCode, TimeUtc, AllXml, IsReviewed, ApplicationName)
VALUES (@ErrorId, @Application, @Host, @Type, @Source, @Message, @User, @StatusCode, @TimeUtc, @AllXml, @IsReviewed, @ApplicationName)
"
            };
            command.Parameters.Add(new SqlParameter("ErrorId", id));
            command.Parameters.Add(new SqlParameter("Application", appName));
            command.Parameters.Add(new SqlParameter("Host", hostName));
            command.Parameters.Add(new SqlParameter("Type", typeName));
            command.Parameters.Add(new SqlParameter("Source", source));
            command.Parameters.Add(new SqlParameter("Message", message));
            command.Parameters.Add(new SqlParameter("User", user));
            command.Parameters.Add(new SqlParameter("StatusCode", statusCode));
            command.Parameters.Add(new SqlParameter("TimeUtc", time.ToUniversalTime()));
            command.Parameters.Add(new SqlParameter("AllXml", xml));
            command.Parameters.Add(new SqlParameter("IsReviewed", isReviewed));
            command.Parameters.Add(new SqlParameter("ApplicationName", appName));

            return command;
        }

        public static SqlCommand GetErrorXml(
            string appName,
            Guid id,
            string schemaName,
            string tableName)
        {
            var command = new SqlCommand
            {
                CommandText = $@"
SELECT AllXml FROM [{schemaName}].[{tableName}]
WHERE 
    Application = @Application 
    AND ErrorId = @ErrorId
"
            };

            command.Parameters.Add(new SqlParameter("Application", appName));
            command.Parameters.Add(new SqlParameter("ErrorId", id));

            return command;
        }

        public static SqlCommand GetErrorsXml(
            string appName,
            int errorIndex,
            int pageSize,
            string schemaName,
            string tableName)
        {
            var command = new SqlCommand
            {
                CommandText = $@"
SELECT ErrorId, AllXml, IsReviewed FROM [{schemaName}].[{tableName}]
WHERE
    Application = @Application
ORDER BY [Sequence] DESC
OFFSET     @offset ROWS
FETCH NEXT @limit ROWS ONLY;
"
            };

            command.Parameters.Add("@Application", SqlDbType.NVarChar, MaxAppNameLength).Value = appName;
            command.Parameters.Add("@offset", SqlDbType.Int).Value = errorIndex;
            command.Parameters.Add("@limit", SqlDbType.Int).Value = pageSize;

            return command;
        }

        public static SqlCommand GetErrorsXmlTotal(string appName,
            string schemaName,
            string tableName)
        {
            var command = new SqlCommand
            {
                CommandText = $"SELECT COUNT(*) FROM [{schemaName}].[{tableName}] WHERE Application = @Application"
            };
            command.Parameters.Add("@Application", SqlDbType.NVarChar, MaxAppNameLength).Value = appName;
            return command;
        }

        public static string CreateMigrationsSql(string schemaName, string tableName)
        {
            return $@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('[{schemaName}].[{tableName}]') AND name = 'IsReviewed')
BEGIN
    ALTER TABLE [{schemaName}].[{tableName}] ADD [IsReviewed] BIT NOT NULL CONSTRAINT [DF_{tableName}_IsReviewed] DEFAULT (0);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('[{schemaName}].[{tableName}]') AND name = 'ApplicationName')
BEGIN
    ALTER TABLE [{schemaName}].[{tableName}] ADD [ApplicationName] NVARCHAR(100) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('[{schemaName}].[{tableName}]') AND name = 'IX_{tableName}_Filtered')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_{tableName}_Filtered] ON [{schemaName}].[{tableName}] (TimeUtc DESC, Application, StatusCode);
END
GO

IF EXISTS (SELECT 1 FROM sys.procedures WHERE object_id = OBJECT_ID('[{schemaName}].[{tableName}_BulkDelete]'))
BEGIN
    DROP PROCEDURE [{schemaName}].[{tableName}_BulkDelete];
END
GO

CREATE PROCEDURE [{schemaName}].[{tableName}_BulkDelete]
    @App NVARCHAR(60),
    @IdsJson NVARCHAR(MAX)
AS
BEGIN
    DELETE FROM [{schemaName}].[{tableName}]
    WHERE Application = @App
      AND ErrorId IN (SELECT CAST(value AS UNIQUEIDENTIFIER) FROM OPENJSON(@IdsJson))
END
GO

IF EXISTS (SELECT 1 FROM sys.procedures WHERE object_id = OBJECT_ID('[{schemaName}].[{tableName}_FilteredCount]'))
BEGIN
    DROP PROCEDURE [{schemaName}].[{tableName}_FilteredCount];
END
GO

CREATE PROCEDURE [{schemaName}].[{tableName}_FilteredCount]
    @App NVARCHAR(60),
    @Type NVARCHAR(100) = NULL,
    @Message NVARCHAR(MAX) = NULL,
    @Host NVARCHAR(50) = NULL,
    @User NVARCHAR(50) = NULL,
    @StatusCode INT = NULL,
    @From DATETIME = NULL,
    @To DATETIME = NULL,
    @IsReviewed BIT = NULL
AS
BEGIN
    SELECT COUNT(*) FROM [{schemaName}].[{tableName}]
    WHERE Application = @App
      AND (@Type IS NULL OR Type LIKE '%' + @Type + '%')
      AND (@Message IS NULL OR Message LIKE '%' + @Message + '%' OR [User] LIKE '%' + @Message + '%' OR AllXml LIKE '%' + @Message + '%')
      AND (@Host IS NULL OR Host LIKE '%' + @Host + '%')
      AND (@User IS NULL OR [User] LIKE '%' + @User + '%')
      AND (@StatusCode IS NULL OR StatusCode = @StatusCode)
      AND (@From IS NULL OR TimeUtc >= @From)
      AND (@To IS NULL OR TimeUtc <= @To)
      AND (@IsReviewed IS NULL OR IsReviewed = @IsReviewed)
END
GO";
        }
    }

    public override async Task DeleteErrorsAsync(IEnumerable<string> errorIds, CancellationToken cancellationToken = default)
    {
        if (errorIds == null || !errorIds.Any()) return;

        var json = System.Text.Json.JsonSerializer.Serialize(errorIds);

        using var connection = new SqlConnection(ConnectionString);
        using var command = new SqlCommand($"[{DatabaseSchemaName}].[{DatabaseTableName}_BulkDelete]", connection);
        command.CommandType = CommandType.StoredProcedure;
        command.Parameters.Add(new SqlParameter("App", ApplicationName));
        command.Parameters.Add(new SqlParameter("IdsJson", json));

        await connection.OpenAsync(cancellationToken);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public override async Task DeleteAllErrorsAsync(string applicationName = null, CancellationToken cancellationToken = default)
    {
        var app = string.IsNullOrEmpty(applicationName) ? ApplicationName : applicationName;

        using var connection = new SqlConnection(ConnectionString);
        using var command = new SqlCommand($"DELETE FROM [{DatabaseSchemaName}].[{DatabaseTableName}] WHERE Application = @Application", connection);
        command.Parameters.Add(new SqlParameter("Application", app));

        await connection.OpenAsync(cancellationToken);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public override async Task SetReviewedAsync(string id, bool isReviewed, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(id)) return;
        var errorGuid = new Guid(id);

        using var connection = new SqlConnection(ConnectionString);
        using var command = new SqlCommand($"UPDATE [{DatabaseSchemaName}].[{DatabaseTableName}] SET IsReviewed = @IsReviewed WHERE ErrorId = @ErrorId AND Application = @Application", connection);
        command.Parameters.Add(new SqlParameter("IsReviewed", isReviewed));
        command.Parameters.Add(new SqlParameter("ErrorId", errorGuid));
        command.Parameters.Add(new SqlParameter("Application", ApplicationName));

        await connection.OpenAsync(cancellationToken);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public override async Task<int> GetErrorsAsync(
        int errorIndex, 
        int pageSize, 
        ICollection<ErrorLogEntry> errorEntryList, 
        ErrorLogFilter filter, 
        CancellationToken cancellationToken = default)
    {
        if (filter == null)
        {
            var entries = new List<ErrorLogEntry>();
            var count = GetErrors(errorIndex, pageSize, entries);
            foreach (var entry in entries) errorEntryList.Add(entry);
            return count;
        }

        using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);

        int totalCount;
        using (var cmdCount = new SqlCommand($"[{DatabaseSchemaName}].[{DatabaseTableName}_FilteredCount]", connection))
        {
            cmdCount.CommandType = CommandType.StoredProcedure;
            cmdCount.Parameters.Add(new SqlParameter("App", ApplicationName));
            cmdCount.Parameters.Add(new SqlParameter("Type", (object)filter.Type ?? DBNull.Value));
            cmdCount.Parameters.Add(new SqlParameter("Message", (object)filter.Message ?? DBNull.Value));
            cmdCount.Parameters.Add(new SqlParameter("Host", (object)filter.Host ?? DBNull.Value));
            cmdCount.Parameters.Add(new SqlParameter("User", (object)filter.User ?? DBNull.Value));
            cmdCount.Parameters.Add(new SqlParameter("StatusCode", (object)filter.StatusCode ?? DBNull.Value));
            cmdCount.Parameters.Add(new SqlParameter("From", (object)filter.From ?? DBNull.Value));
            cmdCount.Parameters.Add(new SqlParameter("To", (object)filter.To ?? DBNull.Value));
            cmdCount.Parameters.Add(new SqlParameter("IsReviewed", (object)filter.IsReviewed ?? DBNull.Value));

            var countResult = await cmdCount.ExecuteScalarAsync(cancellationToken);
            totalCount = Convert.ToInt32(countResult);
        }

        if (pageSize > 0)
        {
            var query = $@"
SELECT ErrorId, AllXml, IsReviewed FROM [{DatabaseSchemaName}].[{DatabaseTableName}]
WHERE Application = @Application
  AND (@Type IS NULL OR Type LIKE '%' + @Type + '%')
  AND (@Message IS NULL OR Message LIKE '%' + @Message + '%' OR [User] LIKE '%' + @Message + '%' OR AllXml LIKE '%' + @Message + '%')
  AND (@Host IS NULL OR Host LIKE '%' + @Host + '%')
  AND (@User IS NULL OR [User] LIKE '%' + @User + '%')
  AND (@StatusCode IS NULL OR StatusCode = @StatusCode)
  AND (@From IS NULL OR TimeUtc >= @From)
  AND (@To IS NULL OR TimeUtc <= @To)
  AND (@IsReviewed IS NULL OR IsReviewed = @IsReviewed)
ORDER BY [Sequence] DESC
OFFSET @offset ROWS
FETCH NEXT @limit ROWS ONLY;
";
            using var cmdFetch = new SqlCommand(query, connection);
            cmdFetch.Parameters.Add(new SqlParameter("Application", ApplicationName));
            cmdFetch.Parameters.Add(new SqlParameter("Type", (object)filter.Type ?? DBNull.Value));
            cmdFetch.Parameters.Add(new SqlParameter("Message", (object)filter.Message ?? DBNull.Value));
            cmdFetch.Parameters.Add(new SqlParameter("Host", (object)filter.Host ?? DBNull.Value));
            cmdFetch.Parameters.Add(new SqlParameter("User", (object)filter.User ?? DBNull.Value));
            cmdFetch.Parameters.Add(new SqlParameter("StatusCode", (object)filter.StatusCode ?? DBNull.Value));
            cmdFetch.Parameters.Add(new SqlParameter("From", (object)filter.From ?? DBNull.Value));
            cmdFetch.Parameters.Add(new SqlParameter("To", (object)filter.To ?? DBNull.Value));
            cmdFetch.Parameters.Add(new SqlParameter("IsReviewed", (object)filter.IsReviewed ?? DBNull.Value));
            cmdFetch.Parameters.Add(new SqlParameter("offset", errorIndex));
            cmdFetch.Parameters.Add(new SqlParameter("limit", pageSize));

            using var reader = await cmdFetch.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var id = reader.GetGuid(0);
                var xml = reader.GetString(1);
                var isReviewedDb = reader.GetBoolean(2);
                var error = ErrorXml.DecodeString(xml);
                error.IsReviewed = isReviewedDb;
                errorEntryList.Add(new ErrorLogEntry(this, id.ToString(), error));
            }
        }

        return totalCount;
    }
}