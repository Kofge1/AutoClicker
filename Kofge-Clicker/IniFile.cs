using System.Runtime.InteropServices;
using System.Text;

namespace KofgeClicker;

public sealed class IniFile
{
    private readonly string _path;

    public IniFile(string path)
    {
        _path = path;
    }

    public string ReadString(string section, string key, string defaultValue = "")
    {
        var buffer = new StringBuilder(2048);
        NativeMethods.GetPrivateProfileString(section, key, defaultValue, buffer, buffer.Capacity, _path);
        return buffer.ToString();
    }

    public int ReadInt(string section, string key, int defaultValue = 0)
    {
        var text = ReadString(section, key, defaultValue.ToString());
        return int.TryParse(text, out var value) ? value : defaultValue;
    }

    public bool ReadBool(string section, string key, bool defaultValue = false)
    {
        var text = ReadString(section, key, defaultValue ? "1" : "0");
        return text == "1" || text.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    public void WriteString(string section, string key, string value)
    {
        NativeMethods.WritePrivateProfileString(section, key, value, _path);
    }

    public void WriteInt(string section, string key, int value)
    {
        WriteString(section, key, value.ToString());
    }

    public void WriteBool(string section, string key, bool value)
    {
        WriteString(section, key, value ? "1" : "0");
    }

    public void WriteSection(string section, IEnumerable<KeyValuePair<string, string>> values)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path) ?? AppContext.BaseDirectory);

        var lines = File.Exists(_path)
            ? File.ReadAllLines(_path).ToList()
            : [];
        var sectionHeader = $"[{section}]";
        var sectionStart = lines.FindIndex(line => string.Equals(line.Trim(), sectionHeader, StringComparison.OrdinalIgnoreCase));
        var newSectionLines = new List<string> { sectionHeader };
        newSectionLines.AddRange(values.Select(pair => $"{pair.Key}={pair.Value}"));

        if (sectionStart < 0)
        {
            if (lines.Count > 0 && lines[^1].Length > 0)
            {
                lines.Add(string.Empty);
            }

            lines.AddRange(newSectionLines);
            File.WriteAllLines(_path, lines, Encoding.UTF8);
            return;
        }

        var sectionEnd = sectionStart + 1;
        while (sectionEnd < lines.Count && !IsSectionHeader(lines[sectionEnd]))
        {
            sectionEnd++;
        }

        lines.RemoveRange(sectionStart, sectionEnd - sectionStart);
        lines.InsertRange(sectionStart, newSectionLines);
        File.WriteAllLines(_path, lines, Encoding.UTF8);
    }

    public void DeleteKey(string section, string key)
    {
        NativeMethods.WritePrivateProfileString(section, key, null, _path);
    }

    public void DeleteSection(string section)
    {
        NativeMethods.WritePrivateProfileString(section, null, null, _path);
    }

    private static bool IsSectionHeader(string line)
    {
        var trimmed = line.Trim();
        return trimmed.Length >= 2 && trimmed[0] == '[' && trimmed[^1] == ']';
    }
}
