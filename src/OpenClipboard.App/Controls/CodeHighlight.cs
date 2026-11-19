using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace OpenClipboard.App.Controls;

public enum CodeLanguage
{
    None,
    Json,
    Xml
}

public static class CodeHighlight
{
    public static readonly AttachedProperty<string?> TextProperty =
        AvaloniaProperty.RegisterAttached<AvaloniaObject, string?>("Text", typeof(CodeHighlight));

    public static readonly AttachedProperty<CodeLanguage> LanguageProperty =
        AvaloniaProperty.RegisterAttached<AvaloniaObject, CodeLanguage>("Language", typeof(CodeHighlight));

    static CodeHighlight()
    {
        TextProperty.Changed.AddClassHandler<TextBlock>(OnChanged);
        LanguageProperty.Changed.AddClassHandler<TextBlock>(OnChanged);
    }

    public static void SetText(AvaloniaObject element, string? value) => element.SetValue(TextProperty, value);
    public static string? GetText(AvaloniaObject element) => element.GetValue(TextProperty);
    public static void SetLanguage(AvaloniaObject element, CodeLanguage value) => element.SetValue(LanguageProperty, value);
    public static CodeLanguage GetLanguage(AvaloniaObject element) => element.GetValue(LanguageProperty);

    private static void OnChanged(TextBlock textBlock, AvaloniaPropertyChangedEventArgs e)
    {
        Apply(textBlock);
    }

    private static void Apply(TextBlock textBlock)
    {
        var text = GetText(textBlock) ?? string.Empty;
        var language = GetLanguage(textBlock);

        textBlock.Inlines?.Clear();
        if (string.IsNullOrEmpty(text))
        {
            textBlock.Text = string.Empty;
            return;
        }

        // Reset plain Text in case it was set previously.
        textBlock.Text = null;

        IEnumerable<Inline> inlines = language switch
        {
            CodeLanguage.Json => BuildJsonInlines(text),
            CodeLanguage.Xml => BuildXmlInlines(text),
            _ => new Inline[] { new Run(text) }
        };

        foreach (var inline in inlines)
        {
            textBlock.Inlines!.Add(inline);
        }
    }

    private static readonly IBrush JsonKeyBrush = Brush.Parse("#0550AE");
    private static readonly IBrush JsonStringBrush = Brush.Parse("#0A7A3D");
    private static readonly IBrush JsonNumberBrush = Brush.Parse("#953800");
    private static readonly IBrush JsonBoolBrush = Brush.Parse("#CF222E");
    private static readonly IBrush JsonNullBrush = Brush.Parse("#6E7781");
    private static readonly IBrush JsonPuncBrush = Brush.Parse("#57606A");
    private static readonly IBrush DefaultBrush = Brushes.Black;

    private static IEnumerable<Inline> BuildJsonInlines(string text)
    {
        // Pretty-print if possible for better readability.
        var source = text;
        try
        {
            using var doc = JsonDocument.Parse(text);
            source = JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            // Keep original.
        }

        var list = new List<Inline>();
        // Regex adapted for JSON tokens: strings (with possible trailing : for keys), numbers, literals, punctuation
        var regex = new Regex("(\"(\\\\u[\\da-fA-F]{4}|\\\\[^u]|[^\\\\\"])*\"(\\s*:)?|\\btrue\\b|\\bfalse\\b|\\bnull\\b|-?\\d+(?:\\.\\d+)?(?:[eE][+\\-]?\\d+)?|[{}\\[\\],:])",
            RegexOptions.Compiled);
        int index = 0;
        foreach (Match m in regex.Matches(source))
        {
            if (m.Index > index)
            {
                list.Add(Run(source.Substring(index, m.Index - index), DefaultBrush));
            }

            var token = m.Value;
            IBrush brush;
            if (token.StartsWith('"'))
            {
                brush = token.TrimEnd().EndsWith(':') ? JsonKeyBrush : JsonStringBrush;
            }
            else if (token == "true" || token == "false")
            {
                brush = JsonBoolBrush;
            }
            else if (token == "null")
            {
                brush = JsonNullBrush;
            }
            else if (token.Length == 1 && "{}[],:".Contains(token))
            {
                brush = JsonPuncBrush;
            }
            else
            {
                brush = JsonNumberBrush;
            }

            var run = Run(token, brush);
            if (token == "null")
            {
                run.FontStyle = FontStyle.Italic;
            }
            list.Add(run);
            index = m.Index + m.Length;
        }

        if (index < source.Length)
        {
            list.Add(Run(source.Substring(index), DefaultBrush));
        }

        return list;
    }

    private static readonly IBrush XmlTagBrush = Brush.Parse("#0550AE");
    private static readonly IBrush XmlAttrNameBrush = Brush.Parse("#953800");
    private static readonly IBrush XmlAttrValueBrush = Brush.Parse("#0A7A3D");
    private static readonly IBrush XmlCommentBrush = Brush.Parse("#6E7781");
    private static readonly IBrush XmlPuncBrush = Brush.Parse("#57606A");

    private static IEnumerable<Inline> BuildXmlInlines(string text)
    {
        var source = text;
        try
        {
            var doc = XDocument.Parse(text, LoadOptions.PreserveWhitespace);
            // If it parses, use formatted output for readability.
            source = doc.Declaration != null
                ? doc.Declaration + Environment.NewLine + doc.ToString()
                : doc.ToString();
        }
        catch
        {
            // Keep original (may be fragment).
        }

        var list = new List<Inline>();
        // Tokenize: comments, processing instructions/cdata as blocks, tags, text
        var regex = new Regex("<!--.*?-->|<!\\[CDATA\\[.*?\\]\\]>|<\\?.*?\\?>|</?[^>]+/?>",
            RegexOptions.Compiled | RegexOptions.Singleline);
        int index = 0;
        foreach (Match m in regex.Matches(source))
        {
            if (m.Index > index)
            {
                list.Add(Run(source.Substring(index, m.Index - index), DefaultBrush));
            }

            var token = m.Value;
            if (token.StartsWith("<!--"))
            {
                var comment = Run(token, XmlCommentBrush);
                comment.FontStyle = FontStyle.Italic;
                list.Add(comment);
            }
            else if (token.StartsWith("<![CDATA[") || token.StartsWith("<?"))
            {
                list.Add(Run(token, XmlPuncBrush));
            }
            else if (token.StartsWith("<"))
            {
                AddTagInlines(list, token);
            }
            else
            {
                list.Add(Run(token, DefaultBrush));
            }

            index = m.Index + m.Length;
        }

        if (index < source.Length)
        {
            list.Add(Run(source.Substring(index), DefaultBrush));
        }

        return list;
    }

    private static void AddTagInlines(List<Inline> list, string tag)
    {
        // <  /?  name  ( attrs )  /?  >
        var m = Regex.Match(tag, @"^(</?)([^\s/>]+)(\s[^>]*?)?(\s*/?>)$", RegexOptions.Singleline);
        if (!m.Success)
        {
            list.Add(Run(tag, XmlTagBrush));
            return;
        }

        list.Add(Run(m.Groups[1].Value, XmlPuncBrush)); // < or </
        list.Add(Run(m.Groups[2].Value, XmlTagBrush));  // tag name

        var attrs = m.Groups[3].Value;
        if (!string.IsNullOrEmpty(attrs))
        {
            AddAttrInlines(list, attrs);
        }

        list.Add(Run(m.Groups[4].Value, XmlPuncBrush)); // / > or >
    }

    private static void AddAttrInlines(List<Inline> list, string attrs)
    {
        // Match name="value" or name='value'
        var regex = new Regex("(\\s+)([\\w:.-]+)(\\s*=\\s*)(\"[^\"]*\"|'[^']*')", RegexOptions.Compiled);
        int index = 0;
        foreach (Match m in regex.Matches(attrs))
        {
            if (m.Index > index)
            {
                list.Add(Run(attrs.Substring(index, m.Index - index), DefaultBrush));
            }
            list.Add(Run(m.Groups[1].Value, DefaultBrush));
            list.Add(Run(m.Groups[2].Value, XmlAttrNameBrush));
            list.Add(Run(m.Groups[3].Value, XmlPuncBrush));
            list.Add(Run(m.Groups[4].Value, XmlAttrValueBrush));
            index = m.Index + m.Length;
        }
        if (index < attrs.Length)
        {
            list.Add(Run(attrs.Substring(index), DefaultBrush));
        }
    }

    private static Run Run(string text, IBrush brush) => new(text) { Foreground = brush };
}
