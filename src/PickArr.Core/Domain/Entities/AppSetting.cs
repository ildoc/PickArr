namespace PickArr.Core.Domain.Entities;

public sealed class AppSetting
{
    public required string Key { get; set; }
    public required string ValueJson { get; set; }
    public bool IsSecret { get; set; }
    public DateTime UpdatedAt { get; set; }
}
