using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Whub.Core;

namespace Whub.Pages;

public static class PortraitStack
{
    const int MaxShown = 6;
    const double Size = 34;

    public static UIElement? ForAccounts()
    {
        if (HubData.Accounts.Count == 0)
            return null;
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var account in HubData.Accounts.Take(MaxShown))
        {
            var circle = Circle(account);
            if (row.Children.Count > 0)
                circle.Margin = new Thickness(-9, 0, 0, 0);
            row.Children.Add(circle);
        }
        return row;
    }

    static Border Circle(AccountRecord account)
    {
        var circle = new Border
        {
            Width = Size,
            Height = Size,
            CornerRadius = new CornerRadius(Size / 2),
            BorderBrush = (Brush)Application.Current.FindResource("Surface"),
            BorderThickness = new Thickness(2),
            Background = (Brush)Application.Current.FindResource("Raised"),
        };
        var portrait = Portraits.Load(account.Id);
        if (portrait != null)
            circle.Background = new ImageBrush(portrait) { Stretch = Stretch.UniformToFill };
        else
            circle.Child = new TextBlock
            {
                Text = account.Name[..Math.Min(1, account.Name.Length)].ToUpperInvariant(),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 12,
            };
        return circle;
    }
}
