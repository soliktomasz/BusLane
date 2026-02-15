using BusLane.Models.Dashboard;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace BusLane.ViewModels.Dashboard;

public partial class TopEntitiesListViewModel : ObservableObject
{
    public string Title { get; }

    [ObservableProperty]
    private ObservableCollection<TopEntityInfo> _entities = new();

    public TopEntitiesListViewModel(string title)
    {
        Title = title;
    }

    public void UpdateEntities(IEnumerable<TopEntityInfo> entities)
    {
        Entities.Clear();
        foreach (var entity in entities)
        {
            Entities.Add(entity);
        }
    }
}
