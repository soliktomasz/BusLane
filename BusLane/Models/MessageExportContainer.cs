namespace BusLane.Models;

/// <summary>
/// Container class for exporting/importing multiple saved messages.
/// </summary>
public class MessageExportContainer
{
    /// <summary>
    /// Version of the export format for future compatibility.
    /// </summary>
    public string Version { get; set; } = "1.0";
    
    /// <summary>
    /// Export timestamp.
    /// </summary>
    public DateTime ExportedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Optional description/name for the export.
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// The exported messages.
    /// </summary>
    public List<SavedMessage> Messages { get; set; } = new();
}