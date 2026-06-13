using System;

namespace ElmahCore;

public class ErrorLogFilter
{
    public string Type { get; set; }
    public string Message { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string Host { get; set; }
    public string User { get; set; }
    public int? StatusCode { get; set; }
    public string Application { get; set; }
    public bool? IsReviewed { get; set; }
}
