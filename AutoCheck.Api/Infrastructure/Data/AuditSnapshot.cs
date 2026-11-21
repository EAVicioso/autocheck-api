using Microsoft.EntityFrameworkCore.ChangeTracking;
namespace AutoCheck.Api.Infrastructure.Data;

public static class AuditSnapshot
{
    public static string Create(EntityEntry entry, bool original)
    {
        var data = new Dictionary<string, object?>();
        foreach (var property in entry.Properties.Where(x => x.Metadata.Name is "Role" or "Permissions" or "CenterId" or "Active" or "Version" or "ArchivedAt"))
        {
            data[property.Metadata.Name] = original ? property.OriginalValue : property.CurrentValue;
        }
        foreach (var property in entry.Properties.Where(x => x.Metadata.Name is "Name" or "Phone" or "Plate" or "Vin"))
        {
            data[property.Metadata.Name] = "[REDACTADO]";
        }
        return JsonSerializer.Serialize(data);
    }
}
