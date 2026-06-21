using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

namespace ElmahCore;

/// <summary>
///     An <see cref="ErrorLog" /> implementation that uses XML files stored on
///     disk as its backing store.
/// </summary>

// ReSharper disable once UnusedType.Global
public class XmlFileErrorLog : ErrorLog
{
    private readonly string _logPath;

    /// <summary>
    ///     Initializes a new instance of the <see cref="XmlFileErrorLog" /> class
    ///     using a dictionary of configured settings.
    /// </summary>
    public XmlFileErrorLog(IOptions<ElmahOptions> options, IWebHostEnvironment hostingEnvironment)
    {
        _logPath = options.Value.LogPath;
        if (_logPath.StartsWith("~/"))
            _logPath = Path.Combine(hostingEnvironment.WebRootPath ?? hostingEnvironment.ContentRootPath,
                _logPath.Substring(2));
    }


    /// <summary>
    ///     Gets the path to where the log is stored.
    /// </summary>

    protected virtual string LogPath => _logPath;

    /// <summary>
    ///     Gets the name of this error log implementation.
    /// </summary>

    public override string Name => "XML File-Based Error Log";

    /// <summary>
    ///     Logs an error to the database.
    /// </summary>
    /// <remarks>
    ///     Logs an error as a single XML file stored in a folder. XML files are named with a
    ///     sortable date and a unique identifier. Currently the XML files are stored indefinitely.
    ///     As they are stored as files, they may be managed using standard scheduled jobs.
    /// </remarks>
    public override string Log(Error error)
    {
        var errorId = Guid.NewGuid();

        Log(errorId, error);

        return errorId.ToString();
    }

    public override void Log(Guid id, Error error)
    {
        var logPath = LogPath;
        if (!Directory.Exists(logPath))
            Directory.CreateDirectory(logPath);

        var timeStamp = error.Time > DateTime.MinValue ? error.Time : DateTime.Now;

        var fileName = string.Format(CultureInfo.InvariantCulture,
            @"error-{0:yyyy-MM-ddHHmmssZ}-{1}.xml",
            /* 0 */ timeStamp.ToUniversalTime(),
            /* 1 */ id);

        var path = Path.Combine(logPath, fileName);

        try
        {
            using var writer = new XmlTextWriter(path, Encoding.UTF8) { Formatting = Formatting.Indented };
            writer.WriteStartElement("error");
            writer.WriteAttributeString("errorId", id.ToString());
            ErrorXml.Encode(error, writer);
            writer.WriteEndElement();
            writer.Flush();
        }
        catch (IOException)
        {
            // If an IOException is thrown during writing the file,
            // it means that we will have an either empty or
            // partially written XML file on disk. In both cases,
            // the file won't be valid and would cause an error in
            // the UI.
            File.Delete(path);
            throw;
        }
    }

    /// <summary>
    ///     Returns a page of errors from the folder in descending order
    ///     of logged time as defined by the sortable file names.
    /// </summary>
    public override int GetErrors(int errorIndex, int pageSize, ICollection<ErrorLogEntry> errorEntryList)
    {
        if (errorIndex < 0) throw new ArgumentOutOfRangeException(nameof(errorIndex), errorIndex, null);
        if (pageSize < 0) throw new ArgumentOutOfRangeException(nameof(pageSize), pageSize, null);

        if (!Directory.Exists(LogPath))
            return 0;
        var dir = new DirectoryInfo(LogPath);
        var infos = dir.GetFiles("error-*.xml");
        if (!infos.Any())
            return 0;

        var files = infos.Where(info => IsUserFile(info.Attributes))
            .OrderByDescending(info => info.Name, StringComparer.OrdinalIgnoreCase);

        if (errorEntryList == null) return files.Count(); // Return total

        var entries = files.Skip(errorIndex)
            .Take(pageSize)
            .Select(x => LoadErrorLogEntry(x.FullName));

        foreach (var entry in entries)
            errorEntryList.Add(entry);

        return files.Count(); // Return total
    }

    private ErrorLogEntry LoadErrorLogEntry(string path)
    {
        for (var i = 0; i < 5; i++)
            try
            {
                using var reader = XmlReader.Create(path, new XmlReaderSettings { CheckCharacters = false });
                if (!reader.IsStartElement("error"))
                    return null;

                var id = reader.GetAttribute("errorId");
                var error = ErrorXml.Decode(reader);
                return new ErrorLogEntry(this, id, error);
            }
            catch (IOException)
            {
                // ignored
            }

        throw new IOException("");
    }

    /// <summary>
    ///     Returns the specified error from the filesystem, or throws an exception if it does not exist.
    /// </summary>
    public override ErrorLogEntry GetError(string id)
    {
        try
        {
            id = new Guid(id).ToString(); // validate GUID
        }
        catch (FormatException e)
        {
            throw new ArgumentException(e.Message, id, e);
        }

        var file = new DirectoryInfo(LogPath).GetFiles($"error-*-{id}.xml")
            .FirstOrDefault();

        if (file == null || !IsUserFile(file.Attributes))
            return null;

        using var reader = XmlReader.Create(file.FullName, XmlReaderSettings);
        return new ErrorLogEntry(this, id, ErrorXml.Decode(reader));
    }

    private static readonly XmlReaderSettings XmlReaderSettings = new XmlReaderSettings { CheckCharacters = false };

    private static bool IsUserFile(FileAttributes attributes)
    {
        return 0 == (attributes & (FileAttributes.Directory |
                                   FileAttributes.Hidden |
                                   FileAttributes.System));
    }

    public override Task DeleteErrorsAsync(IEnumerable<string> errorIds, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(LogPath))
            return Task.CompletedTask;

        foreach (var id in errorIds)
        {
            try
            {
                var guidId = new Guid(id).ToString(); // validate GUID
                var files = new DirectoryInfo(LogPath).GetFiles($"error-*-{guidId}.xml");
                foreach (var file in files)
                {
                    if (IsUserFile(file.Attributes))
                    {
                        file.Delete();
                    }
                }
            }
            catch
            {
                // ignore
            }
        }
        return Task.CompletedTask;
    }

    public override Task DeleteAllErrorsAsync(string applicationName = null, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(LogPath))
            return Task.CompletedTask;

        var dir = new DirectoryInfo(LogPath);
        var files = dir.GetFiles("error-*.xml");
        foreach (var file in files)
        {
            if (IsUserFile(file.Attributes))
            {
                try
                {
                    if (string.IsNullOrEmpty(applicationName))
                    {
                        file.Delete();
                    }
                    else
                    {
                        // Check if file matches applicationName
                        using (var reader = XmlReader.Create(file.FullName, new XmlReaderSettings { CheckCharacters = false }))
                        {
                            if (reader.IsStartElement("error"))
                            {
                                var app = reader.GetAttribute("application");
                                if (string.Equals(app, applicationName, StringComparison.OrdinalIgnoreCase))
                                {
                                    reader.Close(); // must close before delete
                                    file.Delete();
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // ignore
                }
            }
        }
        return Task.CompletedTask;
    }

    public override Task SetReviewedAsync(string id, bool isReviewed, CancellationToken cancellationToken = default)
    {
        try
        {
            var guidId = new Guid(id).ToString(); // validate GUID
            var file = new DirectoryInfo(LogPath).GetFiles($"error-*-{guidId}.xml").FirstOrDefault();
            if (file != null && IsUserFile(file.Attributes))
            {
                var doc = new XmlDocument();
                doc.Load(file.FullName);
                var root = doc.DocumentElement;
                if (root != null && root.Name == "error")
                {
                    if (isReviewed)
                    {
                        root.SetAttribute("isReviewed", "true");
                    }
                    else
                    {
                        root.RemoveAttribute("isReviewed");
                    }
                    doc.Save(file.FullName);
                }
            }
        }
        catch
        {
            // ignore
        }
        return Task.CompletedTask;
    }

    private bool MatchesFilter(string path, ErrorLogFilter filter)
    {
        try
        {
            using var reader = XmlReader.Create(path, new XmlReaderSettings { CheckCharacters = false });
            if (!reader.IsStartElement("error")) return false;

            var app = reader.GetAttribute("application");
            if (filter.Application != null && !string.Equals(app, filter.Application, StringComparison.OrdinalIgnoreCase))
                return false;

            var host = reader.GetAttribute("host");
            if (filter.Host != null && (host == null || !host.Contains(filter.Host, StringComparison.OrdinalIgnoreCase)))
                return false;

            var type = reader.GetAttribute("type");
            if (filter.Type != null && (type == null || !type.Contains(filter.Type, StringComparison.OrdinalIgnoreCase)))
                return false;

            var message = (reader.GetAttribute("message") ?? reader.GetAttribute("Message")) ?? string.Empty;
            var userAttr = reader.GetAttribute("user") ?? reader.GetAttribute("User");
            var resolvedUser = userAttr ?? Environment.GetEnvironmentVariable("USERDOMAIN") ?? Environment.GetEnvironmentVariable("USERNAME") ?? string.Empty;
            var xmlPath = (reader.GetAttribute("path") ?? reader.GetAttribute("Path")) ?? string.Empty;

            if (filter.Message != null)
            {
                var matchesMsg = message.Contains(filter.Message, StringComparison.OrdinalIgnoreCase);
                var matchesUser = resolvedUser.Contains(filter.Message, StringComparison.OrdinalIgnoreCase);
                var matchesPath = xmlPath.Contains(filter.Message, StringComparison.OrdinalIgnoreCase);
                if (!matchesMsg && !matchesUser && !matchesPath)
                    return false;
            }

            if (filter.User != null && !resolvedUser.Contains(filter.User, StringComparison.OrdinalIgnoreCase))
                return false;

            var statusCodeString = reader.GetAttribute("statusCode") ?? string.Empty;
            var statusCode = statusCodeString.Length == 0 ? 0 : XmlConvert.ToInt32(statusCodeString);
            if (filter.StatusCode.HasValue && statusCode != filter.StatusCode.Value)
                return false;

            var isReviewedString = reader.GetAttribute("isReviewed") ?? string.Empty;
            var isReviewed = isReviewedString.Length != 0 && XmlConvert.ToBoolean(isReviewedString);
            if (filter.IsReviewed.HasValue && isReviewed != filter.IsReviewed.Value)
                return false;

            var timeString = reader.GetAttribute("time") ?? string.Empty;
            if (timeString.Length > 0)
            {
                var time = XmlConvert.ToDateTime(timeString, XmlDateTimeSerializationMode.Local);
                if (filter.From.HasValue && time < filter.From.Value)
                    return false;
                if (filter.To.HasValue && time > filter.To.Value)
                    return false;
            }
            else
            {
                if (filter.From.HasValue || filter.To.HasValue)
                    return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public override Task<int> GetErrorsAsync(
        int errorIndex, 
        int pageSize, 
        ICollection<ErrorLogEntry> errorEntryList, 
        ErrorLogFilter filter, 
        CancellationToken cancellationToken = default)
    {
        if (filter == null)
        {
            return Task.FromResult(GetErrors(errorIndex, pageSize, errorEntryList));
        }

        if (!Directory.Exists(LogPath))
            return Task.FromResult(0);

        var dir = new DirectoryInfo(LogPath);
        var infos = dir.GetFiles("error-*.xml");
        if (!infos.Any())
            return Task.FromResult(0);

        var files = infos.Where(info => IsUserFile(info.Attributes))
            .OrderByDescending(info => info.Name, StringComparer.OrdinalIgnoreCase);

        var matchedFiles = new List<FileInfo>();
        foreach (var file in files)
        {
            if (MatchesFilter(file.FullName, filter))
            {
                matchedFiles.Add(file);
            }
        }

        if (errorEntryList != null)
        {
            var paged = matchedFiles.Skip(errorIndex).Take(pageSize);
            foreach (var file in paged)
            {
                try
                {
                    var entry = LoadErrorLogEntry(file.FullName);
                    if (entry != null)
                    {
                        errorEntryList.Add(entry);
                    }
                }
                catch
                {
                    // ignore
                }
            }
        }

        return Task.FromResult(matchedFiles.Count);
    }
}