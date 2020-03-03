using System.Linq;
using Android.Bluetooth;
using Android.Bluetooth.LE;
using Android.Content;
using Android.Graphics;
using Android.Net;
using Android.OS;
using Android.Provider;
using Android.Support.V4.Content;
using Java.IO;
using Java.Lang;
using Java.Util;
using Environment = Android.OS.Environment;
using ScanMode = Android.Bluetooth.LE.ScanMode;

namespace hblink.Platform
{
    public static class Injection
    {
        public static string GetPicturesPath()
        {
            return Environment.GetExternalStoragePublicDirectory(Environment.DirectoryPictures).AbsolutePath;
        }

        public static void ScanFile(Context context, string path)
        {
            Uri contentUri = Uri.Parse(path);
            Intent mediaScanIntent = new Intent(Intent.ActionMediaScannerScanFile, contentUri);
            context.SendBroadcast(mediaScanIntent);
        }

        public static void ShareImage(Context context, string mime, string path)
        {
            var contentUri = FileProvider.GetUriForFile(context, $"{context.PackageName}.fileprovider", new File(path));
            var intent = new Intent(Intent.ActionSend);
            intent.SetDataAndType(contentUri, mime);
            intent.AddFlags(ActivityFlags.GrantWriteUriPermission | ActivityFlags.GrantReadUriPermission);
            context.StartActivity(Intent.CreateChooser(intent, "Send Picture"));
        }

        public static string GetDeviceId(Context context)
        {
            var androidId = Settings.Secure.GetString(context.ContentResolver, Settings.Secure.AndroidId);
            return androidId.Substring(0, 6).PadLeft(6, '0');
        }
    }
}