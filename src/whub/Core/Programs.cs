using System.Diagnostics;

namespace Whub.Core;

public static class Programs
{
    static readonly string AnimateBuild = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ProgramProjects", "welffi animate", "dist", "welffi animate.exe");

    static string AnimateBesideHub => Path.Combine(AppContext.BaseDirectory, "welffi animate.exe");

    public static string? FindAnimate() => new[] { AnimateBesideHub, AnimateBuild, AnimateUpdates.InstalledExe }.FirstOrDefault(File.Exists);

    public static string AnimateStatus()
    {
        if (AnimateUpdates.Downloading)
            return "Downloading...";
        var path = FindAnimate();
        if (path == null)
            return AnimateUpdates.Latest == null ? "Click to download" : $"Click to download {AnimateUpdates.Latest.Tag}";
        if (path == AnimateUpdates.InstalledExe && AnimateUpdates.UpdateAvailable)
            return $"Update available: {AnimateUpdates.Latest!.Tag}";
        return "Opens in its own window";
    }

    public static void LaunchAnimate()
    {
        var path = FindAnimate() ?? throw new FileNotFoundException("welffi animate isn't installed.");
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(path)! });
    }
}
