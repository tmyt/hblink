using System.Linq;
using System.Threading.Tasks;
using Android;
using Android.App;
using Android.OS;
using Android.Content.PM;
using Android.Views;

namespace hblink.Droid
{
    [Activity(
            MainLauncher = true,
            ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize,
            WindowSoftInputMode = SoftInput.AdjustPan | SoftInput.StateHidden
        )]
    public class MainActivity : Windows.UI.Xaml.ApplicationActivity
    {
        private static readonly string[] RequiredPermissions = {
            Manifest.Permission.WriteExternalStorage,
            Manifest.Permission.AccessFineLocation,
        };

        private TaskCompletionSource<bool> _onGoingRequest;

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
        {
            _onGoingRequest.SetResult(grantResults.All(p => p == Permission.Granted));
        }

        private Permission CheckSelfPermissions(string[] permissions)
        {
            return permissions.All(s => CheckSelfPermission(s) == Permission.Granted)
                ? Permission.Granted
                : Permission.Denied;
        }

        public bool IsAllPermissionsGranted()
        {
            return CheckSelfPermissions(RequiredPermissions) == Permission.Granted;
        }

        public Task<bool> RequestPermissions()
        {
            _onGoingRequest = new TaskCompletionSource<bool>();
            RequestPermissions(RequiredPermissions, 1);
            return _onGoingRequest.Task;
        }
    }
}

