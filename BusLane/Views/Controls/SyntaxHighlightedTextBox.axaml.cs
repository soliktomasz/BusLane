using System.Reflection;
using System.Xml;
using Avalonia;
using Avalonia.Controls;
using AvaloniaEdit;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;

namespace BusLane.Views.Controls;

public partial class SyntaxHighlightedTextBox : UserControl
{
    private static IHighlightingDefinition? _jsonHighlighting;
    private static IHighlightingDefinition? _xmlHighlighting;
    private static bool _highlightingsLoaded;

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<SyntaxHighlightedTextBox, string?>(nameof(Text));

    public static readonly StyledProperty<bool> IsJsonProperty =
        AvaloniaProperty.Register<SyntaxHighlightedTextBox, bool>(nameof(IsJson));

    public static readonly StyledProperty<bool> IsXmlProperty =
        AvaloniaProperty.Register<SyntaxHighlightedTextBox, bool>(nameof(IsXml));

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public bool IsJson
    {
        get => GetValue(IsJsonProperty);
        set => SetValue(IsJsonProperty, value);
    }

    public bool IsXml
    {
        get => GetValue(IsXmlProperty);
        set => SetValue(IsXmlProperty, value);
    }

    private TextEditor? _editor;

    public SyntaxHighlightedTextBox()
    {
        InitializeComponent();
        _editor = this.FindControl<TextEditor>("Editor");
        EnsureHighlightingsLoaded();
    }

    private static void EnsureHighlightingsLoaded()
    {
        if (_highlightingsLoaded) return;
        _highlightingsLoaded = true;

        var assembly = Assembly.GetExecutingAssembly();

        _jsonHighlighting = LoadHighlighting(assembly, "BusLane.Assets.Highlighting.JSON.xshd");
        _xmlHighlighting = LoadHighlighting(assembly, "BusLane.Assets.Highlighting.XML.xshd");
    }

    private static IHighlightingDefinition? LoadHighlighting(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null) return null;

        using var reader = XmlReader.Create(stream);
        return HighlightingLoader.Load(reader, HighlightingManager.Instance);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (_editor == null) return;

        if (change.Property == TextProperty)
        {
            _editor.Text = Text ?? string.Empty;
            UpdateSyntaxHighlighting();
        }
        else if (change.Property == IsJsonProperty)
        {
            if (IsJson && IsXml)
            {
                SetValue(IsXmlProperty, false);
            }
            UpdateSyntaxHighlighting();
        }
        else if (change.Property == IsXmlProperty)
        {
            if (IsXml && IsJson)
            {
                SetValue(IsJsonProperty, false);
            }
            UpdateSyntaxHighlighting();
        }
    }

    private void UpdateSyntaxHighlighting()
    {
        if (_editor == null) return;

        if (IsJson)
        {
            _editor.SyntaxHighlighting = _jsonHighlighting;
        }
        else if (IsXml)
        {
            _editor.SyntaxHighlighting = _xmlHighlighting;
        }
        else
        {
            _editor.SyntaxHighlighting = null;
        }
    }
}
