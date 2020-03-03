using Windows.UI.Xaml;

namespace Uno.UI.Toolkit
{
    class VisibleBoundsPadding
    {
        public static readonly DependencyProperty PaddingMaskProperty = DependencyProperty.RegisterAttached(
            "PaddingMask", typeof(string), typeof(VisibleBoundsPadding), new PropertyMetadata(default(string)));

        public static void SetPaddingMask(DependencyObject element, string value)
        {
            element.SetValue(PaddingMaskProperty, value);
        }

        public static string GetPaddingMask(DependencyObject element)
        {
            return (string)element.GetValue(PaddingMaskProperty);
        }
    }
}
