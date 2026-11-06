using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Whub.Core;

public static class Portraits
{
    public static ImageSource? Load(string id)
    {
        var file = Path.Combine(Paths.Portraits, id + ".png");
        if (!File.Exists(file))
            return null;
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 96;
            image.UriSource = new Uri(file);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception e) when (e is IOException or NotSupportedException)
        {
            return null;
        }
    }

    public static async Task Refresh()
    {
        try
        {
            await RobloxApi.FetchPortraits(HubData.Accounts.Select(a => a.Id).ToList());
        }
        catch (Exception e) when (RobloxClient.IsExpected(e))
        {
            return;
        }
        HubData.RaisePortraitsUpdated();
    }

    public static void Delete(string id)
    {
        var file = Path.Combine(Paths.Portraits, id + ".png");
        if (File.Exists(file))
            File.Delete(file);
    }
}
