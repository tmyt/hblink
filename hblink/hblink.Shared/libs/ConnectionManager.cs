using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;

namespace hblink.libs
{
    public class ConnectionManager
    {
        public enum ConnectionStatus
        {
            Starting,
            Started,
            Stopping,
            Stopped,
        }

        private const int MaxConcurrentJobs = 3;

        private readonly Dictionary<ushort, Download> _downloadSession;
        private readonly Queue<Download> _downloadQueue;
        private TcpClient _client;
        private Thread _receiveThread;
        private ushort _counter;

        public event EventHandler Started;
        public event EventHandler Stopped;
        public event EventHandler<Exception> Error;
        public event EventHandler<DownloadIndex.Entry> NewFile;

        public ConnectionStatus Status { get; private set; }

        public ConnectionManager()
        {
            _downloadSession = new Dictionary<ushort, Download>();
            _downloadQueue = new Queue<Download>();
            Status = ConnectionStatus.Stopped;
        }

        public async void Start(string server = "192.168.2.1", int port = 9003)
        {
            Status = ConnectionStatus.Starting;
            for (var i = 0; i < 10; ++i)
            {
                try
                {
                    _client = new TcpClient(AddressFamily.InterNetwork) { ReceiveTimeout = 15 * 1000, SendTimeout = 5 * 1000 };
                    _client.Connect(server, port);
                    _receiveThread = new Thread(ReceiveThread);
                    _receiveThread.Start();
                    return;
                }
                catch (Exception)
                {
                    _client.Dispose();
                    await Task.Delay(333);
                }
            }
            Stop();
        }

        public void Stop()
        {
            RequestStop(null);
        }

        public static async Task<bool> CheckServerAvailable(string server = "192.168.2.1", int port = 9003)
        {
            var client = new TcpClient(AddressFamily.InterNetwork)
            {
                SendTimeout = 15 * 1000,
                ReceiveTimeout = 15 * 1000,
            };
            try
            {
                await client.ConnectAsync(server, port);
                var stream = client.GetStream();
                var header = new byte[8];
                await stream.ReadAsync(header, 0, 8);
                if (header[0] == 0x55 && header[1] == 0xCC) return true;
                return false;
            }
            catch
            {
                return false;
            }
            finally
            {
                client.Close();
                client.Dispose();
            }
        }

        public Task<T> PutRequest<T>(T request) where T : Download
        {
            ContinueRequest(request);
            return request.Task.ContinueWith(a => (T)a.Result);
        }

        private void ContinueRequest<T>(T request) where T : Download
        {
            lock (_downloadSession)
            {
                if (_downloadSession.Count < MaxConcurrentJobs) RunJobLocked(request);
                else _downloadQueue.Enqueue(request);
            }
        }

        private void RunJobLocked(Download job)
        {
            _downloadSession[++_counter] = job;
            _client.GetStream().Write(job.Request(_counter));
        }

        private void DequeueJobLocked()
        {
            if (_downloadQueue.Count == 0) return;
            if (_downloadSession.Count < MaxConcurrentJobs) RunJobLocked(_downloadQueue.Dequeue());
        }

        private void RequestStop(TcpClient client)
        {
            if (client != null && _client != client) return;
            Status = ConnectionStatus.Stopping;
            _receiveThread = null;
            _client?.Close();
            _client = null;
            OnStopped();
        }

        private void ReceiveThread()
        {
            var client = _client;
            var stream = client.GetStream();
            try
            {
                while (true)
                {
                    var header = new byte[8];
                    stream.Read(header);
                    var size = BitConverter.ToInt32(header, 4);
                    var payload = new byte[size];
                    var total = 0;
                    while (total < size)
                    {
                        total += stream.Read(payload, total, size - total);
                    }
                    Debug.WriteLine($"Len: {payload.Length}, Header: {header.Dump(4)} Data: {payload.Dump()}");
                    if (Status == ConnectionStatus.Starting)
                    {
                        OnStarted();
                    }
                    if (header[2] == 'O' && header[3] == 'W')
                    {
                        HandleOW(payload);
                    }
                    else if (header[2] == 'N' && header[3] == 'W')
                    {
                        HandleNW(payload);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                stream.Dispose();
                OnError(e);
                RequestStop(client);
            }
        }

        private void HandleOW(byte[] payload)
        {
            var seq = BitConverter.ToUInt16(payload, 16);
            lock (_downloadSession)
            {
                var handler = _downloadSession[seq];
                if (handler.Handle(payload))
                {
                    _downloadSession.Remove(seq);
                    if (handler.Continuation) ContinueRequest(handler);
                    else DequeueJobLocked();
                }
            }
        }

        private void HandleNW(byte[] payload)
        {
            if (BitConverter.ToUInt32(payload) != 0x08050003) return;
            if (payload[4] == 0) return;
            var content = new byte[payload[4]];
            Buffer.BlockCopy(payload, 5, content, 0, content.Length);
            if (content[2] != 'E') return;
            var isJpeg = content[3] == 0x08;
            var fileSize = BitConverter.ToInt32(content, 4);
            var filePath = Encoding.UTF8.GetString(content, 9, content[8]);
            OnNewFile(new DownloadIndex.Entry
            {
                Path = filePath,
                Size = fileSize,
                IsJpeg = isJpeg,
                CreatedAt = DateTime.Now,
            });
        }

        private void OnStarted()
        {
            Status = ConnectionStatus.Started;
            DispatchOnMainThread(() => Started?.Invoke(this, EventArgs.Empty));
        }

        private void OnStopped()
        {
            Status = ConnectionStatus.Stopped;
            DispatchOnMainThread(() => Stopped?.Invoke(this, EventArgs.Empty));
        }

        private void OnError(Exception e)
        {
            DispatchOnMainThread(() => Error?.Invoke(this, e));
        }

        private void OnNewFile(DownloadIndex.Entry entry)
        {
            DispatchOnMainThread(() => NewFile?.Invoke(this, entry));
        }

        private void DispatchOnMainThread(Action action)
        {
            CoreApplication.MainView.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => action());
        }
    }
}
