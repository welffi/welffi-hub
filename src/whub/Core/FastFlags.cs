using System.IO;
using System.Text;

namespace Whub.Core;

public static class FastFlags
{
    static readonly (string Key, string Value)[] Values =
    [
        ("DFFlagTextureQualityOverrideEnabled", "true"),
        ("DFIntCSGLevelOfDetailSwitchingDistance", "0"),
        ("DFIntCSGLevelOfDetailSwitchingDistanceL12", "0"),
        ("DFIntCSGLevelOfDetailSwitchingDistanceL23", "0"),
        ("DFIntCSGLevelOfDetailSwitchingDistanceL34", "0"),
        ("DFIntDebugFRMQualityLevelOverride", "1"),
        ("DFIntTaskSchedulerTargetFps", "30"),
        ("DFIntTextureQualityOverride", "0"),
        ("FFlagDebugSkyGray", "true"),
        ("FFlagDisablePostFx", "true"),
        ("FIntDebugForceMSAASamples", "1"),
        ("FIntFRMMaxGrassDistance", "0"),
        ("FIntFRMMinGrassDistance", "0"),
        ("FIntRenderGrassDetailStrands", "0"),
        ("FIntRenderShadowIntensity", "0"),
    ];

    public static string Json => "{\n" + string.Join(",\n", Values.Select(v => $"  \"{v.Key}\": {v.Value}")) + "\n}";

    static string VersionsRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox", "Versions");

    public static void Apply(bool on) => Apply(on, VersionsRoot);

    public static void Apply(bool on, string versionsRoot)
    {
        if (!Directory.Exists(versionsRoot))
            return;
        foreach (var version in Directory.GetDirectories(versionsRoot))
        {
            if (File.Exists(Path.Combine(version, "RobloxPlayerBeta.exe")))
                ApplyToFile(on, Path.Combine(version, "ClientSettings", "ClientAppSettings.json"));
        }
    }

    static void ApplyToFile(bool on, string file)
    {
        var backup = file + ".welffi.bak";
        var exists = File.Exists(file);
        var mine = exists && File.ReadAllText(file) == Json;
        if (on)
        {
            if (exists && !mine && !File.Exists(backup))
                File.Copy(file, backup);
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, Json, new UTF8Encoding(false));
        }
        else if (File.Exists(backup))
        {
            File.Move(backup, file, true);
        }
        else if (mine)
        {
            File.Delete(file);
        }
    }
}
