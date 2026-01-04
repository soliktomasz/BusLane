using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BusLane.ViewModels.Core;

/// <summary>
/// Provides a clean, declarative mechanism for forwarding properties from sub-ViewModels
/// to a parent ViewModel while maintaining XAML binding compatibility.
/// </summary>
/// <remarks>
/// This utility enables composition-based ViewModel design while allowing XAML to bind
/// to properties on the parent ViewModel instead of requiring nested paths like "Navigation.SelectedQueue".
/// </remarks>
public sealed class PropertyForwarder
{
    private readonly Action<string> _raisePropertyChanged;

    /// <summary>
    /// Creates a new PropertyForwarder for the specified target ViewModel.
    /// </summary>
    /// <param name="raisePropertyChanged">Action to raise PropertyChanged on the target.</param>
    public PropertyForwarder(Action<string> raisePropertyChanged)
    {
        _raisePropertyChanged = raisePropertyChanged;
    }

    /// <summary>
    /// Registers a sub-ViewModel as a source for property forwarding.
    /// </summary>
    /// <typeparam name="TSource">The type of the source ViewModel.</typeparam>
    /// <param name="source">The source sub-ViewModel instance.</param>
    /// <param name="forwardedProperties">Names of properties to forward from source to target.</param>
    /// <param name="onPropertyChanged">Optional callback for additional actions when any property changes.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public PropertyForwarder Forward<TSource>(
        TSource source, 
        string[] forwardedProperties,
        Action<string?>? onPropertyChanged = null) where TSource : INotifyPropertyChanged
    {
        var propertySet = new HashSet<string>(forwardedProperties);
        
        source.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != null && propertySet.Contains(e.PropertyName))
            {
                _raisePropertyChanged(e.PropertyName);
            }
            onPropertyChanged?.Invoke(e.PropertyName);
        };
        
        return this;
    }

    /// <summary>
    /// Registers a sub-ViewModel with specific property change handlers.
    /// </summary>
    /// <typeparam name="TSource">The type of the source ViewModel.</typeparam>
    /// <param name="source">The source sub-ViewModel instance.</param>
    /// <param name="forwardedProperties">Names of properties to forward from source to target.</param>
    /// <param name="propertyHandlers">Dictionary mapping property names to specific handlers.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public PropertyForwarder ForwardWithHandlers<TSource>(
        TSource source,
        string[] forwardedProperties,
        Dictionary<string, Action>? propertyHandlers = null) where TSource : INotifyPropertyChanged
    {
        var propertySet = new HashSet<string>(forwardedProperties);
        
        source.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != null)
            {
                if (propertySet.Contains(e.PropertyName))
                {
                    _raisePropertyChanged(e.PropertyName);
                }
                
                if (propertyHandlers?.TryGetValue(e.PropertyName, out var handler) == true)
                {
                    handler();
                }
            }
        };
        
        return this;
    }
}

/// <summary>
/// Extension methods for fluent property forwarding setup.
/// </summary>
public static class PropertyForwarderExtensions
{
    /// <summary>
    /// Creates a PropertyForwarder for this ViewModel.
    /// </summary>
    public static PropertyForwarder CreateForwarder(this ObservableObject target, Action<string> raisePropertyChanged)
        => new(raisePropertyChanged);
}

