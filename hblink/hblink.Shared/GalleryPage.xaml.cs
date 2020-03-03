using hblink.libs;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using hblink.Platform;

// 空白ページの項目テンプレートについては、https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x411 を参照してください

namespace hblink
{
    /// <summary>
    /// それ自体で使用できる空白ページまたはフレーム内に移動できる空白ページ。
    /// </summary>
    public sealed partial class GalleryPage : Page
    {
        private readonly ConnectionManager _manager;
        private int _queueCount;

        public ViewModel ViewModel { get; } = new ViewModel();

        public GalleryPage()
        {
            this.InitializeComponent();
            _manager = new ConnectionManager();
            _manager.Started += _manager_Started;
            _manager.Stopped += _manager_Stopped;
            _manager.NewFile += _manager_NewFile;
            Loaded += GalleryPage_Loaded;
            Unloaded += GalleryPage_Unloaded;
            DataContext = ViewModel;
        }

        private void GalleryPage_Loaded(object sender, RoutedEventArgs e)
        {
            Dispatcher.RunIdleAsync(_ => _manager.Start());
        }

        private void GalleryPage_Unloaded(object sender, RoutedEventArgs e)
        {
            _manager.Stop();
        }

        private async void _manager_Started(object sender, EventArgs e)
        {
            statusText.Text = "Status: Connected";
            var indices = await Task.WhenAll(_manager.PutRequest(new DownloadIndex(0))
                , _manager.PutRequest(new DownloadIndex(1)));
            var items = indices.SelectMany(i => i.Table).Select(EntryItem.From).OrderByDescending(x => x.CreatedAt).ToArray();
            ViewModel.Items.Clear();
            foreach (var i in items)
            {
                RequestUpdateThumbnail(i);
                ViewModel.Items.Add(i);
            }
        }

        private void _manager_Stopped(object sender, EventArgs e)
        {
            statusText.Text = "Status: Disconnected";
        }

        private void _manager_NewFile(object sender, DownloadIndex.Entry e)
        {
            var i = EntryItem.From(e);
            RequestUpdateThumbnail(i);
            ViewModel.Items.Insert(0, i);
        }

        private async void RequestUpdateThumbnail(EntryItem i)
        {
            var name = Path.GetFileName($"thumb_{Path.GetFileName(i.FilePath)}.JPG");
            var file = await CreateFile(GetTemporaryFolder(), name);
            await DownloadRequest(file, new DownloadJpeg { Path = i.FilePath, });
            i.Thumbnail = new Uri(file.Path);
        }

        private Task<StorageFile> CreateFile(StorageFolder folder, string name)
        {
            return folder.CreateFileAsync(name, CreationCollisionOption.ReplaceExisting).AsTask();
        }

        private StorageFolder GetTemporaryFolder()
        {
            return ApplicationData.Current.TemporaryFolder;
        }

        private async Task<StorageFolder> GetLibraryFolder()
        {
            var library = await StorageFolder.GetFolderFromPathAsync(Platform.Injection.GetPicturesPath());
#if __IOS__
            return library;
#else
            return await library.CreateFolderAsync("HbLink", CreationCollisionOption.OpenIfExists);
#endif
        }

        private async void MenuFlyoutItem_Download(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedItem == null) return;
            var item = ViewModel.SelectedItem;
            var file = await CreateFile(await GetLibraryFolder(), item.FileName);
            await DownloadRequest(file, item.IsJpeg
                    ? (Download)new DownloadJpeg { Path = item.FilePath }
                    : new DownloadRaw { Path = item.FilePath, FileSize = item.FileSize });
#if __IOS__
            await Injection.SaveToLibrary(file.Path, "HbLink");
            await file.DeleteAsync();
#endif
#if __ANDROID__
            Injection.ScanFile(Context, file.Path);
#endif
            new MessageDialog("download completed").ShowAsync();
        }

        private async void MenuFlyoutItem_ShareJpeg(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedItem == null) return;
            var item = ViewModel.SelectedItem;
            ShareRequest(
                await CreateFile(GetTemporaryFolder(), Path.ChangeExtension(item.FileName, ".JPG")),
                new DownloadJpeg { Path = item.FilePath, });
        }

        private async void MenuFlyoutItem_ShareRaw(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedItem == null) return;
            var item = ViewModel.SelectedItem;
            if (item.IsJpeg)
            {
                MenuFlyoutItem_ShareJpeg(sender, e);
                return;
            }
            ShareRequest(
                await CreateFile(GetTemporaryFolder(), item.FileName),
                new DownloadRaw { Path = item.FilePath, FileSize = item.FileSize });
        }

        private async void ShareRequest(StorageFile target, Download request)
        {
            await DownloadRequest(target, request);
#if __IOS__
            Injection.ShareImage(target.Path);
#endif
#if __ANDROID__
            Injection.ShareImage(Context, request is DownloadJpeg ? "image/jpeg" : "image/x-3rf", target.Path);
#endif
        }

        private async Task DownloadRequest(StorageFile target, Download request)
        {
            var output = await target.OpenStreamForWriteAsync();
            switch (request)
            {
                case DownloadJpeg jpeg:
                    jpeg.Output = output;
                    break;
                case DownloadRaw raw:
                    raw.Output = output;
                    break;
                default:
                    return;
            }
            Interlocked.Increment(ref _queueCount);
            progressBox.Visibility = Visibility.Visible;
            progressText.Text = "0%";
            var sub = request.Progress.ObserveOn(SynchronizationContext.Current).Subscribe(x => progressText.Text = $"{x}%");
            await _manager.PutRequest(request);
            output.Dispose();
            if (Interlocked.Decrement(ref _queueCount) == 0)
            {
                progressBox.Visibility = Visibility.Collapsed;
            }
            sub.Dispose();
        }

        private void Image_OnImageOpened(object sender, RoutedEventArgs e)
        {
            /*
             *         
             */
            var image = (Image)sender;
            //scrollViewer.ChangeView(null, null, (float)scrollViewer.ActualWidth / 2048);
        }

        private void Reconnect(object sender, RoutedEventArgs e)
        {
            _manager.Stop();
            _manager.Start();
        }
    }

    public class EntryItem : INotifyPropertyChanged
    {
        private Uri _thumbnail;

        public string FilePath { get; set; }
        public string FileName { get; set; }
        public Uri Thumbnail { get => _thumbnail; set { _thumbnail = value; OnPropertyChanged(); } }
        public int FileSize { get; set; }
        public bool IsJpeg { get; set; }
        public DateTime CreatedAt { get; set; }
        public int SD { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public static EntryItem From(DownloadIndex.Entry x)
        {
            return new EntryItem
            {
                FilePath = x.Path,
                FileName = Path.GetFileName(x.Path),
                FileSize = x.Size,
                IsJpeg = x.IsJpeg,
                CreatedAt = x.CreatedAt,
                SD = x.Path.StartsWith("/SD1/") ? 1 : 2,
            };
        }
    }

    public class ViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<EntryItem> Items { get; } = new ObservableCollection<EntryItem>();
        public EntryItem SelectedItem { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
