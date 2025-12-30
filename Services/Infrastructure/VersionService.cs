namespace BusLane.Services.Infrastructure;

using System.Reflection;

/// <summary>
/// Implementation of IVersionService that reads version information from the assembly metadata.
/// </summary>
public class VersionService : IVersionService
{
    private readonly Assembly _assembly;
    
    public VersionService()
    {
        _assembly = Assembly.GetExecutingAssembly();
    }
    
    public string Version
    {
        get
        {
            var version = _assembly.GetName().Version;
            return version != null 
                ? $"{version.Major}.{version.Minor}.{version.Build}" 
                : "0.0.0";
        }
    }
    
    public string InformationalVersion
    {
        get
        {
            var attribute = _assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            if (attribute?.InformationalVersion != null)
            {
                // Remove the source revision hash if present (added by SourceLink)
                var version = attribute.InformationalVersion;
                var plusIndex = version.IndexOf('+');
                return plusIndex > 0 ? version[..plusIndex] : version;
            }
            return Version;
        }
    }
    
    public string ProductName
    {
        get
        {
            var attribute = _assembly.GetCustomAttribute<AssemblyProductAttribute>();
            return attribute?.Product ?? "Bus Lane";
        }
    }
    
    public string Copyright
    {
        get
        {
            var attribute = _assembly.GetCustomAttribute<AssemblyCopyrightAttribute>();
            return attribute?.Copyright ?? string.Empty;
        }
    }
    
    public string DisplayVersion => $"v{InformationalVersion}";
}

