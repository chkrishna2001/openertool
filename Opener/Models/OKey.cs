using System;

namespace Opener.Models;

public class OKey
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Key { get; set; } = string.Empty;
    public OKeyType KeyType { get; set; }
    public string Value { get; set; } = string.Empty; // Renamed from Path for clarity across types
    public string Description { get; set; } = string.Empty;
}
