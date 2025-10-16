using UnityEngine;
using TMPro;
using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Networking;
#if ENABLE_SIGNALR
using Microsoft.AspNetCore.SignalR.Client;
using System.Net.Http;
#endif

public class MatchmakerClient : MonoBehaviour
{
    public enum ConnectionMode { Http, SignalR }

    [Header("UI")]
    [SerializeField] private TMP_Text responseText;

    [Header("Mode")]
    [SerializeField] private ConnectionMode mode = ConnectionMode.Http; // default to HTTP; enable SignalR once package is installed

    [Header("Queue Settings")]
    [SerializeField] private int mmr = 1200;
    [SerializeField] private string queueType = "Casual";
    [SerializeField] private string lobbySize = "Small";

    // SignalR specifics
#if ENABLE_SIGNALR
    private HubConnection _connection;
#endif
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
        _hubUrl = $"{_baseUrl}/hubs/matchmaking"; // per guidance, Identify after connect
        Debug.Log($"[{name}#{_id}] Awake. Base='{_baseUrl}' Hub='{_hubUrl}'");
    }

    // ---- Public API ----
    public void JoinMatchmaking()
    {
        if (_queued)
        {
            Debug.Log("[Matchmaker] Already queued. Ignoring join request.");
            return;
        }

        switch (mode)
        {
            case ConnectionMode.Http:
                StartCoroutine(JoinViaHttp());
                break;
            case ConnectionMode.SignalR:
#if ENABLE_SIGNALR
                _ = JoinViaSignalRAsync();
#else
                Debug.LogError("[Matchmaker] SignalR mode selected but ENABLE_SIGNALR not defined or package not installed.");
#endif
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
#if ENABLE_SIGNALR
                _ = LeaveSignalRAsync();
#else
                Debug.LogError("[Matchmaker] SignalR mode selected but ENABLE_SIGNALR not defined or package not installed.");
#endif
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
        while (_queued && !string.IsNullOrEmpty(_ticketId))
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
                var text = req.downloadHandler.text;
                if (!string.IsNullOrEmpty(text))
                {
                    // If server returns updated TTL or matched payload, try parse
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
            }
            else
            {
                Debug.LogWarning($"[Matchmaker][HTTP] Heartbeat failed: {req.responseCode} {req.error}");
            }

            var wait = Mathf.Max(1f, _ttlSeconds * 0.5f);
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
#if ENABLE_SIGNALR
    private void BuildSignalRConnection()
    {
        if (_connection != null) return;

        var builder = new HubConnectionBuilder()
            .WithUrl(_hubUrl, options =>
            {
#if UNITY_EDITOR || UNITY_STANDALONE
                options.HttpMessageHandlerFactory = (msg) =>
                {
                    if (msg is HttpClientHandler handler)
                        handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                    return msg;
                };
#endif
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
            await _connection.StartAsync(ct);
            await _connection.InvokeAsync("Identify", _playerId, ct);
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
            Debug.Log($"[Matchmaker][SR] JoinQueue -> {join.playerId}/{join.mmr}/{join.queueType}/{join.lobbySize}");
            await _connection.InvokeAsync("JoinQueue", join, ct);
            SetStatusText("Joining queue...");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Matchmaker][SR] Join failed: {ex.Message}");
            SetStatusText($"Join failed: {ex.Message}");
        }
    }

    private IEnumerator HeartbeatSignalRLoop()
    {
        while (_queued && !string.IsNullOrEmpty(_ticketId) && _connection != null && _connection.State == HubConnectionState.Connected)
        {
            try
            {
                _ = _connection.InvokeAsync("Heartbeat", _ticketId);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Matchmaker][SR] Heartbeat error: {ex.Message}");
            }
            var wait = Mathf.Max(1f, _ttlSeconds * 0.5f);
            yield return new WaitForSeconds(wait);
        }
    }

    private async Task LeaveSignalRAsync()
    {
        try
        {
            if (_connection != null && _connection.State == HubConnectionState.Connected && !string.IsNullOrEmpty(_ticketId))
            {
                await _connection.InvokeAsync("Leave", _ticketId);
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
#else
    // Stubs when SignalR is not available to keep compilation happy
    private Task JoinViaSignalRAsync() { Debug.LogError("[Matchmaker] SignalR not enabled. Install package and define ENABLE_SIGNALR."); return Task.CompletedTask; }
    private Task LeaveSignalRAsync() { Debug.LogError("[Matchmaker] SignalR not enabled. Install package and define ENABLE_SIGNALR."); return Task.CompletedTask; }
#endif

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
    public class JoinReq { public string playerId; public int mmr; public string queueType; public string lobbySize; }
    [Serializable]
    public class TicketReq { public string ticketId; }
    [Serializable]
    public class JoinResp { public string message; public string ticketId; public int ttlSeconds; public string gameUrl; public string[] players; }

    private class BypassCertificate : CertificateHandler { protected override bool ValidateCertificate(byte[] certificate) => true; }

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
#if ENABLE_SIGNALR
                    await LeaveSignalRAsync();
#endif
                }
            }
            StopHeartbeat();

#if ENABLE_SIGNALR
            if (_connection != null)
            {
                try { await _connection.StopAsync(); } catch { }
                try { await _connection.DisposeAsync(); } catch { }
            }
#endif
        }
        catch { }
    }
}
