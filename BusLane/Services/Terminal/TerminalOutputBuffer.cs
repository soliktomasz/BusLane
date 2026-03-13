namespace BusLane.Services.Terminal;

using System.Text;

/// <summary>
/// Maintains terminal output text while minimizing full-buffer rebuilds.
/// </summary>
internal sealed class TerminalOutputBuffer
{
    private readonly Queue<string> _completedLines = new();
    private readonly StringBuilder _currentLine = new();
    private readonly StringBuilder _text = new();
    private readonly int _maxOutputLines;

    public TerminalOutputBuffer(int maxOutputLines)
    {
        _maxOutputLines = maxOutputLines;
    }

    public string Text => _text.ToString();

    public TerminalOutputUpdate Append(string chunk)
    {
        if (string.IsNullOrEmpty(chunk))
        {
            return new TerminalOutputUpdate(false, Text, string.Empty);
        }

        var appendedText = new StringBuilder();
        var requiresFullRefresh = false;

        foreach (var ch in chunk)
        {
            if (ch == '\r')
            {
                continue;
            }

            if (ch == '\n')
            {
                var newline = Environment.NewLine;
                _text.Append(newline);
                appendedText.Append(newline);
                _completedLines.Enqueue(_currentLine.ToString());
                _currentLine.Clear();

                if (_completedLines.Count > _maxOutputLines)
                {
                    _completedLines.Dequeue();
                    requiresFullRefresh = true;
                }
            }
            else
            {
                _currentLine.Append(ch);
                _text.Append(ch);
                appendedText.Append(ch);
            }
        }

        if (!requiresFullRefresh)
        {
            return new TerminalOutputUpdate(false, Text, appendedText.ToString());
        }

        RebuildText();
        return new TerminalOutputUpdate(true, Text, string.Empty);
    }

    public void Clear()
    {
        _completedLines.Clear();
        _currentLine.Clear();
        _text.Clear();
    }

    private void RebuildText()
    {
        _text.Clear();

        foreach (var line in _completedLines)
        {
            _text.Append(line);
            _text.Append(Environment.NewLine);
        }

        _text.Append(_currentLine);
    }
}

internal readonly record struct TerminalOutputUpdate(bool RequiresFullRefresh, string Text, string AppendedText);
