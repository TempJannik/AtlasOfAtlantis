using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace DOAMapper.Models.Import;

/// <summary>
/// Root model representing the complete JSON import data structure
/// </summary>
public class ImportDataModel
{
    [JsonPropertyName("tiles")]
    public List<TileImportModel> Tiles { get; set; } = new();

    [JsonPropertyName("players")]
    public List<PlayerImportModel> Players { get; set; } = new();

    [JsonPropertyName("alliances")]
    public List<AllianceImportModel> Alliances { get; set; } = new();

    [JsonPropertyName("allianceBases")]
    public List<AllianceBaseImportModel> AllianceBases { get; set; } = new();
}

/// <summary>
/// Model for tile data from JSON import
/// </summary>
public class TileImportModel
{
    [JsonPropertyName("x")]
    [Range(0, 749, ErrorMessage = "X coordinate must be between 0 and 749")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    [Range(0, 749, ErrorMessage = "Y coordinate must be between 0 and 749")]
    public int Y { get; set; }

    [JsonPropertyName("type")]
    [Required(ErrorMessage = "Tile type is required")]
    [StringLength(50, ErrorMessage = "Tile type cannot exceed 50 characters")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("level")]
    [Range(0, int.MaxValue, ErrorMessage = "Level must be non-negative")]
    public int Level { get; set; }

    [JsonPropertyName("playerId")]
    public int PlayerId { get; set; }

    [JsonPropertyName("allianceId")]
    public int AllianceId { get; set; }
}

/// <summary>
/// Model for player data from JSON import
/// </summary>
public class PlayerImportModel
{
    [JsonPropertyName("playerId")]
    [Required(ErrorMessage = "Player ID is required")]
    [StringLength(50, ErrorMessage = "Player ID cannot exceed 50 characters")]
    public string PlayerId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    [StringLength(100, ErrorMessage = "Player name cannot exceed 100 characters")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("city")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    [StringLength(100, ErrorMessage = "City name cannot exceed 100 characters")]
    public string City { get; set; } = string.Empty;

    [JsonPropertyName("might")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string Might { get; set; } = string.Empty;
}

/// <summary>
/// Model for alliance data from JSON import (alliances without bases)
/// </summary>
public class AllianceImportModel
{
    [JsonPropertyName("allianceId")]
    [Required(ErrorMessage = "Alliance ID is required")]
    [StringLength(50, ErrorMessage = "Alliance ID cannot exceed 50 characters")]
    public string AllianceId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    [StringLength(100, ErrorMessage = "Alliance name cannot exceed 100 characters")]
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Model for alliance base data from JSON import (alliances with fortress information)
/// </summary>
public class AllianceBaseImportModel
{
    [JsonPropertyName("alliance_id")]
    [Range(1, int.MaxValue, ErrorMessage = "Alliance ID must be positive")]
    public int AllianceId { get; set; }

    [JsonPropertyName("fortress_level")]
    [Range(0, int.MaxValue, ErrorMessage = "Fortress level must be non-negative")]
    public int FortressLevel { get; set; }

    [JsonPropertyName("x")]
    [Range(0, 749, ErrorMessage = "X coordinate must be between 0 and 749")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    [Range(0, 749, ErrorMessage = "Y coordinate must be between 0 and 749")]
    public int Y { get; set; }

    [JsonPropertyName("name")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    [StringLength(100, ErrorMessage = "Alliance name cannot exceed 100 characters")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("overlord")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    [StringLength(100, ErrorMessage = "Overlord name cannot exceed 100 characters")]
    public string Overlord { get; set; } = string.Empty;

    [JsonPropertyName("power")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string Power { get; set; } = string.Empty;
}
