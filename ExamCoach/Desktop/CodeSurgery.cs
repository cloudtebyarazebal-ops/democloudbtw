using System.Text.RegularExpressions;

namespace ExamCoachDesktop;

/// <summary>Структурные правки C#/Razor/HTML в коде шагов.</summary>
public static class CodeSurgery
{
    public static string RemovePublicMethod(string code, string methodName)
    {
        if (string.IsNullOrEmpty(code)) return code;

        var pattern = $@"public\s+(?:async\s+)?[\w<>,\?\[\]\s]+\s+{Regex.Escape(methodName)}\s*\(";
        var match = Regex.Match(code, pattern);
        if (!match.Success)
            return code;

        var idx = match.Index;
        var docStart = FindDocStart(code, idx);
        var parenStart = code.IndexOf('(', match.Index);
        if (parenStart < 0) return code;

        var parenEnd = FindMatchingParen(code, parenStart);
        if (parenEnd < 0) return code;

        var afterParams = parenEnd + 1;
        var arrowIdx = code.IndexOf("=>", afterParams, StringComparison.Ordinal);
        var braceIdx = FindMethodBodyBrace(code, afterParams);

        int removeEnd;
        if (arrowIdx >= 0 && (braceIdx < 0 || arrowIdx < braceIdx))
        {
            removeEnd = FindExpressionBodyEnd(code, arrowIdx + 2);
            while (removeEnd < code.Length && (code[removeEnd] == '\r' || code[removeEnd] == '\n'))
                removeEnd++;
        }
        else
        {
            if (braceIdx < 0) return code;
            var end = FindMatchingBrace(code, braceIdx);
            if (end < 0) return code;
            removeEnd = end + 1;
            while (removeEnd < code.Length && (code[removeEnd] == '\r' || code[removeEnd] == '\n'))
                removeEnd++;
        }

        return code.Remove(docStart, removeEnd - docStart);
    }

    public static string RemovePublicType(string code, string typeName)
    {
        if (string.IsNullOrEmpty(code)) return code;

        foreach (var prefix in new[] { "public sealed class ", "public static class ", "public record ", "public class " })
        {
            var marker = prefix + typeName;
            var idx = code.IndexOf(marker, StringComparison.Ordinal);
            if (idx < 0) continue;
            return RemoveTypeAt(code, idx);
        }

        return code;
    }

    public static string RemoveMarkedSection(string code, string startMarker, string? endMarker = null)
    {
        if (string.IsNullOrEmpty(code)) return code;

        var idx = code.IndexOf(startMarker, StringComparison.Ordinal);
        if (idx < 0) return code;

        var sectionStart = code.LastIndexOf('\n', Math.Max(0, idx - 1));
        sectionStart = sectionStart < 0 ? 0 : sectionStart + 1;

        var searchFrom = idx + startMarker.Length;
        var sectionEnd = code.Length;

        if (!string.IsNullOrEmpty(endMarker))
        {
            var endIdx = code.IndexOf(endMarker, searchFrom, StringComparison.Ordinal);
            if (endIdx >= 0) sectionEnd = endIdx;
        }
        else
        {
            foreach (var tail in new[] { "\n        CopySeedImages", "\n    private static", "\n    public static", "\n///" })
            {
                var endIdx = code.IndexOf(tail, searchFrom, StringComparison.Ordinal);
                if (endIdx >= 0 && endIdx < sectionEnd) sectionEnd = endIdx;
            }
        }

        return code.Remove(sectionStart, sectionEnd - sectionStart);
    }

    public static string RemoveLineContaining(string code, string fragment)
    {
        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(fragment)) return code;

        var lines = code.Split('\n').ToList();
        lines.RemoveAll(l => l.Contains(fragment, StringComparison.Ordinal));
        return string.Join('\n', lines);
    }

    public static string RemoveLinesMatching(string code, string pattern)
    {
        if (string.IsNullOrEmpty(code)) return code;
        var regex = new Regex(pattern, RegexOptions.Multiline);
        return regex.Replace(code, "");
    }

    public static string RemoveRazorBlockContaining(string code, string fragment)
    {
        if (string.IsNullOrEmpty(code) || !code.Contains(fragment, StringComparison.Ordinal))
            return code;

        var idx = code.IndexOf(fragment, StringComparison.Ordinal);
        var ifIdx = code.LastIndexOf("@if", idx, StringComparison.Ordinal);
        if (ifIdx < 0) return RemoveLineContaining(code, fragment);

        var braceStart = code.IndexOf('{', ifIdx);
        if (braceStart < 0) return RemoveLineContaining(code, fragment);

        var end = FindMatchingBrace(code, braceStart);
        if (end < 0) return RemoveLineContaining(code, fragment);

        var blockStart = code.LastIndexOf('\n', Math.Max(0, ifIdx - 1));
        blockStart = blockStart < 0 ? 0 : blockStart + 1;
        var tail = end + 1;
        while (tail < code.Length && (code[tail] == '\r' || code[tail] == '\n'))
            tail++;

        return code.Remove(blockStart, tail - blockStart);
    }

    public static string RemovePublicEnum(string code, string enumName)
    {
        if (string.IsNullOrEmpty(code)) return code;

        var marker = "public enum " + enumName;
        var idx = code.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0) return code;

        var docStart = FindDocStart(code, idx);
        var braceStart = code.IndexOf('{', idx);
        if (braceStart < 0) return code;

        var end = FindMatchingBrace(code, braceStart);
        if (end < 0) return code;

        var tail = end + 1;
        while (tail < code.Length && (code[tail] == '\r' || code[tail] == '\n'))
            tail++;

        return code.Remove(docStart, tail - docStart);
    }

    public static string RemovePublicProperty(string code, string propertyName)
    {
        if (string.IsNullOrEmpty(code)) return code;

        var propertyPattern =
            $@"(?:^\s*\[[^\]]+\]\s*\r?\n)*?(?:^\s*///[^\r\n]*\r?\n)*?\s*public\s+[\w<>,\?\[\].\s]+\s+{Regex.Escape(propertyName)}\s*\{{\s*get(?:;\s*set|\s*;\s*init)?;\s*\}}\s*(?:=\s*[^;]+;\s*)?\r?\n?";
        var regex = new Regex(propertyPattern, RegexOptions.Multiline);
        var next = regex.Replace(code, "");
        return next == code ? RemoveSimplePropertyLine(code, propertyName) : next;
    }

    private static string RemoveSimplePropertyLine(string code, string propertyName)
    {
        foreach (var pattern in new[] { $"{propertyName} {{ get; set; }}", $"{propertyName} {{ get; init; }}" })
        {
            var idx = code.IndexOf(pattern, StringComparison.Ordinal);
            if (idx < 0) continue;

            var lineStart = code.LastIndexOf('\n', Math.Max(0, idx - 1));
            lineStart = lineStart < 0 ? 0 : lineStart + 1;
            var docStart = FindDocStart(code, lineStart);
            var attrStart = FindAttributeBlockStart(code, docStart);

            var lineEnd = code.IndexOf('\n', idx);
            if (lineEnd < 0) lineEnd = code.Length;
            else lineEnd++;

            var removeEnd = lineEnd;
            var probe = removeEnd;
            while (probe < code.Length && (code[probe] == '\r' || code[probe] == '\n'))
                probe++;

            if (probe < code.Length)
            {
                var initLineEnd = code.IndexOf('\n', probe);
                if (initLineEnd < 0) initLineEnd = code.Length;
                var initLine = code[probe..initLineEnd].TrimStart();
                if (initLine.StartsWith("=", StringComparison.Ordinal))
                {
                    removeEnd = initLineEnd < code.Length ? initLineEnd + 1 : initLineEnd;
                    while (!initLine.Contains(';') && removeEnd < code.Length)
                    {
                        var nextEnd = code.IndexOf('\n', removeEnd);
                        if (nextEnd < 0)
                        {
                            removeEnd = code.Length;
                            break;
                        }

                        var nextLine = code[removeEnd..nextEnd];
                        initLine = nextLine.TrimStart();
                        removeEnd = nextEnd + 1;
                        if (initLine.Contains(';'))
                            break;
                    }
                }
            }

            return code.Remove(attrStart, removeEnd - attrStart);
        }

        return code;
    }

    public static string RemoveFormGroupContaining(string code, string fragment)
    {
        if (string.IsNullOrEmpty(code) || !code.Contains(fragment, StringComparison.Ordinal))
            return code;

        var idx = code.IndexOf(fragment, StringComparison.Ordinal);
        var start = code.LastIndexOf("form-group", idx, StringComparison.OrdinalIgnoreCase);
        if (start < 0) return RemoveLineContaining(code, fragment);

        start = code.LastIndexOf('\n', Math.Max(0, start - 1));
        start = start < 0 ? 0 : start + 1;

        var end = code.IndexOf("</div>", idx, StringComparison.OrdinalIgnoreCase);
        if (end < 0) return RemoveLineContaining(code, fragment);
        end += "</div>".Length;
        while (end < code.Length && (code[end] == '\r' || code[end] == '\n'))
            end++;

        return code.Remove(start, end - start);
    }

    public static string RemoveMembersContaining(string code, string names)
    {
        foreach (var name in names.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            code = RemovePublicProperty(code, name);
            code = RemoveLineContainingWord(code, name);
        }
        return code;
    }

    /// <summary>Удаляет строки, где <paramref name="word"/> — отдельный идентификатор (не часть StatusMessage и т.п.).</summary>
    public static string RemoveLineContainingWord(string code, string word)
    {
        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(word)) return code;

        var pattern = $@"\b{Regex.Escape(word)}\b";
        var lines = code.Split('\n').Where(l => !Regex.IsMatch(l, pattern)).ToList();
        return string.Join('\n', lines);
    }

    private static int FindAttributeBlockStart(string code, int fromIndex)
    {
        var start = fromIndex;
        while (start > 0)
        {
            var prev = code.LastIndexOf('\n', start - 2);
            if (prev < 0) break;
            var line = code[(prev + 1)..start].Trim();
            if (line.StartsWith('['))
            {
                start = prev + 1;
                continue;
            }
            break;
        }
        return start;
    }

    private static int FindExpressionBodyEnd(string code, int fromIndex)
    {
        var depth = 0;
        var inString = false;
        var inChar = false;
        var verbatim = false;
        char stringQuote = '"';

        for (var i = fromIndex; i < code.Length; i++)
        {
            var c = code[i];
            if (inString)
            {
                if (verbatim)
                {
                    if (c == '"' && i + 1 < code.Length && code[i + 1] == '"') { i++; continue; }
                    if (c == stringQuote) inString = false;
                }
                else
                {
                    if (c == '\\') { i++; continue; }
                    if (c == stringQuote) inString = false;
                }
                continue;
            }

            if (inChar)
            {
                if (c == '\\') { i++; continue; }
                if (c == '\'') inChar = false;
                continue;
            }

            if (c == '@' && i + 1 < code.Length && code[i + 1] == '"')
            {
                verbatim = true;
                inString = true;
                stringQuote = '"';
                i++;
                continue;
            }

            if (c == '"') { inString = true; verbatim = false; stringQuote = '"'; continue; }
            if (c == '\'') { inChar = true; continue; }

            if (c == '(' || c == '[' || c == '{') depth++;
            else if (c == ')' || c == ']' || c == '}') depth--;
            else if (c == ';' && depth == 0) return i + 1;
        }

        return code.Length;
    }

    private static int FindMethodBodyBrace(string code, int searchFrom)
    {
        for (var i = searchFrom; i < code.Length; i++)
        {
            var c = code[i];
            if (c == '{') return i;
            if (c == ';' || c == '\n') break;
        }
        return -1;
    }

    private static int FindMatchingParen(string code, int openIndex)
    {
        var depth = 0;
        var inString = false;
        var inChar = false;
        var verbatim = false;
        char stringQuote = '"';

        for (var i = openIndex; i < code.Length; i++)
        {
            var c = code[i];
            if (inString)
            {
                if (verbatim)
                {
                    if (c == '"' && i + 1 < code.Length && code[i + 1] == '"') { i++; continue; }
                    if (c == stringQuote) inString = false;
                }
                else
                {
                    if (c == '\\') { i++; continue; }
                    if (c == stringQuote) inString = false;
                }
                continue;
            }

            if (inChar)
            {
                if (c == '\\') { i++; continue; }
                if (c == '\'') inChar = false;
                continue;
            }

            if (c == '@' && i + 1 < code.Length && code[i + 1] == '"')
            {
                verbatim = true;
                inString = true;
                stringQuote = '"';
                i++;
                continue;
            }

            if (c == '"') { inString = true; verbatim = false; stringQuote = '"'; continue; }
            if (c == '\'') { inChar = true; continue; }

            if (c == '(') depth++;
            else if (c == ')')
            {
                depth--;
                if (depth == 0) return i;
            }
        }

        return -1;
    }

    private static int FindDocStart(string code, int fromIndex)
    {
        var docStart = fromIndex;
        while (docStart > 0)
        {
            var prev = code.LastIndexOf('\n', docStart - 2);
            if (prev < 0) break;
            var line = code[(prev + 1)..docStart].Trim();
            if (line.StartsWith("///") || line.StartsWith("///"))
            {
                docStart = prev + 1;
                continue;
            }
            break;
        }
        return docStart;
    }

    private static string RemoveTypeAt(string code, int idx)
    {
        var lineStart = code.LastIndexOf('\n', Math.Max(0, idx - 1));
        lineStart = lineStart < 0 ? 0 : lineStart + 1;

        var attrStart = lineStart;
        while (attrStart > 0)
        {
            var prev = code.LastIndexOf('\n', attrStart - 2);
            if (prev < 0) break;
            var line = code[(prev + 1)..attrStart].Trim();
            if (line.StartsWith('['))
            {
                attrStart = prev + 1;
                continue;
            }
            break;
        }

        var docStart = attrStart;
        while (docStart > 0)
        {
            var prev = code.LastIndexOf('\n', docStart - 2);
            if (prev < 0) break;
            var line = code[(prev + 1)..docStart].Trim();
            if (line.StartsWith("///") || line.StartsWith("///"))
            {
                docStart = prev + 1;
                continue;
            }
            break;
        }

        var braceStart = code.IndexOf('{', idx);
        if (braceStart < 0) return code;

        var end = FindMatchingBrace(code, braceStart);
        if (end < 0) return code;

        var tail = end + 1;
        while (tail < code.Length && (code[tail] == '\r' || code[tail] == '\n'))
            tail++;

        return code.Remove(docStart, tail - docStart);
    }

    private static int FindMatchingBrace(string code, int openIndex)
    {
        var depth = 0;
        var inString = false;
        var inChar = false;
        var verbatim = false;
        char stringQuote = '"';

        for (var i = openIndex; i < code.Length; i++)
        {
            var c = code[i];
            if (inString)
            {
                if (verbatim)
                {
                    if (c == '"' && i + 1 < code.Length && code[i + 1] == '"') { i++; continue; }
                    if (c == stringQuote) inString = false;
                }
                else
                {
                    if (c == '\\') { i++; continue; }
                    if (c == stringQuote) inString = false;
                }
                continue;
            }

            if (inChar)
            {
                if (c == '\\') { i++; continue; }
                if (c == '\'') inChar = false;
                continue;
            }

            if (c == '@' && i + 1 < code.Length && code[i + 1] == '"')
            {
                verbatim = true;
                inString = true;
                stringQuote = '"';
                i++;
                continue;
            }

            if (c == '"') { inString = true; verbatim = false; stringQuote = '"'; continue; }
            if (c == '\'') { inChar = true; continue; }

            if (c == '{') depth++;
            else if (c == '}')
            {
                depth--;
                if (depth == 0) return i;
            }
        }

        return -1;
    }
}
