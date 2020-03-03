using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CoreBluetooth;
using Foundation;
using Photos;
using UIKit;

namespace hblink.Platform
{
    public static class Injection
    {
        public static string GetPicturesPath()
        {
            return Path.GetTempPath();
        }

        public static async Task<bool> SaveToLibrary(string path, string albumName)
        {
            var source = new TaskCompletionSource<bool>();
            PHAssetCollection album = await GetAlbum(albumName);
            PHPhotoLibrary.SharedPhotoLibrary.PerformChanges(() =>
            {
                var assetReq = PHAssetChangeRequest.FromImage(NSUrl.CreateFileUrl(path, null));
                var albumReq = PHAssetCollectionChangeRequest.ChangeRequest(album);
                var placeholder = assetReq.PlaceholderForCreatedAsset;
                albumReq.AddAssets(new[] { (PHObject)placeholder });
            }, (success, err) =>
            {
                Debug.WriteLine($"SaveToLibrary: {success}");
                source.SetResult(success);
            });
            return await source.Task;
        }

        #region GetAlbum
        public static Task<PHAssetCollection> GetAlbum(string name)
        {
            var options = new PHFetchOptions();
            options.Predicate = NSPredicate.FromFormat("title = %@", NSObject.FromObject(name));
            var collection = PHAssetCollection.FetchAssetCollections(PHAssetCollectionType.Album, PHAssetCollectionSubtype.Any, options);
            return collection.firstObject != null
                ? Task.FromResult((PHAssetCollection)collection.firstObject)
                : CreateAlbum(name);
        }

        public static Task<PHAssetCollection> CreateAlbum(string name)
        {
            var source = new TaskCompletionSource<PHAssetCollection>();
            PHObjectPlaceholder placeholder = null;
            PHPhotoLibrary.SharedPhotoLibrary.PerformChanges(() =>
            {
                var request = PHAssetCollectionChangeRequest.CreateAssetCollection(name);
                placeholder = request.PlaceholderForCreatedAssetCollection;
            }, (success, err) =>
            {
                if (success)
                {
                    var result = PHAssetCollection.FetchAssetCollections(new[] { placeholder.LocalIdentifier }, null);
                    source.SetResult((PHAssetCollection)result.firstObject);
                }
                else
                {
                    source.SetException(new Exception(err.ToString()));
                }
            });
            return source.Task;
        }
        #endregion

        public static void ShareImage(string path)
        {
            var fileURL = new NSUrl(path, false);
            var controller = new UIActivityViewController(new NSObject[] { fileURL }, null);
            var window = UIApplication.SharedApplication.Windows.FirstOrDefault(w => w.RootViewController != null);
            window.RootViewController.PresentViewController(controller, true, null);
        }

        public static async void RequestPermission()
        {
            await PHPhotoLibrary.RequestAuthorizationAsync();
        }

        public static string GetDeviceId()
        {
            return UIDevice.CurrentDevice.IdentifierForVendor.AsString().Substring(30);
        }
    }
}