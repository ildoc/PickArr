using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PickArr.Integration.Tests;

public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dataPath = Path.Combine(Path.GetTempPath(), "PickArr-tests", Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(_dataPath);
        builder.UseEnvironment("Development");
        builder.UseSetting("PickArr:DataPath", _dataPath);
        builder.UseSetting("Hangfire:Storage", "Memory");
        builder.UseSetting("PickArr:Catalog:AutoInitialImport", "false");
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
