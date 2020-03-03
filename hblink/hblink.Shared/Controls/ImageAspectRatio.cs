using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace hblink.Shared.Controls
{
    class Dimension
    {
        public static readonly DependencyProperty RatioProperty = DependencyProperty.RegisterAttached(
            "Ratio", typeof(string), typeof(Dimension), new PropertyMetadata(default(string), RatioChanged));

        private static void RatioChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var image = (FrameworkElement)d;
            image.SizeChanged -= ImageOnSizeChanged;
            if (string.IsNullOrEmpty((string)e.NewValue)) return;
            image.SizeChanged += ImageOnSizeChanged;
            UpdateLayout(image, (string)e.NewValue);
        }

        private static void ImageOnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateLayout((FrameworkElement)sender, GetRatio((DependencyObject)sender));
        }

        private static void UpdateLayout(FrameworkElement image, string layoutParam)
        {
            var p = layoutParam.Split(',');
            var mode = p[0].ToLowerInvariant();
            var ratio = p[1].Split(':');
            switch (mode)
            {
                case "w":
                    image.Width = image.ActualHeight / int.Parse(ratio[1]) * int.Parse(ratio[0]);
                    break;
                case "h":
                    image.Height = image.ActualWidth / int.Parse(ratio[1]) * int.Parse(ratio[0]);
                    break;
            }
        }

        public static void SetRatio(DependencyObject element, string value)
        {
            element.SetValue(RatioProperty, value);
        }

        public static string GetRatio(DependencyObject element)
        {
            return (string)element.GetValue(RatioProperty);
        }
    }
}
