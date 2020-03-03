using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace hblink.Shared.Controls
{
    class ItemsWrapGridEx
    {
        public static readonly DependencyProperty ItemWidthSupportProperty = DependencyProperty.RegisterAttached(
            "ItemWidthSupport", typeof(bool), typeof(ItemsWrapGridEx), new PropertyMetadata(default(bool), ItemWidthSupportChanged));

        private static void ItemWidthSupportChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var gridView = (GridView)d;
            gridView.SizeChanged -= GridViewOnSizeChanged;
            if (!(bool)e.NewValue) return;
            gridView.SizeChanged += GridViewOnSizeChanged;
            UpdateLayout(gridView);
        }

        private static void GridViewOnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateLayout((GridView)sender);
        }

        private static void UpdateLayout(GridView gridView)
        {
            var panel = (ItemsWrapGrid)gridView.ItemsPanelRoot;
            if (panel == null) return;
            panel.ItemWidth = gridView.ActualWidth / panel.MaximumRowsOrColumns;
            panel.ItemHeight = panel.ItemWidth / 4 * 3;
        }

        public static void SetItemWidthSupport(DependencyObject element, bool value)
        {
            element.SetValue(ItemWidthSupportProperty, value);
        }

        public static bool GetItemWidthSupport(DependencyObject element)
        {
            return (bool)element.GetValue(ItemWidthSupportProperty);
        }
    }
}
