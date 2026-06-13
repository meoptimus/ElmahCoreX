using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable MemberCanBePrivate.Global

namespace ElmahCore;

/// <summary>
///     An <see cref="ErrorLog" /> implementation that uses memory as its
///     backing store.
/// </summary>
/// <remarks>
///     All <see cref="MemoryErrorLog" /> instances will share the same memory
///     store that is bound to the application (not an instance of this class).
/// </remarks>
public sealed class MemoryErrorLog : ErrorLog
{
    //
    // The collection that provides the actual storage for this log
    // implementation and a lock to guarantee concurrency correctness.
    //

    private static EntryCollection _entries;
    private static readonly ReaderWriterLockSlim Lock = new ReaderWriterLockSlim();

    /// <summary>
    ///     The maximum number of errors that will ever be allowed to be stored
    ///     in memory.
    /// </summary>
    private static readonly int MaximumSize = 500;

    /// <summary>
    ///     The maximum number of errors that will be held in memory by default
    ///     if no size is specified.
    /// </summary>
    private static readonly int DefaultSize = 15;

    //
    // IMPORTANT! The size must be the same for all instances
    // for the entries collection to be initialized correctly.
    //

    private readonly int _size;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MemoryErrorLog" /> class
    ///     with a default size for maximum recordable entries.
    /// </summary>

    // ReSharper disable once UnusedMember.Global
    public MemoryErrorLog() : this(DefaultSize)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="MemoryErrorLog" /> class
    ///     with a specific size for maximum recordable entries.
    /// </summary>

    // ReSharper disable once MemberCanBePrivate.Global
    public MemoryErrorLog(int size)
    {
        if (size < 0 || size > MaximumSize)
            throw new ArgumentOutOfRangeException(nameof(size), size, $"Size must be between 0 and {MaximumSize}.");

        _size = size;
    }

    /// <summary>
    ///     Gets the name of this error log implementation.
    /// </summary>

    public override string Name => "In-Memory Error Log";

    /// <summary>
    ///     Logs an error to the application memory.
    /// </summary>
    /// <remarks>
    ///     If the log is full then the oldest error entry is removed.
    /// </remarks>
    public override string Log(Error error)
    {
        var newId = Guid.NewGuid();

        Log(newId, error);

        return newId.ToString();
    }

    public override void Log(Guid id, Error error)
    {
        if (error == null)
            throw new ArgumentNullException(nameof(error));

        //
        // Make a copy of the error to log since the source is mutable.
        // Assign a new GUID and create an entry for the error.
        //

        error = error.Clone();
        error.ApplicationName = ApplicationName;
        var entry = new ErrorLogEntry(this, id.ToString(), error);

        Lock.EnterWriteLock();

        try
        {
            var entries = _entries ??= new EntryCollection(_size);
            entries.Add(entry);
        }
        finally
        {
            Lock.ExitWriteLock();
        }
    }

    /// <summary>
    ///     Returns the specified error from application memory, or null
    ///     if it does not exist.
    /// </summary>
    public override ErrorLogEntry GetError(string id)
    {
        Lock.EnterReadLock();

        ErrorLogEntry entry;

        try
        {
            if (_entries == null || !_entries.Contains(id))
                return null;

            entry = _entries[id];
        }
        finally
        {
            Lock.ExitReadLock();
        }

        if (entry == null)
            return null;

        //
        // Return a copy that the caller can party on.
        //

        var error = entry.Error.Clone();
        return new ErrorLogEntry(this, entry.Id, error);
    }

    /// <summary>
    ///     Returns a page of errors from the application memory in
    ///     descending order of logged time.
    /// </summary>
    public override int GetErrors(int errorIndex, int pageSize, ICollection<ErrorLogEntry> errorEntryList)
    {
        if (errorIndex < 0) throw new ArgumentOutOfRangeException(nameof(errorIndex), errorIndex, null);
        if (pageSize < 0) throw new ArgumentOutOfRangeException(nameof(pageSize), pageSize, null);

        //
        // To minimize the time for which we hold the lock, we'll first
        // grab just references to the entries we need to return. Later,
        // we'll make copies and return those to the caller. Since Error 
        // is mutable, we don't want to return direct references to our 
        // internal versions since someone could change their state.
        //

        ErrorLogEntry[] selectedEntries = null;
        int totalCount;

        Lock.EnterReadLock();

        try
        {
            if (_entries == null)
                return 0;

            totalCount = _entries.Count;

            var startIndex = errorIndex;
            var endIndex = Math.Min(startIndex + pageSize, totalCount);
            var count = Math.Max(0, endIndex - startIndex);

            if (count > 0)
            {
                selectedEntries = new ErrorLogEntry[count];

                var sourceIndex = endIndex;
                var targetIndex = 0;

                while (sourceIndex > startIndex)
                    selectedEntries[targetIndex++] = _entries[--sourceIndex];
            }
        }
        finally
        {
            Lock.ExitReadLock();
        }

        if (errorEntryList != null && selectedEntries != null)
            //
            // Return copies of fetched entries. If the Error class would 
            // be immutable then this step wouldn't be necessary.
            //

            foreach (var entry in selectedEntries)
            {
                var error = entry.Error.Clone();
                errorEntryList.Add(new ErrorLogEntry(this, entry.Id, error));
            }

        return totalCount;
    }

    public override Task DeleteErrorsAsync(IEnumerable<string> errorIds, CancellationToken cancellationToken = default)
    {
        Lock.EnterWriteLock();
        try
        {
            if (_entries != null)
            {
                foreach (var id in errorIds)
                {
                    if (_entries.Contains(id))
                    {
                        _entries.Remove(id);
                    }
                }
            }
        }
        finally
        {
            Lock.ExitWriteLock();
        }
        return Task.CompletedTask;
    }

    public override Task DeleteAllErrorsAsync(string applicationName = null, CancellationToken cancellationToken = default)
    {
        Lock.EnterWriteLock();
        try
        {
            if (_entries != null)
            {
                if (string.IsNullOrEmpty(applicationName))
                {
                    _entries.Clear();
                }
                else
                {
                    var toRemove = new List<string>();
                    foreach (var entry in _entries)
                    {
                        if (entry.Error.ApplicationName == applicationName)
                        {
                            toRemove.Add(entry.Id);
                        }
                    }
                    foreach (var id in toRemove)
                    {
                        _entries.Remove(id);
                    }
                }
            }
        }
        finally
        {
            Lock.ExitWriteLock();
        }
        return Task.CompletedTask;
    }

    public override Task SetReviewedAsync(string id, bool isReviewed, CancellationToken cancellationToken = default)
    {
        Lock.EnterWriteLock();
        try
        {
            if (_entries != null && _entries.Contains(id))
            {
                _entries[id].Error.IsReviewed = isReviewed;
            }
        }
        finally
        {
            Lock.ExitWriteLock();
        }
        return Task.CompletedTask;
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
            return GetErrorsAsync(errorIndex, pageSize, errorEntryList, cancellationToken);
        }

        Lock.EnterReadLock();
        var matched = new List<ErrorLogEntry>();
        try
        {
            if (_entries == null)
                return Task.FromResult(0);

            var all = _entries.OrderByDescending(e => e.Error.Time).ToList();

            foreach (var entry in all)
            {
                var error = entry.Error;
                if (filter.Application != null && !string.Equals(error.ApplicationName, filter.Application, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (filter.Host != null && !error.HostName.Contains(filter.Host, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (filter.Type != null && !error.Type.Contains(filter.Type, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (filter.Message != null && !error.Message.Contains(filter.Message, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (filter.User != null && !error.User.Contains(filter.User, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (filter.StatusCode.HasValue && error.StatusCode != filter.StatusCode.Value)
                    continue;
                if (filter.IsReviewed.HasValue && error.IsReviewed != filter.IsReviewed.Value)
                    continue;
                if (filter.From.HasValue && error.Time < filter.From.Value)
                    continue;
                if (filter.To.HasValue && error.Time > filter.To.Value)
                    continue;

                matched.Add(entry);
            }
        }
        finally
        {
            Lock.ExitReadLock();
        }

        if (errorEntryList != null)
        {
            var paged = matched.Skip(errorIndex).Take(pageSize);
            foreach (var entry in paged)
            {
                var error = entry.Error.Clone();
                errorEntryList.Add(new ErrorLogEntry(this, entry.Id, error));
            }
        }

        return Task.FromResult(matched.Count);
    }

    private sealed class EntryCollection : KeyedCollection<string, ErrorLogEntry>
    {
        private readonly int _size;

        public EntryCollection(int size)
        {
            _size = size;
        }

        protected override string GetKeyForItem(ErrorLogEntry item)
        {
            return item.Id;
        }

        protected override void InsertItem(int index, ErrorLogEntry item)
        {
            if (Count == _size)
            {
                RemoveAt(0);
                index--;
            }

            base.InsertItem(index, item);
        }
    }
}