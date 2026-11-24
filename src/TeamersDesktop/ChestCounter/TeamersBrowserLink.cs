using System;
using System.IO;
using System.Linq;
using System.Security;
using Microsoft.Win32;

namespace ChestCounter;

internal static class TeamersBrowserLink
{
    private const string Scheme = "chestcounter-teamers";

    public static bool TryParse(string value, out string code)
    {
        code = "";
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) || uri.Scheme != Scheme ||
            uri.Host != "connect" || uri.Port != -1 || uri.UserInfo.Length != 0 ||
            uri.Query.Length != 0 || uri.Fragment.Length != 0 || uri.AbsolutePath.Length != 33)
            return false;
        string candidate = uri.AbsolutePath[1..];
        if (!candidate.All(Uri.IsHexDigit)) return false;
        code = candidate.ToLowerInvariant();
        return true;
    }

    public static void Register(Store store)
    {
        string? executable = Environment.ProcessPath;
        if (executable == null || !Path.GetExtension(executable).Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
            Path.GetFileName(executable).Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase)) return;
        // The site can open this one dialog. It never receives the local data directory.
        try
        {
            using var protocol = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + Scheme);
            protocol.SetValue("", "URL:ChestCounter Teamers");
            protocol.SetValue("URL Protocol", "");
            using var action = protocol.CreateSubKey(@"shell\open\command");
            string folder = Path.GetFullPath(store.Folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            action.SetValue("", $"\"{executable}\" --teamers-app \"{folder}\" \"%1\"");
        }
        catch (Exception problem) when (problem is IOException or UnauthorizedAccessException or SecurityException)
        {
            // The manual code in Teamers still works when protocol registration is restricted.
        }
    }
}
