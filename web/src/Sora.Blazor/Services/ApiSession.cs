using System.Text;
using System.Text.Json;

namespace Sora.Blazor.Services;

public sealed class ApiSession
{
    public event Action? Changed;
    public string? AccessToken { get; private set; }
    public string? UserName { get; private set; }
    public bool IsSignedIn => !string.IsNullOrWhiteSpace(AccessToken);
    public string PreferenceUserKey => ReadClaimValues("sub").FirstOrDefault() ?? "unknown-user";
    public string WorkspaceProfile
    {
        get
        {
            if (HasAnyRole("ADMIN")) return "admin";
            if (HasAnyRole("DISPATCH_LEAD")) return "dispatch-lead";
            if (HasAnyRole("WAREHOUSE")) return "warehouse";
            if (HasAnyRole("SELLER")) return "seller";
            if (HasAnyRole("DISPATCH_ASSISTANT")) return "dispatch-assistant";
            if (HasAnyRole("DRIVER")) return "driver";
            return "staff";
        }
    }

    public string WorkspaceLabel => WorkspaceProfile switch
    {
        "admin" => "Administración",
        "seller" => "Ventas",
        "warehouse" => "Almacén",
        "dispatch-lead" => "Jefatura de despacho",
        "dispatch-assistant" => "Ayudante de despacho",
        "driver" => "Reparto",
        _ => "Personal"
    };
    public void SignIn(string token, string userName) { AccessToken = token; UserName = userName; Changed?.Invoke(); }
    public void SignOut() { AccessToken = null; UserName = null; Changed?.Invoke(); }

    public bool CanAccessPage(string page) => page switch
    {
        "staff" => HasPermission("staff.manage"),
        "customers" => HasPermission("customers.read"),
        "products" => HasPermission("products.read"),
        "suppliers" => HasPermission("suppliers.read"),
        "inventory" => HasPermission("inventory.read"),
        "orders" => HasPermission("orders.read"),
        "dispatches" => HasPermission("dispatches.read") || HasPermission("orders.prepare"),
        _ => HasPermission("staff.manage")
    };

    public bool HasPermission(string permission) => ReadClaimValues("permission")
        .Contains(permission, StringComparer.OrdinalIgnoreCase);


    public bool HasAnyRole(params string[] allowedRoles)
    {
        if (string.IsNullOrWhiteSpace(AccessToken)) return false;
        try
        {
            var segments = AccessToken.Split('.');
            if (segments.Length < 2) return false;
            var payload = segments[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            using var document = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Name is not ("role" or "roles" or "http://schemas.microsoft.com/ws/2008/06/identity/claims/role")) continue;
                IEnumerable<string?> values = property.Value.ValueKind == JsonValueKind.Array
                    ? property.Value.EnumerateArray().Select(x => x.GetString())
                    : new[] { property.Value.GetString() };
                if (values.Any(value => value is not null && allowedRoles.Contains(value, StringComparer.OrdinalIgnoreCase)))
                    return true;
            }
        }
        catch (FormatException) { }
        catch (JsonException) { }
        return false;
    }

    private IEnumerable<string> ReadClaimValues(string claimName)
    {
        if (string.IsNullOrWhiteSpace(AccessToken)) return [];
        try
        {
            var segments = AccessToken.Split('.');
            if (segments.Length < 2) return [];
            var payload = segments[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            using var document = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
            if (!document.RootElement.TryGetProperty(claimName, out var value)) return [];
            return value.ValueKind == JsonValueKind.Array
                ? value.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String)
                    .Select(x => x.GetString()!).ToArray()
                : value.ValueKind == JsonValueKind.String ? [value.GetString()!] : [];
        }
        catch (FormatException) { return []; }
        catch (JsonException) { return []; }
    }
}
