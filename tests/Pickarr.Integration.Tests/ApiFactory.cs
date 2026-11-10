using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Pickarr.Integration.Tests;

public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dataPath = Path.Combine(Path.GetTempPath(), "Pickarr-tests", Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(_dataPath);
        builder.UseEnvironment("Development");
        builder.UseSetting("Pickarr:DataPath", _dataPath);
        builder.UseSetting("Hangfire:Storage", "Memory");
        builder.UseSetting("Pickarr:Catalog:AutoInitialImport", "false");
        ConfigureApplication(builder);
    }

    protected virtual void ConfigureApplication(IWebHostBuilder builder)
    {
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(_dataPath))
        {
            try
            {
                Directory.Delete(_dataPath, recursive: true);
            }
            catch
            {
                // best-effort cleanup
            }
        }
    }
}
