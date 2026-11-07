// WebSocket-Verbindung zum Node-Server (gleiches Protokoll wie der Browser-Client)
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ZA
{
    public class Net
    {
        public enum State { Disconnected, Connecting, Connected }
        public State state = State.Disconnected;
        public string lastError = "";
        readonly ConcurrentQueue<Dictionary<string, object>> inbox = new ConcurrentQueue<Dictionary<string, object>>();
        readonly ConcurrentQueue<string> outbox = new ConcurrentQueue<string>();
        ClientWebSocket ws;
        CancellationTokenSource cts;
        string url;

        public void Connect(string serverUrl)
        {
            Close();
            url = serverUrl.TrimEnd('/');
            if (!url.StartsWith("http")) url = "http://" + url;
            var wsUrl = url.Replace("https://", "wss://").Replace("http://", "ws://") + "/ws";
            state = State.Connecting; lastError = "";
            cts = new CancellationTokenSource();
            ws = new ClientWebSocket();
            Task.Run(() => Run(wsUrl, cts.Token));
        }

        async Task Run(string wsUrl, CancellationToken ct)
        {
            try
            {
                await ws.ConnectAsync(new Uri(wsUrl), ct);
                state = State.Connected;
                _ = Task.Run(() => SendLoop(ct));
                var buf = new byte[1 << 16];
                var sb = new StringBuilder();
                while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
                {
                    var res = await ws.ReceiveAsync(new ArraySegment<byte>(buf), ct);
                    if (res.MessageType == WebSocketMessageType.Close) break;
                    sb.Append(Encoding.UTF8.GetString(buf, 0, res.Count));
                    if (!res.EndOfMessage) continue;
                    var text = sb.ToString(); sb.Clear();
                    var msg = J.Obj(MiniJson.Parse(text));
                    if (msg != null) inbox.Enqueue(msg);
                }
            }
            catch (Exception e) { if (!ct.IsCancellationRequested) lastError = e.Message; }
            state = State.Disconnected;
        }

        async Task SendLoop(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
                {
                    if (outbox.TryDequeue(out var s))
                        await ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(s)), WebSocketMessageType.Text, true, ct);
                    else await Task.Delay(5, ct);
                }
            }
            catch (Exception) { /* Verbindung weg */ }
        }

        public void Send(Dictionary<string, object> msg)
        {
            if (state != State.Connected) return;
            outbox.Enqueue(MiniJson.Serialize(msg));
        }
        public void Send(params object[] kv)
        {
            var d = new Dictionary<string, object>();
            for (int i = 0; i + 1 < kv.Length; i += 2) d[kv[i].ToString()] = kv[i + 1];
            Send(d);
        }

        public bool TryReceive(out Dictionary<string, object> msg) => inbox.TryDequeue(out msg);

        public void Close()
        {
            try { cts?.Cancel(); ws?.Dispose(); } catch (Exception) { }
            ws = null; cts = null; state = State.Disconnected;
        }

        /// <summary>LAN-Adressen vom Server holen (für "Freunde einladen")</summary>
        public static async Task<List<string>> FetchInfo(string serverUrl)
        {
            var list = new List<string>();
            try
            {
                using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                var u = serverUrl.TrimEnd('/'); if (!u.StartsWith("http")) u = "http://" + u;
                var txt = await http.GetStringAsync(u + "/info");
                foreach (var x in J.A(MiniJson.Parse(txt), "urls")) list.Add(x.ToString());
            }
            catch (Exception) { }
            return list;
        }
    }
}
