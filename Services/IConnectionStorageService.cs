using BusLane.Models;

namespace BusLane.Services;

public interface IConnectionStorageService
{
    Task<IEnumerable<SavedConnection>> GetConnectionsAsync();
    Task SaveConnectionAsync(SavedConnection connection);
    Task DeleteConnectionAsync(string connectionId);
    Task<SavedConnection?> GetConnectionAsync(string connectionId);
    Task ClearAllConnectionsAsync();
}

