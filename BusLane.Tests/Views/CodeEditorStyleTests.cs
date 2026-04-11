namespace BusLane.Tests.Views;

using System.Linq;
using System.Xml.Linq;
using FluentAssertions;

public class CodeEditorStyleTests
{
    [Fact]
    public void AppStyles_CodeEditorPointerOverTemplateStyle_KeepsDarkBackground()
    {
        // Arrange
        var xaml = File.ReadAllText(GetAppStylesPath());

        // Act
        var styleBlock = GetStyleBlock(xaml, "TextBox.code-editor:pointerover /template/ Border#PART_BorderElement");

        // Assert
        styleBlock.Should().Contain("<Setter Property=\"Background\" Value=\"{DynamicResource CodeBackground}\"/>");
    }

    [Fact]
    public void AppStyles_CodeEditorFocusTemplateStyle_KeepsDarkBackground()
    {
        // Arrange
        var xaml = File.ReadAllText(GetAppStylesPath());

        // Act
        var styleBlock = GetStyleBlock(xaml, "TextBox.code-editor:focus /template/ Border#PART_BorderElement");

        // Assert
        styleBlock.Should().Contain("<Setter Property=\"Background\" Value=\"{DynamicResource CodeBackground}\"/>");
    }

    [Fact]
    public void AppStyles_CodeEditorPointerOverStyle_ReassertsCodeEditorBrushes()
    {
        // Arrange
        var xaml = File.ReadAllText(GetAppStylesPath());

        // Act
        var styleBlock = GetStyleBlock(xaml, "TextBox.code-editor:pointerover");

        // Assert
        styleBlock.Should().Contain("<Setter Property=\"Background\" Value=\"{DynamicResource CodeBackground}\"/>");
        styleBlock.Should().Contain("<Setter Property=\"Foreground\" Value=\"{DynamicResource CodeForeground}\"/>");
        styleBlock.Should().Contain("<Setter Property=\"CaretBrush\" Value=\"{DynamicResource CodeForeground}\"/>");
    }

    [Fact]
    public void AppStyles_CodeEditorTemplateOverrides_DoNotTargetAllBorders()
    {
        // Arrange
        var xaml = File.ReadAllText(GetAppStylesPath());

        // Assert
        xaml.Should().NotContain("<Style Selector=\"TextBox.code-editor:pointerover /template/ Border\">");
    }

    [Fact]
    public void SendMessageDialog_CodeEditorRemainsInsideSharedDialogBody()
    {
        // Arrange
        var xaml = File.ReadAllText(GetSendMessageDialogPath());
        var document = XDocument.Parse(xaml);
        var codeEditor = document
            .Descendants()
            .FirstOrDefault(element => HasClass(element, "code-editor"));

        // Assert
        codeEditor.Should().NotBeNull("SendMessageDialog should include a code editor");
        codeEditor!
            .Ancestors()
            .Any(element => HasClass(element, "dialog-body"))
            .Should()
            .BeTrue("the code editor should be nested inside the shared dialog body");
    }

    private static string GetAppStylesPath()
    {
        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BusLane",
            "Styles",
            "AppStyles.axaml"));
    }

    private static string GetSendMessageDialogPath()
    {
        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BusLane",
            "Views",
            "Dialogs",
            "SendMessageDialog.axaml"));
    }

    private static string GetStyleBlock(string xaml, string selector)
    {
        var styleTag = $"<Style Selector=\"{selector}\">";
        var startIndex = xaml.IndexOf(styleTag, StringComparison.Ordinal);
        startIndex.Should().BeGreaterThanOrEqualTo(0, $"style '{selector}' should exist in AppStyles.axaml");

        var endIndex = xaml.IndexOf("</Style>", startIndex, StringComparison.Ordinal);
        endIndex.Should().BeGreaterThan(startIndex);

        return xaml[startIndex..(endIndex + "</Style>".Length)];
    }

    private static bool HasClass(XElement element, string className)
    {
        var classes = element.Attribute("Classes")?.Value;
        return classes?
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Contains(className, StringComparer.Ordinal) == true;
    }
}
