using System.Windows;
using Whub.Core;

namespace Whub.Pages;

public static class AnimateLauncher
{
    public static async void Open()
    {
        var path = Programs.FindAnimate();
        if (path == null)
        {
            await AnimateUpdates.InstallLatest();
        }
        else if (path == AnimateUpdates.InstalledExe && AnimateUpdates.UpdateAvailable)
        {
            var answer = MessageBox.Show($"welffi animate {AnimateUpdates.Latest!.Tag} is out. Update before opening?", "welffi hub", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (answer == MessageBoxResult.Cancel)
                return;
            if (answer == MessageBoxResult.Yes)
                await AnimateUpdates.InstallLatest();
        }
        Programs.LaunchAnimate();
    }
}
