using Microsoft.Extensions.Logging.Abstractions;
using Pickarr.Infrastructure.Diagnostics;
using Shouldly;

namespace Pickarr.Infrastructure.Tests;

public sealed class LogFileServiceTests
{
    [Fact]
    public async Task List_Download_and_Clear_only_touch_pickarr_log_files()
    {
        var root = Path.Combine(Path.GetTempPath(), "Pickarr-log-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "logs"));
        try
        {
            var allowed = Path.Combine(root, "logs", "pickarr-20261111.log");
            var other = Path.Combine(root, "logs", "other.log");
            await File.WriteAllTextAsync(allowed, "hello");
            await File.WriteAllTextAsync(other, "nope");

            var service = new LogFileService(new PickarrDataPaths(root), NullLogger<LogFileService>.Instance);
            var list = await service.ListAsync();
            list.Items.Count.ShouldBe(1);
            list.Items[0].FileName.ShouldBe("pickarr-20261111.log");

            var opened = await service.OpenReadAsync("pickarr-20261111.log");
            opened.ShouldNotBeNull();
            await using (opened!.Value.Stream)
            {
                using var reader = new StreamReader(opened.Value.Stream);
                (await reader.ReadToEndAsync()).ShouldBe("hello");
            }

            (await service.OpenReadAsync("../secrets.txt")).ShouldBeNull();
            (await service.OpenReadAsync("other.log")).ShouldBeNull();

            var cleared = await service.ClearAllAsync();
            cleared.Deleted.ShouldBe(1);
            File.Exists(allowed).ShouldBeFalse();
            File.Exists(other).ShouldBeTrue();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
