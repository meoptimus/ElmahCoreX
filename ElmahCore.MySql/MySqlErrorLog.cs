using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using MySql.Data.MySqlClient;

namespace ElmahCore.MySql;

/// <summary>
///     An <see cref="ErrorLog" /> implementation that uses MySQL
///     as its backing store.
/// </summary>
public class MySqlErrorLog : ErrorLog
{
    private readonly bool _logAllXml;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MySqlErrorLog" /> class
    ///     using a dictionary of configured settings.
    /// </summary>
    public MySqlErrorLog(IOptions<ElmahOptions> option) : this(option.Value.ConnectionString,
        option.Value.CreateTablesIfNotExist, option.Value.LogAllXml)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="MySqlErrorLog" /> class
    ///     to use a specific connection string for connecting to the database.
    /// </summary>
    public MySqlErrorLog(string connectionString, bool createTablesIfNotExist = true, bool logAllXml = true)
    {
        if (string.IsNullOrEmpty(connectionString))
            throw new ArgumentNullException(nameof(connectionString));

        ConnectionString = connectionString;
        _logAllXml = logAllXml;

        if (createTablesIfNotExist)
            CreateTableIfNotExist();
    }

    /// <summary>
    ///     Gets the name of this error log implementation.
    /// </summary>
    public override string Name => "MySQL Error Log";

    /// <summary>
    ///     Gets the connection string used by the log to connect to the database.
    /// </summary>
    protected virtual string ConnectionString { get; }

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

        using var connection = new MySqlConnection(ConnectionString);
        using var command = CommandExtension.LogError(id, ApplicationName, error.HostName, error.Type,
            error.Source, error.Message, error.User, error.StatusCode, error.Time, errorXml, error.IsReviewed);
        connection.Open();
        command.Connection = connection;
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

        using (var connection = new MySqlConnection(ConnectionString))
        using (var command = CommandExtension.GetErrorXml(ApplicationName, errorGuid))
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

        using var connection = new MySqlConnection(ConnectionString);
        connection.Open();

        using (var command = CommandExtension.GetErrorsXml(ApplicationName, errorIndex, pageSize))
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

        return GetTotalErrorsXml(connection);
    }

    /// <summary>
    ///     Creates the necessary tables used by this implementation
    /// </summary>
    private void CreateTableIfNotExist()
    {
        using var connection = new MySqlConnection(ConnectionString);
        connection.Open();
        var databaseName = connection.Database;

        using var commandCheck = CommandExtension.CheckTable(databaseName);
        commandCheck.Connection = connection;
        var exists = Convert.ToBoolean(commandCheck.ExecuteScalar());

        if (!exists)
        {
            using var commandCreate = CommandExtension.CreateTable();
            commandCreate.Connection = connection;
            commandCreate.ExecuteNonQuery();
        }

        RunMigrations(connection, databaseName);
    }

    private void RunMigrations(MySqlConnection connection, string databaseName)
    {
        // 1. Check IsReviewed column
        using (var cmdCheck = new MySqlCommand(
            "SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = @Database AND table_name = 'ELMAH_Error' AND column_name = 'IsReviewed'", 
            connection))
        {
            cmdCheck.Parameters.AddWithValue("@Database", databaseName);
            var exists = Convert.ToInt32(cmdCheck.ExecuteScalar()) > 0;
            if (!exists)
            {
                using var cmdAlter = new MySqlCommand("ALTER TABLE ELMAH_Error ADD COLUMN IsReviewed TINYINT(1) NOT NULL DEFAULT 0", connection);
                cmdAlter.ExecuteNonQuery();
            }
        }

        // 2. Check ApplicationName column
        using (var cmdCheck = new MySqlCommand(
            "SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = @Database AND table_name = 'ELMAH_Error' AND column_name = 'ApplicationName'", 
            connection))
        {
            cmdCheck.Parameters.AddWithValue("@Database", databaseName);
            var exists = Convert.ToInt32(cmdCheck.ExecuteScalar()) > 0;
            if (!exists)
            {
                using var cmdAlter = new MySqlCommand("ALTER TABLE ELMAH_Error ADD COLUMN ApplicationName VARCHAR(100) NULL", connection);
                cmdAlter.ExecuteNonQuery();
            }
        }

        // 3. Check Index
        using (var cmdCheck = new MySqlCommand(
            "SELECT COUNT(*) FROM information_schema.statistics WHERE table_schema = @Database AND table_name = 'ELMAH_Error' AND index_name = 'IX_ELMAH_Error_Filtered'", 
            connection))
        {
            cmdCheck.Parameters.AddWithValue("@Database", databaseName);
            var exists = Convert.ToInt32(cmdCheck.ExecuteScalar()) > 0;
            if (!exists)
            {
                try
                {
                    using var cmdIndex = new MySqlCommand("CREATE INDEX IX_ELMAH_Error_Filtered ON ELMAH_Error (TimeUtc DESC, Application, StatusCode)", connection);
                    cmdIndex.ExecuteNonQuery();
                }
                catch
                {
                    // ignore if index somehow exists or cannot be created
                }
            }
        }
    }

    private int GetTotalErrorsXml(MySqlConnection connection)
    {
        using var command = CommandExtension.GetTotalErrorsXml(ApplicationName);
        command.Connection = connection;
        return Convert.ToInt32(command.ExecuteScalar());
    }

    public override async Task DeleteErrorsAsync(IEnumerable<string> errorIds, CancellationToken cancellationToken = default)
    {
        if (errorIds == null || !errorIds.Any()) return;

        using var connection = new MySqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var idList = errorIds.ToList();
        var paramNames = new List<string>();
        using var command = new MySqlCommand();
        command.Connection = connection;

        for (int i = 0; i < idList.Count; i++)
        {
            var paramName = "@id" + i;
            paramNames.Add(paramName);
            command.Parameters.AddWithValue(paramName, idList[i]);
        }

        command.CommandText = $"DELETE FROM ELMAH_Error WHERE Application = @Application AND ErrorId IN ({string.Join(",", paramNames)})";
        command.Parameters.AddWithValue("@Application", ApplicationName);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public override async Task DeleteAllErrorsAsync(string applicationName = null, CancellationToken cancellationToken = default)
    {
        var app = string.IsNullOrEmpty(applicationName) ? ApplicationName : applicationName;

        using var connection = new MySqlConnection(ConnectionString);
        using var command = new MySqlCommand("DELETE FROM ELMAH_Error WHERE Application = @Application", connection);
        command.Parameters.AddWithValue("@Application", app);

        await connection.OpenAsync(cancellationToken);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public override async Task SetReviewedAsync(string id, bool isReviewed, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(id)) return;

        using var connection = new MySqlConnection(ConnectionString);
        using var command = new MySqlCommand("UPDATE ELMAH_Error SET IsReviewed = @IsReviewed WHERE ErrorId = @ErrorId AND Application = @Application", connection);
        command.Parameters.AddWithValue("@IsReviewed", isReviewed ? 1 : 0);
        command.Parameters.AddWithValue("@ErrorId", id);
        command.Parameters.AddWithValue("@Application", ApplicationName);

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

        using var connection = new MySqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);

        // Build count query
        var countQuery = "SELECT COUNT(*) FROM ELMAH_Error WHERE Application = @Application";
        var countCmd = new MySqlCommand(string.Empty, connection);
        countCmd.Parameters.AddWithValue("Application", ApplicationName);

        var filterSql = BuildFilterSqlMySql(filter, countCmd.Parameters);
        countCmd.CommandText = countQuery + filterSql;

        var countObj = await countCmd.ExecuteScalarAsync(cancellationToken);
        var totalCount = Convert.ToInt32(countObj);

        if (pageSize > 0)
        {
            var fetchQuery = "SELECT ErrorId, AllXml, IsReviewed FROM ELMAH_Error WHERE Application = @Application" + filterSql;
            fetchQuery += " ORDER BY Sequence DESC LIMIT @limit OFFSET @offset";

            var fetchCmd = new MySqlCommand(fetchQuery, connection);
            foreach (MySqlParameter p in countCmd.Parameters)
            {
                fetchCmd.Parameters.Add(p.Clone());
            }
            fetchCmd.Parameters.Add(new MySqlParameter("offset", errorIndex));
            fetchCmd.Parameters.Add(new MySqlParameter("limit", pageSize));

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

    private string BuildFilterSqlMySql(ErrorLogFilter filter, MySqlParameterCollection parameters)
    {
        var sql = "";
        if (filter.Type != null)
        {
            sql += " AND Type LIKE @Type";
            parameters.Add(new MySqlParameter("Type", "%" + filter.Type + "%"));
        }
        if (filter.Message != null)
        {
            sql += " AND (Message LIKE @Message OR User LIKE @Message OR AllXml LIKE @Message)";
            parameters.Add(new MySqlParameter("Message", "%" + filter.Message + "%"));
        }
        if (filter.Host != null)
        {
            sql += " AND Host LIKE @Host";
            parameters.Add(new MySqlParameter("Host", "%" + filter.Host + "%"));
        }
        if (filter.User != null)
        {
            sql += " AND User LIKE @User";
            parameters.Add(new MySqlParameter("User", "%" + filter.User + "%"));
        }
        if (filter.StatusCode.HasValue)
        {
            sql += " AND StatusCode = @StatusCode";
            parameters.Add(new MySqlParameter("StatusCode", filter.StatusCode.Value));
        }
        if (filter.From.HasValue)
        {
            sql += " AND TimeUtc >= @From";
            parameters.Add(new MySqlParameter("From", filter.From.Value.ToUniversalTime()));
        }
        if (filter.To.HasValue)
        {
            sql += " AND TimeUtc <= @To";
            parameters.Add(new MySqlParameter("To", filter.To.Value.ToUniversalTime()));
        }
        if (filter.IsReviewed.HasValue)
        {
            sql += " AND IsReviewed = @IsReviewed";
            parameters.Add(new MySqlParameter("IsReviewed", filter.IsReviewed.Value ? 1 : 0));
        }
        return sql;
    }
}