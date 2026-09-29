using System.IO;

public static class AirSettingsStorage
{
    // ADB-created files may be readable but not writable by the app. Replace the
    // directory entry atomically instead of opening the existing inode for writing.
    public static void Save(string path, string json)
    {
        string temporary = path + ".new";
        File.WriteAllText(temporary, json);
        if (File.Exists(path)) File.Replace(temporary, path, null);
        else File.Move(temporary, path);
    }
}
