using System;

public static class OfficeDisplayText
{
    public const string ItemColor = "#63D9EE";

    public static string FormatItemName(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
        {
            return rawName;
        }

        var normalized = rawName.Replace('_', ' ');
        var words = normalized.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < words.Length; index++)
        {
            words[index] = FormatWord(words[index]);
        }

        return string.Join("-", words);
    }

    public static string FormatItemNameRich(string rawName)
    {
        return $"<color={ItemColor}>{EscapeRichText(FormatItemName(rawName))}</color>";
    }

    public static string FormatActionLabel(string rawLabel, string rawItemName)
    {
        if (string.IsNullOrEmpty(rawLabel) || string.IsNullOrEmpty(rawItemName))
        {
            return EscapeRichText(rawLabel);
        }

        var index = rawLabel.IndexOf(rawItemName, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return EscapeRichText(rawLabel);
        }

        var before = rawLabel.Substring(0, index);
        var after = rawLabel.Substring(index + rawItemName.Length);
        return $"{EscapeRichText(before)}{FormatItemNameRich(rawItemName)}{EscapeRichText(after)}";
    }

    public static string FormatKnownItemNames(string message, OfficeWorldObjectDto[] objects)
    {
        if (string.IsNullOrEmpty(message))
        {
            return message;
        }

        var formatted = EscapeRichText(message);
        if (objects == null)
        {
            return formatted;
        }

        foreach (var worldObject in objects)
        {
            if (worldObject == null || string.IsNullOrEmpty(worldObject.name))
            {
                continue;
            }

            formatted = ReplaceAllIgnoreCase(formatted, worldObject.name, FormatItemNameRich(worldObject.name));
        }

        return formatted;
    }

    public static string EscapeRichText(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }

    private static string FormatWord(string word)
    {
        if (string.Equals(word, "backend", StringComparison.OrdinalIgnoreCase))
        {
            return "Backend";
        }

        if (string.Equals(word, "frontend", StringComparison.OrdinalIgnoreCase))
        {
            return "FrontEnd";
        }

        if (string.Equals(word, "qa", StringComparison.OrdinalIgnoreCase))
        {
            return "QA";
        }

        if (string.Equals(word, "pm", StringComparison.OrdinalIgnoreCase))
        {
            return "PM";
        }

        if (word.Length == 0)
        {
            return word;
        }

        return char.ToUpperInvariant(word[0]) + word.Substring(1);
    }

    private static string ReplaceAllIgnoreCase(string value, string search, string replacement)
    {
        if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(search))
        {
            return value;
        }

        var result = value;
        var searchStart = 0;
        while (searchStart < result.Length)
        {
            var index = result.IndexOf(search, searchStart, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                break;
            }

            result = result.Substring(0, index) + replacement + result.Substring(index + search.Length);
            searchStart = index + replacement.Length;
        }

        return result;
    }
}
