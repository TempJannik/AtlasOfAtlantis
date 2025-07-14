namespace DOAMapper.Shared.Constants;

public static class RealmConstants
{
    public const string DefaultRealmId = "default";
    public const string DefaultRealmName = "Default Realm";
    
    public const int MaxRealmIdLength = 50;
    public const int MaxRealmNameLength = 100;
    
    // Validation patterns
    public const string RealmIdPattern = @"^[a-zA-Z0-9_-]+$";
    
    // Reserved realm IDs that cannot be used
    public static readonly HashSet<string> ReservedRealmIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "admin",
        "system",
        "api",
        "test",
        "temp",
        "null",
        "undefined"
    };
}
