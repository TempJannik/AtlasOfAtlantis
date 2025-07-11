using System.ComponentModel.DataAnnotations;

namespace DOAMapper.Models.Import;

/// <summary>
/// Extension methods for validating import models
/// </summary>
public static class ImportValidationExtensions
{
    /// <summary>
    /// Validates an import model and returns validation results
    /// </summary>
    /// <typeparam name="T">Type of model to validate</typeparam>
    /// <param name="model">Model instance to validate</param>
    /// <returns>Collection of validation results</returns>
    public static ICollection<ValidationResult> Validate<T>(this T model) where T : class
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(model);
        Validator.TryValidateObject(model, validationContext, validationResults, true);
        return validationResults;
    }

    /// <summary>
    /// Checks if an import model is valid
    /// </summary>
    /// <typeparam name="T">Type of model to validate</typeparam>
    /// <param name="model">Model instance to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    public static bool IsValid<T>(this T model) where T : class
    {
        return model.Validate().Count == 0;
    }

    /// <summary>
    /// Gets validation error messages for an import model
    /// </summary>
    /// <typeparam name="T">Type of model to validate</typeparam>
    /// <param name="model">Model instance to validate</param>
    /// <returns>List of error messages</returns>
    public static List<string> GetValidationErrors<T>(this T model) where T : class
    {
        return model.Validate().Select(vr => vr.ErrorMessage ?? "Unknown validation error").ToList();
    }

    /// <summary>
    /// Validates a collection of import models and returns all validation errors
    /// </summary>
    /// <typeparam name="T">Type of models to validate</typeparam>
    /// <param name="models">Collection of models to validate</param>
    /// <returns>Dictionary mapping model index to validation errors</returns>
    public static Dictionary<int, List<string>> ValidateCollection<T>(this IEnumerable<T> models) where T : class
    {
        var errors = new Dictionary<int, List<string>>();
        var index = 0;

        foreach (var model in models)
        {
            var modelErrors = model.GetValidationErrors();
            if (modelErrors.Any())
            {
                errors[index] = modelErrors;
            }
            index++;
        }

        return errors;
    }

    /// <summary>
    /// Validates import data and returns comprehensive validation results
    /// </summary>
    /// <param name="importData">Import data to validate</param>
    /// <returns>Validation result with detailed error information</returns>
    public static ImportValidationResult ValidateImportData(this ImportDataModel importData)
    {
        var result = new ImportValidationResult();

        // Validate root model
        var rootErrors = importData.GetValidationErrors();
        if (rootErrors.Any())
        {
            result.RootErrors = rootErrors;
        }

        // Validate tiles
        var tileErrors = importData.Tiles.ValidateCollection();
        if (tileErrors.Any())
        {
            result.TileErrors = tileErrors;
        }

        // Validate players
        var playerErrors = importData.Players.ValidateCollection();
        if (playerErrors.Any())
        {
            result.PlayerErrors = playerErrors;
        }

        // Validate alliances
        var allianceErrors = importData.Alliances.ValidateCollection();
        if (allianceErrors.Any())
        {
            result.AllianceErrors = allianceErrors;
        }

        // Validate alliance bases
        var allianceBaseErrors = importData.AllianceBases.ValidateCollection();
        if (allianceBaseErrors.Any())
        {
            result.AllianceBaseErrors = allianceBaseErrors;
        }

        return result;
    }
}

/// <summary>
/// Comprehensive validation result for import data
/// </summary>
public class ImportValidationResult
{
    public List<string> RootErrors { get; set; } = new();
    public Dictionary<int, List<string>> TileErrors { get; set; } = new();
    public Dictionary<int, List<string>> PlayerErrors { get; set; } = new();
    public Dictionary<int, List<string>> AllianceErrors { get; set; } = new();
    public Dictionary<int, List<string>> AllianceBaseErrors { get; set; } = new();

    public bool IsValid => !RootErrors.Any() && 
                          !TileErrors.Any() && 
                          !PlayerErrors.Any() && 
                          !AllianceErrors.Any() && 
                          !AllianceBaseErrors.Any();

    public int TotalErrorCount => RootErrors.Count + 
                                 TileErrors.Values.Sum(e => e.Count) +
                                 PlayerErrors.Values.Sum(e => e.Count) +
                                 AllianceErrors.Values.Sum(e => e.Count) +
                                 AllianceBaseErrors.Values.Sum(e => e.Count);

    public List<string> GetAllErrors()
    {
        var allErrors = new List<string>();
        
        allErrors.AddRange(RootErrors);
        
        foreach (var kvp in TileErrors)
        {
            allErrors.AddRange(kvp.Value.Select(e => $"Tile {kvp.Key}: {e}"));
        }
        
        foreach (var kvp in PlayerErrors)
        {
            allErrors.AddRange(kvp.Value.Select(e => $"Player {kvp.Key}: {e}"));
        }
        
        foreach (var kvp in AllianceErrors)
        {
            allErrors.AddRange(kvp.Value.Select(e => $"Alliance {kvp.Key}: {e}"));
        }
        
        foreach (var kvp in AllianceBaseErrors)
        {
            allErrors.AddRange(kvp.Value.Select(e => $"Alliance Base {kvp.Key}: {e}"));
        }
        
        return allErrors;
    }
}
