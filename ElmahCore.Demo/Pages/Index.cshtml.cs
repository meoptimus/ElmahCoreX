using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ElmahCore.Demo.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(ILogger<IndexModel> logger)
    {
        _logger = logger;
    }

    public void OnGet()
    {
        _logger.LogTrace("Test");
        _logger.LogDebug("Test");
        _logger.LogError("Test");
        _logger.LogInformation("Test");
        _logger.LogWarning("Test");
        _logger.LogCritical(new InvalidOperationException("Test"), "Test");

        try
        {
            throw new ArgumentNullException("connectionString", "The database connection string is empty or invalid. Please check your appsettings.json configuration file and ensure that the database service is running and accessible from the hosting environment.");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to initialize the database repository layer because the configuration provider returned invalid parameters for the current development environment. See inner exception for details.", ex);
        }
    }
}