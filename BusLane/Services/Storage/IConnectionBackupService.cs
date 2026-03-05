namespace BusLane.Services.Storage;

using BusLane.Models;

/// <summary>
/// Exports and imports saved connections using password-protected backup files.
/// </summary>
public interface IConnectionBackupService
{
    /// <summary>
    /// Exports provided connections to an encrypted backup file.
    /// </summary>
    /// <param name="connections">Connections to export.</param>
    /// <param name="filePath">Destination backup file path.</param>
    /// <param name="passphrase">Passphrase used to encrypt backup content.</param>
    Task ExportAsync(IEnumerable<SavedConnection> connections, string filePath, string passphrase);

    /// <summary>
    /// Imports and decrypts connections from a backup file.
    /// </summary>
    /// <param name="filePath">Backup file path.</param>
    /// <param name="passphrase">Passphrase used to decrypt backup content.</param>
    /// <returns>Decrypted list of saved connections.</returns>
    Task<IReadOnlyList<SavedConnection>> ImportAsync(string filePath, string passphrase);
}
