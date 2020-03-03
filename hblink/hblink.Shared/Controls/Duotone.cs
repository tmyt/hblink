using System;
using System.Collections.Generic;
using System.Text;
using Windows.ApplicationModel.Store;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace hblink.Shared.Controls
{
    public partial class Duotone : Control
    {
        public static readonly DependencyProperty PrimaryProperty = DependencyProperty.Register(
            "Primary", typeof(string), typeof(Duotone), new PropertyMetadata(default(string)));

        public string Primary
        {
            get { return (string)GetValue(PrimaryProperty); }
            set { SetValue(PrimaryProperty, value); }
        }

        public static readonly DependencyProperty SecondaryProperty = DependencyProperty.Register(
            "Secondary", typeof(string), typeof(Duotone), new PropertyMetadata(default(string)));

        public string Secondary
        {
            get { return (string)GetValue(SecondaryProperty); }
            set { SetValue(SecondaryProperty, value); }
        }

        public Duotone()
        {
            DefaultStyleKey = typeof(Duotone);
        }
    }
}
