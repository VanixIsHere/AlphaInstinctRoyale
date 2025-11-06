using UnityEngine;
using TMPro;
using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Networking;
using Microsoft.AspNetCore.SignalR.Client;
using System.Net.Http;
using Microsoft.AspNetCore.Http.Connections;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

public class MatchmakerClient : MonoBehaviour
{
    public enum ConnectionMode { Http, SignalR }

    [Header("UI")]
    [SerializeField] private TMP_Text responseText;

    [Header("Mode")]
    private ConnectionMode mode = ConnectionMode.SignalR;
    [SerializeField] private bool allowSignalROnThisPlatform = false; // Set true if you know this platform supports it

    [Header("Queue Settings")]
    [SerializeField] private int mmr = 1200;
    [SerializeField] private string queueType = "Casual";
    [SerializeField] private string lobbySize = "Small";

    [Header("Networking")]
    [SerializeField] private bool forceLongPolling = true;
    [SerializeField] private bool bypassTlsForDev = false; // Only for HTTPS dev endpoints with self-signed certs
    [SerializeField] private bool fallbackToHttpOnFailure = true;
    [SerializeField] private bool enableHeartbeat = true; // Dev toggle to enable/disable heartbeat
    [SerializeField, Range(3f, 5f)] private float heartbeatSeconds = 4f; // Heartbeat cadence

    // SignalR specifics
    private HubConnection _connection;
    private string _hubUrl;

    // Shared state
    private string _baseUrl;
    private string _playerId;
    private string _ticketId;
    private int _ttlSeconds;
    private bool _queued;
    private Coroutine _heartbeatCo;
    private CancellationTokenSource _cts;
    private int _id;

    void Awake()
    {
        _id = GetInstanceID();
        _playerId = "Player_" + UnityEngine.Random.Range(1000, 9999);
        _baseUrl = Env.MatchmakingUrlBase;
        _hubUrl = $"{_baseUrl}/Matchmaking"; // per guidance, Identify after connect

#if UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS
        // Default to HTTP mode on platforms where .NET SignalR client is commonly unsupported
        if (!allowSignalROnThisPlatform)
        {
            mode = ConnectionMode.Http;
        }
#endif

#if UNITY_EDITOR || UNITY_STANDALONE
        // Reduce chances of ReceiveFailure on legacy HttpWebRequest pipeline in Unity's Mono
        ServicePointManager.Expect100Continue = false;
        ServicePointManager.DefaultConnectionLimit = Math.Max(ServicePointManager.DefaultConnectionLimit, 20);
        // Ensure TLS 1.2 is enabled when using HTTPS endpoints
        try { ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; } catch { }
#endif

        Debug.Log($"[{name}#{_id}] Awake. Base='{_baseUrl}' Hub='{_hubUrl}' Mode={mode}");
    }

    // ---- Public API ----
    public void JoinMatchmaking()
    {
        if (_queued)
        {
            Debug.Log("[Matchmaker] Already queued. Ignoring join request.");
            return;
        }

        Debug.Log($"In JoinMatchmaking with mode {mode}");

        switch (mode)
        {
            case ConnectionMode.Http:
                StartCoroutine(JoinViaHttp());
                break;
            case ConnectionMode.SignalR:
                _ = JoinViaSignalRAsync();
                break;
        }
    }

    public void LeaveMatchmaking()
    {
        if (!_queued && string.IsNullOrEmpty(_ticketId))
        {
            Debug.Log("[Matchmaker] Not queued. Ignoring leave request.");
            return;
        }

        switch (mode)
        {
            case ConnectionMode.Http:
                StartCoroutine(LeaveHttp());
                break;
            case ConnectionMode.SignalR:
                _ = LeaveSignalRAsync();
                break;
    }
    }

    // ---- HTTP implementation ----
    private IEnumerator JoinViaHttp()
    {
        var url = $"{_baseUrl}/matchmaking/join";
        var join = new JoinReq { playerId = _playerId, mmr = mmr, queueType = queueType, lobbySize = lobbySize };
        var json = JsonUtility.ToJson(join);

        using var req = new UnityWebRequest(url, "POST");
        var body = System.Text.Encoding.UTF8.GetBytes(json);
        req.uploadHandler = new UploadHandlerRaw(body);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
#if UNITY_EDITOR || UNITY_STANDALONE
        req.certificateHandler = new BypassCertificate();
#endif
        Debug.Log($"[Matchmaker][HTTP] POST {url} -> {json}");
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[Matchmaker][HTTP] Join failed: {req.responseCode} {req.error}");
            SetStatusText($"Join failed: {req.error}");
            yield break;
        }

        var respText = req.downloadHandler.text;
        Debug.Log($"[Matchmaker][HTTP] Join resp: {respText}");
        var resp = JsonUtility.FromJson<JoinResp>(respText);

        if (!string.IsNullOrEmpty(resp.ticketId))
        {
            _ticketId = resp.ticketId;
            _ttlSeconds = Math.Max(1, resp.ttlSeconds);
            _queued = true;
            SetStatusText(string.IsNullOrEmpty(resp.gameUrl) ? (resp.message ?? "Queued") : "Match found!");

            StopHeartbeat();
            if (string.IsNullOrEmpty(resp.gameUrl))
                _heartbeatCo = StartCoroutine(HeartbeatHttpLoop());
            else
                OnMatchFound(resp);
        }
        else
        {
            SetStatusText(resp.message ?? "Join response missing ticketId");
        }
    }

    private IEnumerator HeartbeatHttpLoop()
    {
        while (enableHeartbeat && _queued && !string.IsNullOrEmpty(_ticketId))
        {
            var url = $"{_baseUrl}/matchmaking/heartbeat";
            var payload = new TicketReq { ticketId = _ticketId };
            var json = JsonUtility.ToJson(payload);

            using var req = new UnityWebRequest(url, "POST");
            var body = System.Text.Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
#if UNITY_EDITOR || UNITY_STANDALONE
            req.certificateHandler = new BypassCertificate();
#endif
            Debug.Log($"[Matchmaker][HTTP] Heartbeat -> {json}");
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                var text = req.downloadHandler.text?.Trim();
                if (!string.IsNullOrEmpty(text))
                {
                    // Prefer boolean acknowledgement contract: true = stay queued, false = drop
                    if (string.Equals(text, "true", StringComparison.OrdinalIgnoreCase))
                    {
                        // ok, continue
                    }
                    else if (string.Equals(text, "false", StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.LogWarning("[Matchmaker][HTTP] Heartbeat reported not queued anymore. Stopping.");
                        _queued = false;
                        SetStatusText("Queue expired");
                        yield break;
                    }
                    else
                    {
                        // Back-compat: If server returns object with TTL or match info
                        try
                        {
                            var resp = JsonUtility.FromJson<JoinResp>(text);
                            if (resp != null)
                            {
                                if (resp.ttlSeconds > 0) _ttlSeconds = resp.ttlSeconds;
                                if (!string.IsNullOrEmpty(resp.gameUrl))
                                {
                                    OnMatchFound(resp);
                                    yield break;
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
            else
            {
                Debug.LogWarning($"[Matchmaker][HTTP] Heartbeat failed: {req.responseCode} {req.error}");
            }

            var wait = Mathf.Clamp(heartbeatSeconds, 3f, 5f);
            yield return new WaitForSeconds(wait);
        }
    }

    private IEnumerator LeaveHttp()
    {
        if (string.IsNullOrEmpty(_ticketId)) yield break;

        var url = $"{_baseUrl}/matchmaking/leave";
        var payload = new TicketReq { ticketId = _ticketId };
        var json = JsonUtility.ToJson(payload);

        using var req = new UnityWebRequest(url, "POST");
        var body = System.Text.Encoding.UTF8.GetBytes(json);
        req.uploadHandler = new UploadHandlerRaw(body);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
#if UNITY_EDITOR || UNITY_STANDALONE
        req.certificateHandler = new BypassCertificate();
#endif
        Debug.Log($"[Matchmaker][HTTP] Leave -> {json}");
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"[Matchmaker][HTTP] Leave failed: {req.responseCode} {req.error}");
        }

        CleanupQueueState("Left queue");
    }

    // ---- SignalR implementation ----
    private void BuildSignalRConnection()
    {
        if (_connection != null) return;

        var builder = new HubConnectionBuilder()
            .WithUrl(_hubUrl, options =>
            {
#if UNITY_WEBGL
                // WebGL cannot use .NET WebSockets; use LongPolling
                options.Transports = HttpTransportType.LongPolling;
#endif

                if (forceLongPolling)
                {
                    options.Transports = HttpTransportType.LongPolling;
                }

#if UNITY_EDITOR || UNITY_STANDALONE
                // Only bypass certificates in Editor/Standalone for local HTTPS dev (self-signed)
                if (bypassTlsForDev && _hubUrl.StartsWith("https", System.StringComparison.OrdinalIgnoreCase))
                {
                    options.HttpMessageHandlerFactory = (msg) =>
                    {
                        try
                        {
                            if (msg is HttpClientHandler handler)
                                handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                        }
                        catch (System.PlatformNotSupportedException ex)
                        {
                            Debug.LogWarning($"[Matchmaker][SR] TLS bypass unsupported on this platform: {ex.Message}");
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogWarning($"[Matchmaker][SR] TLS bypass setup failed: {ex.Message}");
                        }
                        return msg;
                    };
                }
#endif
            })
            .AddJsonProtocol(o =>
            {
                o.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                o.PayloadSerializerOptions.PropertyNameCaseInsensitive = true;
                o.PayloadSerializerOptions.IncludeFields = true;
            })
            .WithAutomaticReconnect();

        _connection = builder.Build();
        RegisterSignalRHandlers();
    }

    private void RegisterSignalRHandlers()
    {
        _connection.Reconnecting += error => { SetStatusText("Reconnecting..."); return Task.CompletedTask; };
        _connection.Reconnected += id => { SetStatusText("Reconnected"); return Task.CompletedTask; };
        _connection.Closed += error => { SetStatusText("Disconnected"); return Task.CompletedTask; };

        _connection.On<JoinResp>("Queued", (resp) =>
        {
            if (resp == null) { Debug.LogWarning("[Matchmaker][SR] Queued payload null"); return; }
            _ticketId = resp.ticketId;
            _ttlSeconds = Math.Max(1, resp.ttlSeconds);
            _queued = true;
            SetStatusText(resp.message ?? "Queued");
            StopHeartbeat();
            _heartbeatCo = StartCoroutine(HeartbeatSignalRLoop());
        });

        _connection.On<JoinResp>("MatchFound", (resp) =>
        {
            if (resp == null) { Debug.LogWarning("[Matchmaker][SR] MatchFound payload null"); return; }
            OnMatchFound(resp);
        });
    }

    private async Task EnsureSignalRConnectedAsync(CancellationToken ct)
    {
        BuildSignalRConnection();
        if (_connection.State == HubConnectionState.Disconnected)
        {
            SetStatusText("Connecting...");
            // Use overloads without CancellationToken on platforms with limited threading support
#if UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS
            await _connection.StartAsync();
            await _connection.InvokeAsync("Identify", _playerId);
#else
            await _connection.StartAsync(ct);
            await _connection.InvokeAsync("Identify", _playerId, ct);
            // await _connection.InvokeAsync("Identify", new IdentifyPayload { PlayerId = _playerId }, ct);
#endif
            SetStatusText("Connected");
        }
    }

    private async Task JoinViaSignalRAsync()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        try
        {
            await EnsureSignalRConnectedAsync(ct);
            var join = new JoinReq { playerId = _playerId, mmr = mmr, queueType = queueType, lobbySize = lobbySize };
            var joinPayload = new JoinPayload { PlayerId = join.playerId, Mmr = join.mmr, QueueType = join.queueType, LobbySize = join.lobbySize };
            Debug.Log($"[Matchmaker][SR] JoinQueue -> {joinPayload.PlayerId}/{joinPayload.Mmr}/{joinPayload.QueueType}/{joinPayload.LobbySize}");

#if UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS
            await _connection.InvokeAsync("JoinQueue", joinPayload);
#else
            await _connection.InvokeAsync("JoinQueue", _playerId, mmr);
            return;
            await _connection.InvokeAsync("JoinQueue", joinPayload, ct);
#endif
            SetStatusText("Joining queue...");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Matchmaker][SR] Join failed: {ex}");
            SetStatusText($"Join failed: {ex.Message}");
            return;
            if (fallbackToHttpOnFailure)
            {
                Debug.LogWarning("[Matchmaker] Falling back to HTTP matchmaking.");
                mode = ConnectionMode.Http;
                StartCoroutine(JoinViaHttp());
            }
        }
    }

    private IEnumerator HeartbeatSignalRLoop()
    {
        while (enableHeartbeat && // heartbeat is enabled
                _queued && // currently in queue
                !string.IsNullOrEmpty(_ticketId) && // already received a ticket
                _connection != null && // has a SignalR connection
                _connection.State == HubConnectionState.Connected) // is connected to the SignalR hub
        {
            Task<bool> task = null;
            try
            {
                task = _connection.InvokeAsync<bool>("Heartbeat", new TicketPayload { TicketId = _ticketId });
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Matchmaker][SR] Heartbeat start error: {ex.Message}");
                // fall through to wait and try again next tick
            }

            if (task != null)
            {
                while (!task.IsCompleted)
                    yield return null;

                if (task.IsFaulted)
                {
                    var err = task.Exception?.GetBaseException();
                    Debug.LogWarning($"[Matchmaker][SR] Heartbeat failed: {err?.Message}");
                }
                else if (task.IsCanceled)
                {
                    Debug.LogWarning("[Matchmaker][SR] Heartbeat canceled");
                }
                else if (!task.Result)
                {
                    Debug.LogWarning("[Matchmaker][SR] Heartbeat reported not queued anymore. Stopping.");
                    _queued = false;
                    SetStatusText("Queue expired");
                    yield break;
                }
            }

            yield return new WaitForSeconds(heartbeatSeconds);
        }
    }

    private async Task LeaveSignalRAsync()
    {
        try
        {
            if (_connection != null && _connection.State == HubConnectionState.Connected && !string.IsNullOrEmpty(_ticketId))
            {
                
#if UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS
                await _connection.InvokeAsync("Leave", new TicketPayload { TicketId = _ticketId });
#else
                await _connection.InvokeAsync("Leave", new TicketPayload { TicketId = _ticketId });
#endif
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Matchmaker][SR] Leave failed: {ex.Message}");
        }
        finally
        {
            CleanupQueueState("Left queue");
        }
    }

    // ---- Shared helpers ----
    private void OnMatchFound(JoinResp resp)
    {
        StopHeartbeat();
        _queued = false;
        SetStatusText("Match found!");
        // TODO: consume resp.gameUrl and resp.players to transition to game
        Debug.Log($"[Matchmaker] Match URL: {resp.gameUrl} Ticket: {resp.ticketId}");
    }

    private void StopHeartbeat()
    {
        if (_heartbeatCo != null)
        {
            StopCoroutine(_heartbeatCo);
            _heartbeatCo = null;
        }
    }

    private void CleanupQueueState(string status)
    {
        StopHeartbeat();
        _queued = false;
        _ticketId = null;
        _ttlSeconds = 0;
        SetStatusText(status);
    }

    private void SetStatusText(string text)
    {
        if (responseText != null)
            responseText.text = text;
    }

    // ---- Data contracts ----
    [Serializable]
    public class JoinReq {
        public string playerId;
        public int mmr;
        public string queueType;
        public string lobbySize;
    }
    [Serializable]
    public class TicketReq {
        public string ticketId;
    }
    [Serializable]
    public class JoinResp {
        public string message;
        public string ticketId;
        public int ttlSeconds;
        public string gameUrl;
        public string[] players;
    }

    private class BypassCertificate : CertificateHandler {
        protected override bool ValidateCertificate(byte[] certificate) => true;
    }

    // Payloads for SignalR single-object invocation with camelCase property names
    private class IdentifyPayload
    {
        public string PlayerId { get; set; }
    }

    private class JoinPayload
    {
        public string PlayerId { get; set; }
        public int Mmr { get; set; }
        public string QueueType { get; set; }
        public string LobbySize { get; set; }
    }

    private class TicketPayload
    {
        public string TicketId { get; set; }
    }

    private async void OnDestroy()
    {
        try
        {
            _cts?.Cancel();
            if (_queued)
            {
                // best effort leave
                if (mode == ConnectionMode.SignalR)
                {
                    await LeaveSignalRAsync();
                }
            }
            StopHeartbeat();

            if (_connection != null)
            {
                try { await _connection.StopAsync(); } catch { }
                try { await _connection.DisposeAsync(); } catch { }
            }
        }
        catch { }
    }
}
