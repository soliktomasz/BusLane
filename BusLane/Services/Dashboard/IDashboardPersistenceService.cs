namespace BusLane.Services.Dashboard;

using Models;

public interface IDashboardPersistenceService
{
    DashboardConfiguration Load();
    void Save(DashboardConfiguration config);
    DashboardConfiguration GetDefaultConfiguration();
}
