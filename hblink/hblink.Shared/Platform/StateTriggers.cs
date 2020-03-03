using System;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;

namespace hblink.Shared.Platform
{
    public class FoldableStateTrigger : StateTriggerBase
    {
        private bool _isFoldableDevice;

        public bool IsFoldableDevice
        {
            get => _isFoldableDevice;
            set
            {
                _isFoldableDevice = value;
                SetActive(value);
            }
        }

        public FoldableStateTrigger()
        {
            var view = ApplicationView.GetForCurrentView();
            view.VisibleBoundsChanged += View_VisibleBoundsChanged;
            View_VisibleBoundsChanged(view, null);
        }

        private void View_VisibleBoundsChanged(ApplicationView sender, object args)
        {
            var bounds = sender.VisibleBounds;
            IsFoldableDevice = Math.Abs(1 - bounds.Width / bounds.Height) < 0.25;
        }
    }
}
