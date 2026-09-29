using System.IO;
using System.Xml.Linq;
using UnityEditor.Android;
public sealed class AirVoiceAndroidBuild : IPostGenerateGradleAndroidProject
{
    public int callbackOrder => 50;
    public void OnPostGenerateGradleAndroidProject(string path)
    {
        var file = Path.Combine(path, "src/main/AndroidManifest.xml");
        var doc = XDocument.Load(file); XNamespace android = "http://schemas.android.com/apk/res/android";
        foreach (string permission in new[] { "android.permission.RECORD_AUDIO", "android.permission.MODIFY_AUDIO_SETTINGS" }) {
            bool found = false;
            foreach (var element in doc.Root.Elements("uses-permission")) if ((string)element.Attribute(android + "name") == permission) found = true;
            if (!found) doc.Root.Add(new XElement("uses-permission", new XAttribute(android + "name", permission)));
        }
        doc.Save(file);
    }
}
