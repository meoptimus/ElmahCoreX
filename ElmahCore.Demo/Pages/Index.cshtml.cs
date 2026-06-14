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
            try
            {
                try
                {
                    throw new System.IO.FileNotFoundException("Could not load file or assembly 'Newtonsoft.Json, Version=13.0.0.0, Culture=neutral, PublicKeyToken=30ad4fe6b2a6aeed' or one of its dependencies. The system cannot find the file specified. Ensure that the assembly is deployed correctly and present in the bin folder or Global Assembly Cache.", "Newtonsoft.Json.dll");
                }
                catch (Exception ex1)
                {
                    throw new TypeInitializationException("Newtonsoft.Json.JsonConvert", ex1);
                }
            }
            catch (Exception ex2)
            {
                throw new InvalidOperationException("An error occurred while parsing the configuration profile. The JSON serialization settings could not be loaded due to a missing assembly version reference.", ex2);
            }
        }
        catch (Exception ex3)
        {
            throw new AggregateException("One or more critical errors occurred during the application initialization pipeline. Please inspect the nested inner exceptions to troubleshoot the boot failure.", ex3);
        }
    }
}