using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;

namespace ElmahCore.Postgresql;

/// <summary>
///     An <see cref="ErrorLog" /> implementation that uses PostgreSQL
///     as its backing store.
/// </summary>
[UsedImplicitly]
public class PgsqlErrorLog : ErrorLog
{
    private const int MaxAppNameLength = 60;
    private readonly bool _logAllXml;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PgsqlErrorLog" /> class
    ///     using a dictionary of configured settings.
    /// </summary>
    public PgsqlErrorLog(IOptions<ElmahOptions> option) : this(option.Value.ConnectionString, option.Value.CreateTablesIfNotExist,
        option.Value.LogAllXml)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="PgsqlErrorLog" /> class
    ///     to use a specific connection string for connecting to the database.
    /// </summary>
    public PgsqlErrorLog(string connectionString, bool createTablesIfNotExist, bool logAllXml = true)
    {
        if (string.IsNullOrEmpty(connectionString))
            throw new ArgumentNullException(nameof(connectionString));

        ConnectionString = connectionString;
        _logAllXml = logAllXml;

        if (createTablesIfNotExist)
            CreateTableIfNotExists();
    }

    /// <summary>
    ///     Gets the name of this error log implementation.
    /// </summary>
    public override string Name => "PostgreSQL Error Log";

    /// <summary>
    ///     Gets the connection string used by the log to connect to the database.
    /// </summary>
    public virtual string ConnectionString { get; }

    public override string Log(Error error)
    {
        var id = Guid.NewGuid();

        Log(id, error);

        return id.ToString();
    }

    public override void Log(Guid id, Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        var errorXml = _logAllXml
            ? ErrorXml.EncodeString(error)
            : "<error message=\"AllXml logging disabled\" />";

        using var connection = new NpgsqlConnection(ConnectionString);
        using var command = Commands.LogError(id, ApplicationName, error.HostName, error.Type, error.Source,
            error.Message, error.User, error.StatusCode, error.Time, errorXml, error.IsReviewed);
        command.Connection = connection;
        connection.Open();
        command.ExecuteNonQuery();
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

        using (var connection = new NpgsqlConnection(ConnectionString))
        using (var command = Commands.GetErrorXml(ApplicationName, errorGuid))
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

        using var connection = new NpgsqlConnection(ConnectionString);
        connection.Open();

        using (var command = Commands.GetErrorsXml(ApplicationName, errorIndex, pageSize))
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

        using (var command = Commands.GetErrorsXmlTotal(ApplicationName))
        {
            command.Connection = connection;
            return Convert.ToInt32(command.ExecuteScalar());
        }
    }

    /// <summary>
    ///     Creates the necessary tables and sequences used by this implementation
    /// </summary>
    private void CreateTableIfNotExists()
    {
        using var connection = new NpgsqlConnection(ConnectionString);
        connection.Open();

        using (var cmdCheck = Commands.CheckTable())
        {
            cmdCheck.Connection = connection;
            var exists = (bool)cmdCheck.ExecuteScalar();

            if (!exists)
            {
                using var cmdCreate = Commands.CreateTable();
                cmdCreate.Connection = connection;
                cmdCreate.ExecuteNonQuery();
            }
        }

        // Run migrations
        using (var cmdMigrate = Commands.CreateMigrations())
        {
            cmdMigrate.Connection = connection;
            cmdMigrate.ExecuteNonQuery();
        }
    }

    private static class Commands
    {
        public static NpgsqlCommand CheckTable()
        {
            var command = new NpgsqlCommand();
            command.CommandText =
                @"
SELECT EXISTS (
   SELECT 1
   FROM   information_schema.tables 
   WHERE  table_schema = 'public'
   AND    table_name = 'elmah_error'
   )
";
            return command;
        }

        public static NpgsqlCommand CreateTable()
        {
            var command = new NpgsqlCommand();
            command.CommandText =
                @"
CREATE SEQUENCE ELMAH_Error_SEQUENCE;
CREATE TABLE ELMAH_Error
(
    ErrorId		UUID NOT NULL,
    Application	VARCHAR(60) NOT NULL,
    Host 		VARCHAR(50) NOT NULL,
    Type		VARCHAR(100) NOT NULL,
    Source		VARCHAR(60)  NOT NULL,
    Message		VARCHAR(500) NOT NULL,
    ""User""		VARCHAR(50)  NOT NULL,
    StatusCode	INT NOT NULL,
    TimeUtc		TIMESTAMP NOT NULL,
    Sequence	INT NOT NULL DEFAULT NEXTVAL('ELMAH_Error_SEQUENCE'),
    AllXml		TEXT NOT NULL
);

ALTER TABLE ELMAH_Error ADD CONSTRAINT PK_ELMAH_Error PRIMARY KEY (ErrorId);

CREATE INDEX IX_ELMAH_Error_App_Time_Seq ON ELMAH_Error USING BTREE
(
    Application   ASC,
    TimeUtc       DESC,
    Sequence      DESC
);
";

            return command;
        }

        public static NpgsqlCommand LogError(
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
            bool isReviewed)
        {
            var command = new NpgsqlCommand();
            command.CommandText =
                @"
/* elmah */
INSERT INTO Elmah_Error (ErrorId, Application, Host, Type, Source, Message, ""User"", StatusCode, TimeUtc, AllXml, IsReviewed, ApplicationName)
VALUES (@ErrorId, @Application, @Host, @Type, @Source, @Message, @User, @StatusCode, @TimeUtc, @AllXml, @IsReviewed, @ApplicationName)
";
            command.Parameters.Add(new NpgsqlParameter("ErrorId", id));
            command.Parameters.Add(new NpgsqlParameter("Application", appName));
            command.Parameters.Add(new NpgsqlParameter("Host", hostName));
            command.Parameters.Add(new NpgsqlParameter("Type", typeName));
            command.Parameters.Add(new NpgsqlParameter("Source", source));
            command.Parameters.Add(new NpgsqlParameter("Message", message));
            command.Parameters.Add(new NpgsqlParameter("User", user));
            command.Parameters.Add(new NpgsqlParameter("StatusCode", statusCode));
            command.Parameters.Add(new NpgsqlParameter("TimeUtc", time.ToUniversalTime()));
            command.Parameters.Add(new NpgsqlParameter("AllXml", xml));
            command.Parameters.Add(new NpgsqlParameter("IsReviewed", isReviewed));
            command.Parameters.Add(new NpgsqlParameter("ApplicationName", appName));

            return command;
        }

        public static NpgsqlCommand GetErrorXml(string appName, Guid id)
        {
            var command = new NpgsqlCommand();

            command.CommandText =
                @"
SELECT AllXml 
FROM Elmah_Error 
WHERE 
    Application = @Application 
    AND ErrorId = @ErrorId
";

            command.Parameters.Add(new NpgsqlParameter("Application", appName));
            command.Parameters.Add(new NpgsqlParameter("ErrorId", id));

            return command;
        }

        public static NpgsqlCommand GetErrorsXml(string appName, int errorIndex, int pageSize)
        {
            var command = new NpgsqlCommand();

            command.CommandText =
                @"
SELECT ErrorId, AllXml, IsReviewed FROM Elmah_Error
WHERE
    Application = @Application
ORDER BY Sequence DESC
OFFSET @offset
LIMIT @limit
";

            command.Parameters.Add("@Application", NpgsqlDbType.Text, MaxAppNameLength).Value = appName;
            command.Parameters.Add("@offset", NpgsqlDbType.Integer).Value = errorIndex;
            command.Parameters.Add("@limit", NpgsqlDbType.Integer).Value = pageSize;

            return command;
        }

        public static NpgsqlCommand GetErrorsXmlTotal(string appName)
        {
            var command = new NpgsqlCommand();
            command.CommandText = "SELECT COUNT(*) FROM Elmah_Error WHERE Application = @Application";
            command.Parameters.Add("@Application", NpgsqlDbType.Text, MaxAppNameLength).Value = appName;
            return command;
        }

        public static NpgsqlCommand CreateMigrations()
        {
            var command = new NpgsqlCommand();
            command.CommandText =
                @"
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'elmah_error' AND column_name = 'isreviewed') THEN
        ALTER TABLE ELMAH_Error ADD COLUMN IsReviewed BOOLEAN NOT NULL DEFAULT FALSE;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'elmah_error' AND column_name = 'applicationname') THEN
        ALTER TABLE ELMAH_Error ADD COLUMN ApplicationName VARCHAR(100) NULL;
    END IF;
END
$$;

CREATE INDEX IF NOT EXISTS IX_ELMAH_Error_Filtered ON ELMAH_Error (TimeUtc DESC, Application, StatusCode);
";
            return command;
        }
    }

    public override async Task DeleteErrorsAsync(IEnumerable<string> errorIds, CancellationToken cancellationToken = default)
    {
        if (errorIds == null || !errorIds.Any()) return;

        var guidList = errorIds.Select(id => new Guid(id)).ToList();

        using var connection = new NpgsqlConnection(ConnectionString);
        using var command = new NpgsqlCommand("DELETE FROM Elmah_Error WHERE Application = @Application AND ErrorId = ANY(@Ids)", connection);
        command.Parameters.Add(new NpgsqlParameter("Application", ApplicationName));
        command.Parameters.Add(new NpgsqlParameter("Ids", guidList));

        await connection.OpenAsync(cancellationToken);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public override async Task DeleteAllErrorsAsync(string applicationName = null, CancellationToken cancellationToken = default)
    {
        var app = string.IsNullOrEmpty(applicationName) ? ApplicationName : applicationName;

        using var connection = new NpgsqlConnection(ConnectionString);
        using var command = new NpgsqlCommand("DELETE FROM Elmah_Error WHERE Application = @Application", connection);
        command.Parameters.Add(new NpgsqlParameter("Application", app));

        await connection.OpenAsync(cancellationToken);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public override async Task SetReviewedAsync(string id, bool isReviewed, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(id)) return;
        var errorGuid = new Guid(id);

        using var connection = new NpgsqlConnection(ConnectionString);
        using var command = new NpgsqlCommand("UPDATE Elmah_Error SET IsReviewed = @IsReviewed WHERE ErrorId = @ErrorId AND Application = @Application", connection);
        command.Parameters.Add(new NpgsqlParameter("IsReviewed", isReviewed));
        command.Parameters.Add(new NpgsqlParameter("ErrorId", errorGuid));
        command.Parameters.Add(new NpgsqlParameter("Application", ApplicationName));

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

        using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);

        // Build count query
        var countQuery = "SELECT COUNT(*) FROM Elmah_Error WHERE Application = @Application";
        var countCmd = new NpgsqlCommand(string.Empty, connection);
        countCmd.Parameters.Add(new NpgsqlParameter("Application", ApplicationName));

        var filterSql = BuildFilterSqlPg(filter, countCmd.Parameters);
        countCmd.CommandText = countQuery + filterSql;

        var countObj = await countCmd.ExecuteScalarAsync(cancellationToken);
        var totalCount = Convert.ToInt32(countObj);

        if (pageSize > 0)
        {
            var fetchQuery = "SELECT ErrorId, AllXml, IsReviewed FROM Elmah_Error WHERE Application = @Application" + filterSql;
            fetchQuery += " ORDER BY Sequence DESC OFFSET @offset LIMIT @limit";

            var fetchCmd = new NpgsqlCommand(fetchQuery, connection);
            // Copy parameters to avoid adding duplicates
            foreach (NpgsqlParameter p in countCmd.Parameters)
            {
                fetchCmd.Parameters.Add(p.Clone());
            }
            fetchCmd.Parameters.Add(new NpgsqlParameter("offset", errorIndex));
            fetchCmd.Parameters.Add(new NpgsqlParameter("limit", pageSize));

            using var reader = await fetchCmd.ExecuteReaderAsync(cancellationToken);
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

    private string BuildFilterSqlPg(ErrorLogFilter filter, NpgsqlParameterCollection parameters)
    {
        var sql = "";
        if (filter.Type != null)
        {
            sql += " AND Type ILIKE @Type";
            parameters.Add(new NpgsqlParameter("Type", "%" + filter.Type + "%"));
        }
        if (filter.Message != null)
        {
            sql += " AND Message ILIKE @Message";
            parameters.Add(new NpgsqlParameter("Message", "%" + filter.Message + "%"));
        }
        if (filter.Host != null)
        {
            sql += " AND Host ILIKE @Host";
            parameters.Add(new NpgsqlParameter("Host", "%" + filter.Host + "%"));
        }
        if (filter.User != null)
        {
            sql += " AND \"User\" ILIKE @User";
            parameters.Add(new NpgsqlParameter("User", "%" + filter.User + "%"));
        }
        if (filter.StatusCode.HasValue)
        {
            sql += " AND StatusCode = @StatusCode";
            parameters.Add(new NpgsqlParameter("StatusCode", filter.StatusCode.Value));
        }
        if (filter.From.HasValue)
        {
            sql += " AND TimeUtc >= @From";
            parameters.Add(new NpgsqlParameter("From", filter.From.Value.ToUniversalTime()));
        }
        if (filter.To.HasValue)
        {
            sql += " AND TimeUtc <= @To";
            parameters.Add(new NpgsqlParameter("To", filter.To.Value.ToUniversalTime()));
        }
        if (filter.IsReviewed.HasValue)
        {
            sql += " AND IsReviewed = @IsReviewed";
            parameters.Add(new NpgsqlParameter("IsReviewed", filter.IsReviewed.Value));
        }
        return sql;
    }
}