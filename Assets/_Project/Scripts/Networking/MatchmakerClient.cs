using UnityEngine;
using TMPro;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using AIR.Shared.Contracts.Matchmaking;
using Microsoft.AspNetCore.SignalR.Client;
using System.Net.Http;
using Microsoft.AspNetCore.Http.Connections;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

public class MatchmakerClient : MonoBehaviour
{
    private static MatchmakerClient _activeQueueInstance;

    [Header("UI")]
    [SerializeField] private TMP_Text responseText;

    [Header("Platform Support")]
    [SerializeField] private bool allowSignalROnThisPlatform = false; // Set true if you know this platform supports it

    [Header("Queue Settings")]
    [SerializeField] private int mmr = 1200;
    [SerializeField] private QueueType queueType = QueueType.Casual;
    [SerializeField] private LobbySize lobbySize = LobbySize.Small;
    [SerializeField] private bool botFill = false;
    [SerializeField, Min(0)] private int botCount = 0;

    [Header("Networking")]
    [SerializeField] private bool forceLongPolling = true;
    [SerializeField] private bool bypassTlsForDev = false; // Only for HTTPS dev endpoints with self-signed certs
    [SerializeField] private bool enableHeartbeat = true; // Dev toggle to enable/disable heartbeat
    [SerializeField, Range(3f, 5f)] private float heartbeatSeconds = 4f; // Heartbeat cadence

    // SignalR specifics
    private HubConnection _connection;
    private string _hubUrl;
    private bool _isResettingConnection;

    // Shared state
    private string _baseUrl;
    private string _playerId;
    private string _ticketId;
    private int _ttlSeconds;
    private QueueLease? _currentLease;
    private QueueStateSnapshot _lastQueueState;
    private bool _queued;
    private Coroutine _heartbeatCo;
    private CancellationTokenSource _cts;
    private bool _shouldRequeueOnReconnect;
    private int _id;
    private readonly ConcurrentQueue<Action> _mainThreadActions = new ConcurrentQueue<Action>();
    private TimeSpan _queueElapsedBeforeCurrentRun = TimeSpan.Zero;
    private DateTime _queueTimerStartedUtc = DateTime.MinValue;
    private bool _queueTimerRunning;
    public DateTime QueueJoinedUtc { get; private set; } = DateTime.MinValue;
    public TimeSpan QueueTimeElapsed
    {
        get
        {
            if (QueueJoinedUtc == DateTime.MinValue)
                return TimeSpan.Zero;

            if (!_queueTimerRunning)
                return _queueElapsedBeforeCurrentRun;

            return _queueElapsedBeforeCurrentRun + (DateTime.UtcNow - _queueTimerStartedUtc);
        }
    }
    public bool IsQueued => _queued;
    public bool HasQueueSession => QueueJoinedUtc != DateTime.MinValue;
    public bool IsQueueTimerRunning => _queueTimerRunning;
    public static MatchmakerClient ActiveQueueInstance => _activeQueueInstance;

    void Awake()
    {
        _id = GetInstanceID();
        _playerId = "Player_" + UnityEngine.Random.Range(1000, 9999);
        _baseUrl = Env.MatchmakingUrlBase;
        _hubUrl = $"{_baseUrl}/Matchmaking"; // per guidance, Identify after connect

#if UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS
        if (!allowSignalROnThisPlatform)
        {
            Debug.LogWarning($"[{name}#{_id}] SignalR matchmaking disabled for this platform. Enable allowSignalROnThisPlatform once you've verified support.");
        }
#endif

#if UNITY_EDITOR || UNITY_STANDALONE
        // Reduce chances of ReceiveFailure on legacy HttpWebRequest pipeline in Unity's Mono
        ServicePointManager.Expect100Continue = false;
        ServicePointManager.DefaultConnectionLimit = Math.Max(ServicePointManager.DefaultConnectionLimit, 20);
        // Ensure TLS 1.2 is enabled when using HTTPS endpoints
        try { ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; } catch { }
#endif

        Debug.Log($"[{name}#{_id}] Awake. Base='{_baseUrl}' Hub='{_hubUrl}'");
    }

    // ---- Public API ----
    public void JoinMatchmaking()
    {
        _shouldRequeueOnReconnect = false;

        if (_queued)
        {
            Debug.Log("[Matchmaker] Already queued. Ignoring join request.");
            return;
        }

        Debug.Log("[Matchmaker] Attempting to join matchmaking via SignalR.");

#if UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS
        if (!allowSignalROnThisPlatform)
        {
            Debug.LogWarning("[Matchmaker] SignalR matchmaking is disabled for this platform.");
            SetStatusText("Matchmaking unavailable on this platform.");
            return;
        }
#endif

        _ = JoinViaSignalRAsync();
    }

    public void LeaveMatchmaking()
    {
        _shouldRequeueOnReconnect = false;

        if (!_queued && string.IsNullOrEmpty(_ticketId))
        {
            Debug.Log("[Matchmaker] Not queued. Ignoring leave request.");
            return;
        }

        _ = LeaveSignalRAsync();
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
            .WithAutomaticReconnect(new[]
            {
                TimeSpan.Zero,
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(4)
            });

        _connection = builder.Build();
        RegisterSignalRHandlers();
    }

    private void RegisterSignalRHandlers()
    {
        _connection.Reconnecting += error =>
        {
            EnqueueMainThread(() =>
            {
                Debug.Log($"[Matchmaker][State] Reconnecting ({error?.Message ?? "unknown error"})");
                HandleReconnectStarted();
            });
            return Task.CompletedTask;
        };
        _connection.Reconnected += id =>
        {
            EnqueueMainThread(() =>
            {
                Debug.Log($"[Matchmaker][State] Reconnected (ConnectionId={id ?? "null"})");
                HandleReconnected();
            });
            return Task.CompletedTask;
        };
        _connection.Closed += error =>
        {
            EnqueueMainThread(() =>
            {
                Debug.Log($"[Matchmaker][State] Connection closed ({error?.Message ?? "no reason"})");
                HandleTerminalDisconnect("Disconnected");
            });
            return Task.CompletedTask;
        };

        _connection.On<PlayerJoinResponse>("Queued", (resp) =>
        {
            EnqueueMainThread(() =>
            {
                if (resp == null)
                {
                    Debug.LogWarning("[Matchmaker][SR] Queued payload null");
                    return;
                }
                if (string.IsNullOrEmpty(resp.TicketId))
                {
                    Debug.LogWarning("[Matchmaker][SR] Queued payload missing TicketId");
                    return;
                }

                _ticketId = resp.TicketId;
                _queued = true;
                Debug.Log($"[Matchmaker] _queued flagged true, IsQueued now {IsQueued}");
                var leaseIssuedAt = resp.Lease.IssuedAtUtc;
                StartQueueTimer(leaseIssuedAt != default ? leaseIssuedAt : DateTime.UtcNow);
                _lastQueueState = resp.QueueState;
                UpdateLease(resp.Lease);
                SetStatusText(BuildQueueStatus(resp));
                EnsureHeartbeatLoopRunning();
                Debug.Log($"[Matchmaker][SR] Queued ticket {_ticketId} Lease={_currentLease?.HeartbeatIntervalSeconds ?? 0}s");
                Debug.Log($"[Matchmaker][State] Ticket issued -> {_ticketId} (Queue={queueType} Lobby={lobbySize})");
                Debug.Log("[Matchmaker][State] Heartbeat loop started");
            });
        });

        _connection.On<MatchOffer>("MatchFound", (offer) =>
        {
            EnqueueMainThread(() =>
            {
                if (offer == null)
                {
                    Debug.LogWarning("[Matchmaker][SR] MatchFound payload null");
                    return;
                }
                // OnMatchFound(offer);
            });
        });
    }

    private async Task EnsureSignalRConnectedAsync(CancellationToken ct)
    {
        while (_isResettingConnection)
            await Task.Yield();

        BuildSignalRConnection();

        if (_connection != null &&
            _connection.State != HubConnectionState.Connected &&
            _connection.State != HubConnectionState.Disconnected)
        {
            Debug.Log($"[Matchmaker][State] Resetting stale connection in state {_connection.State} before connect");
            await ResetConnectionAsync();
            while (_isResettingConnection)
                await Task.Yield();

            BuildSignalRConnection();
        }

        if (_connection == null)
            BuildSignalRConnection();

        if (_connection.State == HubConnectionState.Disconnected)
        {
            SetStatusText("Connecting...");
            Debug.Log("[Matchmaker][State] Connecting to matchmaking hub...");
            // Use overloads without CancellationToken on platforms with limited threading support
#if UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS
            await _connection.StartAsync();
#else
            await _connection.StartAsync(ct);
#endif
            Debug.Log($"[Matchmaker][State] Connected transport for {_playerId} (ConnectionId={_connection.ConnectionId ?? "null"})");
            SetStatusText("Connected");
        }
    }

    private async Task IdentifyCurrentConnectionAsync(CancellationToken ct)
    {
        if (_connection == null || _connection.State != HubConnectionState.Connected)
            return;

#if UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS
        await _connection.InvokeAsync("Identify", _playerId);
#else
        await _connection.InvokeAsync("Identify", _playerId, ct);
#endif
        Debug.Log($"[Matchmaker][State] Identified {_playerId} on ConnectionId={_connection.ConnectionId ?? "null"}");
    }

    private async Task JoinViaSignalRAsync()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        try
        {
            await EnsureSignalRConnectedAsync(ct);
            await IdentifyCurrentConnectionAsync(ct);
            var joinRequest = new PlayerJoinRequest
            {
                PlayerId = _playerId,
                MMR = mmr,
                QueueType = queueType,
                LobbySize = lobbySize,
                BotFill = botFill,
                BotCount = botCount > 0 ? botCount : null
            };
            var botCountText = joinRequest.BotCount?.ToString() ?? "null";
            Debug.Log($"[Matchmaker][SR] JoinQueue -> {joinRequest.PlayerId}/{joinRequest.MMR}/{joinRequest.QueueType}/{joinRequest.LobbySize}/BotFill={joinRequest.BotFill}/BotCount={botCountText}");

#if UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS
            await _connection.InvokeAsync("JoinQueue", joinRequest);
#else
            await _connection.InvokeAsync("JoinQueue", joinRequest, ct);
#endif
            Debug.Log($"[Matchmaker][State] Queue joined for {_playerId} -> {joinRequest.QueueType}/{joinRequest.LobbySize}");
            SetStatusText("Queue joined.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Matchmaker][SR] Join failed: {ex}");
            SetStatusText($"Join failed: {ex.Message}");
        }
    }

    private IEnumerator HeartbeatSignalRLoop()
    {
        Debug.Log($"Heartbeat loop activated -> Ticket={_ticketId ?? "none"} Queued={_queued} ConnectionState={_connection?.State}");
        var iteration = 0;
        Task<HeartbeatAck> task;
        while (enableHeartbeat &&
               _queued &&
               !string.IsNullOrEmpty(_ticketId) &&
               _connection != null &&
               _connection.State == HubConnectionState.Connected)
        {
            if (QueueJoinedUtc == DateTime.MinValue)
                StartQueueTimer(DateTime.UtcNow);
            else
                ResumeQueueTimer();

            iteration++;
            Debug.Log($"[Matchmaker][State] Heartbeat iteration {iteration} -> Ticket={_ticketId} ConnectionState={_connection.State}");

            task = null;
            try
            {
                var ping = new HeartbeatPing
                {
                    TicketId = _ticketId,
                    ClientTimestampUtc = DateTime.UtcNow
                };
                task = _connection.InvokeAsync<HeartbeatAck>("Heartbeat", ping);
                Debug.Log($"Sending another heartbeat with ping details: {ping.TicketId}, {ping.ClientTimestampUtc}");
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
                    if (ShouldInvalidateQueueOnHeartbeatFailure(err))
                    {
                        Debug.LogWarning("[Matchmaker][State] Discarding local queue ticket after heartbeat failure.");
                        _heartbeatCo = null;
                        CleanupQueueState("Queue lost");
                        yield break;
                    }
                }
                else if (task.IsCanceled)
                {
                    Debug.LogWarning("[Matchmaker][SR] Heartbeat canceled");
                }
                else
                {
                    var ack = task.Result;
                    if (ack == null)
                    {
                        Debug.LogWarning("[Matchmaker][SR] Heartbeat ack null");
                        _heartbeatCo = null;
                        CleanupQueueState("Queue lost");
                        yield break;
                    }
                    else
                    {
                        if (ack.Lease.Equals(default(QueueLease)))
                        {
                            Debug.LogWarning("[Matchmaker][SR] Heartbeat ack missing lease. Discarding local ticket.");
                            _heartbeatCo = null;
                            CleanupQueueState("Queue lost");
                            yield break;
                        }

                        UpdateLease(ack.Lease);
                        Debug.Log($"[Matchmaker][State] Heartbeat ack -> TTL {_ttlSeconds}s Ticket={_ticketId}");
                    }
                }
            }

            if (HasLeaseExpired())
            {
                Debug.LogWarning("[Matchmaker][SR] Lease expired while waiting for heartbeat.");
                Debug.Log("[Matchmaker][State] Lease expired while waiting for heartbeat response");
                _heartbeatCo = null;
                CleanupQueueState("Queue expired");
                yield break;
            }

            var waitForTime = GetHeartbeatIntervalSeconds();
            Debug.Log($"[Matchmaker][State] Waiting {waitForTime}s before next heartbeat (unscaled, timeScale={Time.timeScale})");
            yield return new WaitForSecondsRealtime(waitForTime);
        }
        var exitReason = "unknown";
        if (!enableHeartbeat)
            exitReason = "Heartbeat toggle disabled";
        else if (!_queued)
            exitReason = "Queue state cleared (match found or manually left)";
        else if (string.IsNullOrEmpty(_ticketId))
            exitReason = "TicketId missing";
        else if (_connection == null)
            exitReason = "Connection disposed";
        else if (_connection.State != HubConnectionState.Connected)
            exitReason = $"Connection state {_connection.State}";

        Debug.Log($"[Matchmaker][State] Heartbeat loop exiting -> Reason={exitReason} Queued={_queued} TicketSet={!string.IsNullOrEmpty(_ticketId)} ConnectionState={_connection?.State}");
        PauseQueueTimer();
        _heartbeatCo = null;
    }

    private async Task LeaveSignalRAsync()
    {
        try
        {
            if (_connection != null && _connection.State == HubConnectionState.Connected && !string.IsNullOrEmpty(_ticketId))
            {
                var leaveRequest = new LeaveQueueRequest
                {
                    TicketId = _ticketId,
                    PlayerId = _playerId
                };
                LeaveQueueResult result = null;
                Debug.Log($"[Matchmaker][State] LeaveQueue -> Ticket={leaveRequest.TicketId} Player={leaveRequest.PlayerId}");
#if UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS
                result = await _connection.InvokeAsync<LeaveQueueResult>("Leave", leaveRequest);
#else
                result = await _connection.InvokeAsync<LeaveQueueResult>("Leave", leaveRequest);
#endif
                if (result != null && !result.Success)
                {
                    Debug.LogWarning($"[Matchmaker][SR] Leave reported failure: {result.FailureReason} {result.Message}");
                }
                else
                {
                    Debug.Log("[Matchmaker][State] Leave acknowledged by server");
                }
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
    private void OnMatchFound(MatchOffer offer)
    {
        StopHeartbeat();
        _queued = false;
        _currentLease = null;
        PauseQueueTimer();

        if (!string.IsNullOrEmpty(offer?.TicketId))
            _ticketId = offer.TicketId;

        var roster = offer?.Roster;
        var connection = offer?.Connection;
        var status = roster != null
            ? $"Match found ({roster.QueueType} • {roster.LobbySize})"
            : "Match found!";
        SetStatusText(status);

        var host = connection?.Host ?? "unknown";
        var port = connection?.Port ?? 0;
        var transport = connection?.Transport ?? "unknown";
        var region = connection?.Region ?? "unknown";
        Debug.Log($"[Matchmaker] Match offer Ticket={offer?.TicketId} Session={offer?.SessionId} Host={host}:{port} Transport={transport} Region={region}");
        Debug.Log($"[Matchmaker][State] Match found -> Ticket={offer?.TicketId} Session={offer?.SessionId} {region}/{transport}");
    }

    private void StopHeartbeat()
    {
        if (_heartbeatCo != null)
        {
            StopCoroutine(_heartbeatCo);
            _heartbeatCo = null;
            PauseQueueTimer();
            Debug.Log("[Matchmaker][State] Heartbeat loop stopped");
        }
    }

    private void CleanupQueueState(string status, bool preserveRequeueIntent = false)
    {
        var previousTicket = _ticketId;
        var previousTtl = _ttlSeconds;
        StopHeartbeat();
        _queued = false;
        _ticketId = null;
        _ttlSeconds = 0;
        _currentLease = null;
        _lastQueueState = null;
        _queueElapsedBeforeCurrentRun = TimeSpan.Zero;
        _queueTimerStartedUtc = DateTime.MinValue;
        _queueTimerRunning = false;
        QueueJoinedUtc = DateTime.MinValue;
        if (!preserveRequeueIntent)
            _shouldRequeueOnReconnect = false;
        if (_activeQueueInstance == this)
            _activeQueueInstance = null;
        SetStatusText(status);
        Debug.Log($"[Matchmaker][State] Queue state reset -> {status} (Ticket={previousTicket ?? "none"} TTL={previousTtl}s)");
    }

    private void HandleReconnectStarted()
    {
        var hadQueueSession = _queued || !string.IsNullOrEmpty(_ticketId) || HasQueueSession;
        if (!hadQueueSession)
        {
            SetStatusText("Reconnecting...");
            return;
        }

        _shouldRequeueOnReconnect = true;
        CleanupQueueState("Reconnecting...", preserveRequeueIntent: true);
    }

    private async void HandleReconnected()
    {
        try
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            await IdentifyCurrentConnectionAsync(ct);

            if (_shouldRequeueOnReconnect)
            {
                _shouldRequeueOnReconnect = false;
                SetStatusText("Reconnected. Requeueing...");
                await JoinViaSignalRAsync();
                return;
            }

            SetStatusText("Reconnected");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Matchmaker][State] Reconnect recovery failed: {ex.Message}");
            HandleTerminalDisconnect("Disconnected");
        }
    }

    private void HandleTerminalDisconnect(string status)
    {
        var hadQueueSession = _queued || !string.IsNullOrEmpty(_ticketId) || HasQueueSession;
        _cts?.Cancel();
        _shouldRequeueOnReconnect = false;

        if (hadQueueSession)
            CleanupQueueState(status);
        else
            SetStatusText(status);

        _ = ResetConnectionAsync();
    }

    private async Task ResetConnectionAsync()
    {
        if (_isResettingConnection)
            return;

        var connectionToDispose = _connection;
        if (connectionToDispose == null)
            return;

        _isResettingConnection = true;
        _connection = null;

        try
        {
            try
            {
                await connectionToDispose.StopAsync();
            }
            catch { }

            try
            {
                await connectionToDispose.DisposeAsync();
            }
            catch { }
        }
        finally
        {
            _isResettingConnection = false;
        }
    }

    private void StartQueueTimer(DateTime joinedUtc)
    {
        _activeQueueInstance = this;
        QueueJoinedUtc = joinedUtc;
        _queueElapsedBeforeCurrentRun = TimeSpan.Zero;
        _queueTimerStartedUtc = joinedUtc;
        _queueTimerRunning = true;
    }

    private void ResumeQueueTimer()
    {
        if (!_queued || QueueJoinedUtc == DateTime.MinValue || _queueTimerRunning)
            return;

        _activeQueueInstance = this;
        _queueTimerStartedUtc = DateTime.UtcNow;
        _queueTimerRunning = true;
    }

    private void PauseQueueTimer()
    {
        if (!_queueTimerRunning)
            return;

        _queueElapsedBeforeCurrentRun += DateTime.UtcNow - _queueTimerStartedUtc;
        _queueTimerStartedUtc = DateTime.MinValue;
        _queueTimerRunning = false;
    }

    private void EnsureHeartbeatLoopRunning()
    {
        if (!_queued || string.IsNullOrEmpty(_ticketId) || !enableHeartbeat)
            return;

        if (_connection == null || _connection.State != HubConnectionState.Connected)
            return;

        if (_heartbeatCo != null)
            return;

        ResumeQueueTimer();
        _heartbeatCo = StartCoroutine(HeartbeatSignalRLoop());
    }

    private void UpdateLease(QueueLease? lease)
    {
        if (!lease.HasValue)
        {
            Debug.Log("[Matchmaker][State] Lease cleared");
            return;
        }

        _currentLease = lease;
        var now = DateTime.UtcNow;
        var expires = lease.Value.ExpiresAtUtc;
        var secondsRemaining = Mathf.Max(0f, (float)(expires - now).TotalSeconds);
        _ttlSeconds = Mathf.CeilToInt(secondsRemaining);
        Debug.Log($"[Matchmaker][State] Lease updated -> TTL={_ttlSeconds}s Interval={lease.Value.HeartbeatIntervalSeconds}s Grace={lease.Value.HeartbeatGraceSeconds}s");
    }

    private float GetHeartbeatIntervalSeconds()
    {
        if (_currentLease.HasValue && _currentLease.Value.HeartbeatIntervalSeconds > 0)
        {
            return Mathf.Max(1f, _currentLease.Value.HeartbeatIntervalSeconds);
        }
        return Mathf.Clamp(heartbeatSeconds, 1f, 10f);
    }

    private bool HasLeaseExpired()
    {
        if (!_currentLease.HasValue)
            return false;

        var lease = _currentLease.Value;
        var grace = Mathf.Max(0, lease.HeartbeatGraceSeconds);
        return DateTime.UtcNow > lease.ExpiresAtUtc.AddSeconds(grace);
    }

    private static bool ShouldInvalidateQueueOnHeartbeatFailure(Exception err)
    {
        if (err == null)
            return false;

        var message = err.Message ?? string.Empty;
        if (message.IndexOf("ticket", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (message.IndexOf("queue", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (message.IndexOf("lease", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (message.IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (message.IndexOf("expired", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }

    private string BuildQueueStatus(PlayerJoinResponse resp)
    {
        if (resp?.QueueState == null)
            return "Queued";

        var state = resp.QueueState;
        var waitLabel = Convert.ToString(state.ExpectedWaitSeconds);
        if (string.IsNullOrEmpty(waitLabel))
            waitLabel = "?";
        var activeLabel = Convert.ToString(state.ActiveTickets);
        if (string.IsNullOrEmpty(activeLabel))
            activeLabel = "?";

        return $"Queued ({state.QueueType} • target {state.LobbyTargetSize} • wait {waitLabel}s • {activeLabel} in queue)";
    }

    private void SetStatusText(string text)
    {
        if (responseText != null)
            responseText.text = text;
    }

    private void Update()
    {
        while (_mainThreadActions.TryDequeue(out var action))
        {
            try
            {
                action?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
    }

    private void EnqueueMainThread(Action action)
    {
        if (action == null)
            return;
        _mainThreadActions.Enqueue(action);
    }

    private async void OnDestroy()
    {
        try
        {
            _cts?.Cancel();
            if (_queued)
            {
                // best effort leave
                await LeaveSignalRAsync();
            }
            StopHeartbeat();

            if (_connection != null)
            {
                try { await _connection.StopAsync(); } catch { }
                try { await _connection.DisposeAsync(); } catch { }
            }
        }
        catch { }
        finally
        {
            if (_activeQueueInstance == this)
                _activeQueueInstance = null;
        }
    }
}
