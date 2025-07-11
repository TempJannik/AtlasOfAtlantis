namespace DOAMapper.Shared.Models.Enums;

/// <summary>
/// Categories of errors that can occur during import processing
/// </summary>
public enum ImportErrorCategory
{
    /// <summary>
    /// Unknown or uncategorized error
    /// </summary>
    Unknown,

    /// <summary>
    /// JSON format or parsing errors
    /// </summary>
    DataFormat,

    /// <summary>
    /// Data validation errors (invalid values, missing required fields, etc.)
    /// </summary>
    DataValidation,

    /// <summary>
    /// Duplicate data detection errors
    /// </summary>
    DataDuplication,

    /// <summary>
    /// Database transaction errors (rollback, commit failures, etc.)
    /// </summary>
    DatabaseTransaction,

    /// <summary>
    /// Database update/insert errors
    /// </summary>
    DatabaseUpdate,

    /// <summary>
    /// Database connection errors
    /// </summary>
    DatabaseConnection,

    /// <summary>
    /// Operation timeout errors
    /// </summary>
    Timeout,

    /// <summary>
    /// Memory-related errors (out of memory, etc.)
    /// </summary>
    Memory,

    /// <summary>
    /// Security/authorization errors
    /// </summary>
    Security,

    /// <summary>
    /// File access errors (file not found, permission denied, etc.)
    /// </summary>
    FileAccess
}
