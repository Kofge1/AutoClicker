using System.Runtime.InteropServices;
using System.Text;

namespace AutoClickerCs;

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

    public void DeleteKey(string section, string key)
    {
        NativeMethods.WritePrivateProfileString(section, key, null, _path);
    }

    public void DeleteSection(string section)
    {
        NativeMethods.WritePrivateProfileString(section, null, null, _path);
    }
}
