using System.Xml.Linq;

namespace AetherXIV.Launcher.Core;

public static class ServerXmlWriter
{
    public static XDocument CreateDocument(IEnumerable<ServerProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);

        XElement root = new("Servers");
        foreach (ServerProfile profile in profiles)
        {
            profile.Validate();
            root.Add(new XElement("Server",
                new XAttribute("Name", profile.Name),
                new XAttribute("Address", profile.Host),
                new XAttribute("LoginUrl", profile.LoginUrl)));
        }

        return new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root);
    }

    public static string ToXml(IEnumerable<ServerProfile> profiles)
    {
        return CreateDocument(profiles).ToString(SaveOptions.DisableFormatting);
    }

    public static void Write(string path, IEnumerable<ServerProfile> profiles)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
        CreateDocument(profiles).Save(path);
    }
}
