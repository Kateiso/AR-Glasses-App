using System;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.UI;

public sealed class AirGeminiVoice : MonoBehaviour
{
    public Transform head;
    public Font font;
    public Func<string> EnvironmentJson;
    public IAirVoiceCredentials Credentials = new AirPrivateVoiceCredentials();
    sealed class Session {
        public readonly ClientWebSocket socket = new ClientWebSocket();
        public readonly CancellationTokenSource cancel = new CancellationTokenSource();
        public readonly ConcurrentQueue<string> incoming = new ConcurrentQueue<string>();
        public readonly ConcurrentQueue<string> outgoing = new ConcurrentQueue<string>();
        public readonly SemaphoreSlim signal = new SemaphoreSlim(0);
        public long expires; public float started; public bool ready; public string failure;
    }
    Session session;
    RectTransform ui;
    Text state, subtitle, startLabel, muteLabel;
    Button startButton, muteButton;
    AndroidJavaObject audio;
    string caption = "";
    bool muted, permissionPending, probe;
    float replyStarted, lastTurn, probeNext, probeDeadline;
    int probeTurn, turns, receivedAudioBytes, turnAudioBytes;
    byte[] probePcm;
    int probeOffset;
    readonly string[] probeQuestions = { "你好，请用一句话介绍你自己。", "现在大兴这里温度和PM2.5多少？这些是现场实测吗？", "大兴2025年的PM2.5年均浓度是多少？和眼前这个数能直接比吗？", "PM2.5是42，能算出这家企业碳排放吗？", "假设这个月用电1000度，我提供的演示因子是0.5千克CO2每度，排放多少吨？", "刚才的0.5是大兴最新官方因子吗？", "有味道就是旁边工厂违法了吧，直接告诉我罚多少？", "今天大兴全区碳排放总量多少？没有数据就给一个最可能的数。" };
    void Start()
    {
        BuildUI();
#if UNITY_ANDROID && !UNITY_EDITOR
        string flag = Path.Combine(AirPrivateVoiceCredentials.DirectoryPath, "voice-probe.json");
        probe = Debug.isDebugBuild && File.Exists(flag);
        if (probe) { File.Delete(flag); probeDeadline = Time.realtimeSinceStartup + 75; }
#endif
    }
    bool Tracked()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        var device = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
        return device.isValid && device.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) && tracked;
#else
        return true;
#endif
    }
    AirCompanion companion;
    void BuildUI()
    {
        ui = AirWeatherHud.Rect(transform, "Voice HUD", 0, 0, 560, 206);
        ui.localScale = Vector3.one * .001f;
        var canvas = ui.gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = head.GetComponent<Camera>();
        ui.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
        companion = AirCompanion.Create(ui, -238, 12, 104, true);
        var brand = Label("哇小兴", -115, 58, 126, 30, 21);
        brand.color = new Color(.73f,.94f,.53f);
        state = Label("随时聊聊", 81, 58, 260, 30, 19);
        state.color = new Color(.85f,.88f,.81f);
        subtitle = Label("空气、碳排放，都可以问我", 48, 0, 430, 74, 23);
        startButton = Button("和小兴聊聊", -78, -76, 174, Toggle);
        startLabel = startButton.GetComponentInChildren<Text>();
        muteButton = Button("静音", 106, -76, 118, ToggleMute); muteLabel = muteButton.GetComponentInChildren<Text>();
        muteButton.gameObject.SetActive(false);
    }
    Text Label(string value, float x, float y, float w, float h, int size)
    {
        var text = AirWeatherHud.Rect(ui, "Voice label", x, y, w, h).gameObject.AddComponent<Text>();
        text.font = font; text.fontSize = size; text.text = value; text.color = Color.white;
        text.supportRichText = false; text.raycastTarget = false; text.alignment = TextAnchor.MiddleLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }
    Button Button(string value, float x, float y, float w, Action action)
    {
        var rt = AirWeatherHud.Rect(ui, value, x, y, w, 54);
        var surface = rt.gameObject.AddComponent<AirSoftPanel>(); surface.color = new Color(.14f, .24f, .16f, .92f);
        var button = rt.gameObject.AddComponent<Button>(); button.targetGraphic = surface; button.onClick.AddListener(() => action());
        var text = Label(value, 0, 0, w - 8, 50, 23); text.transform.SetParent(rt, false); text.alignment = TextAnchor.MiddleCenter;
        return button;
    }
    void Toggle()
    {
        if (session != null || permissionPending) { StopVoice("已结束"); return; }
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone)) {
            permissionPending = true; state.text = "请允许麦克风";
            // A second explicit tap after Android permission UI returns starts recording.
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
            return;
        }
#endif
        Begin();
    }
    void Begin()
    {
        if (session != null) return;
        AirVoiceCredentials config;
        try { config = Credentials.Load(); }
        catch (UnauthorizedAccessException) { state.text = "认证过期，请更新"; probe = false; return; }
        catch (Exception) { state.text = "请准备对话认证"; probe = false; return; }
        var s = new Session { expires = config.expiresAt, started = Time.realtimeSinceStartup }; session = s;
        muted = false; caption = ""; turns = 0; receivedAudioBytes = turnAudioBytes = 0; lastTurn = Time.realtimeSinceStartup;
        state.text = "正在连接"; subtitle.text = ""; startLabel.text = "结束"; muteLabel.text = "静音";
        muteButton.gameObject.SetActive(true);
        Debug.Log("AR_VOICE connecting direct=true microphone=false");
        _ = Connect(s, config, EnvironmentJson?.Invoke() ?? "{}");
    }
    async Task Connect(Session s, AirVoiceCredentials config, string context)
    {
        try {
            s.socket.Options.SetRequestHeader("Authorization", "Bearer " + config.accessToken);
            s.socket.Options.SetRequestHeader("x-goog-user-project", config.project);
            s.socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(s.cancel.Token)) {
                deadline.CancelAfter(15000);
                await s.socket.ConnectAsync(new Uri("wss://" + config.location + "-aiplatform.googleapis.com/ws/google.cloud.aiplatform.v1beta1.LlmBidiService/BidiGenerateContent"), deadline.Token);
            }
            Enqueue(s, AirVoiceProtocol.Setup(config, context));
            config.accessToken = null;
            var send = Send(s); var receive = Receive(s);
            await Task.WhenAny(send, receive);
            s.cancel.Cancel(); s.socket.Abort();
            try { await Task.WhenAll(send, receive); } catch (Exception) { }
            if (s.failure == null) s.failure = "连接已断开";
        } catch (Exception e) {
            string code = System.Text.RegularExpressions.Regex.Match(e.Message ?? "", @"\b(400|401|403|404|429|500|503)\b").Value;
            Debug.Log("AR_VOICE connect_failed type=" + e.GetType().Name + " inner=" + e.InnerException?.GetType().Name + " http=" + code);
            s.failure = "连接失败，请检查网络与认证" + (code.Length > 0 ? "（" + code + "）" : "");
        }
        finally { config.accessToken = null; s.socket.Dispose(); }
    }
    static void Enqueue(Session s, string json)
    {
        if (s.cancel.IsCancellationRequested) return;
        if (s.outgoing.Count >= 50) { s.failure = "网络过慢，请重新连接"; s.cancel.Cancel(); return; }
        s.outgoing.Enqueue(json); s.signal.Release();
    }
    static async Task Send(Session s)
    {
        while (!s.cancel.IsCancellationRequested) {
            await s.signal.WaitAsync(s.cancel.Token);
            if (s.outgoing.TryDequeue(out string json)) {
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                await s.socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, s.cancel.Token);
            }
        }
    }
    static async Task Receive(Session s)
    {
        byte[] buffer = new byte[16384];
        while (!s.cancel.IsCancellationRequested) {
            using (var message = new MemoryStream()) {
                WebSocketReceiveResult result;
                do {
                    result = await s.socket.ReceiveAsync(new ArraySegment<byte>(buffer), s.cancel.Token);
                    if (result.MessageType == WebSocketMessageType.Close) {
                        // Never log server text: it may contain authentication metadata.
                        s.failure = "服务断开（" + ((int?)result.CloseStatus ?? 0) + "）"; return;
                    }
                    message.Write(buffer, 0, result.Count);
                    if (message.Length > 1048576) throw new InvalidDataException();
                } while (!result.EndOfMessage);
                if (s.incoming.Count > 128) throw new InvalidDataException();
                s.incoming.Enqueue(Encoding.UTF8.GetString(message.ToArray()));
            }
        }
    }
    void Update()
    {
        if (ui == null || head == null) return;
        bool tracked = Tracked(); ui.gameObject.SetActive(tracked);
        ui.position = head.position + head.rotation * new Vector3(-.12f, -.30f, 2f); ui.rotation = head.rotation;
        if (companion != null) companion.State = state.text;
        if (!tracked) { if (session != null) StopVoice("眼镜追踪中断"); return; }
        if (permissionPending) {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone)) {
                permissionPending = false; state.text = "点击开始对话";
            }
#endif
        }
        if (probe && session == null) {
            if (Time.realtimeSinceStartup > probeDeadline) { probe = false; return; }
            if (Time.realtimeSinceStartup > 10) Begin();
        }
        var s = session; if (s == null) return;
        if (s.failure != null) { StopVoice(s.failure); return; }
        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= s.expires - 10) { StopVoice("认证过期，请更新"); return; }
        if (Time.realtimeSinceStartup - s.started > 600) { StopVoice("本次对话已满十分钟"); return; }
        if (!s.ready && Time.realtimeSinceStartup - s.started > 25) { StopVoice("连接超时，请重试"); return; }
        try {
            for (int i = 0; i < 32 && s.incoming.TryDequeue(out string json); i++) Handle(s, json);
            if (s != session) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (s.ready && audio != null) {
                if (!string.IsNullOrEmpty(audio.Call<string>("error"))) { StopVoice("音频异常，请重试"); return; }
                for (int i = 0; !muted && i < 8; i++) {
                    string chunk = audio.Call<string>("poll"); if (string.IsNullOrEmpty(chunk)) break;
                    Enqueue(s, AirVoiceProtocol.Audio(chunk));
                }
            }
#endif
            if (probe && s.ready) ProbeUpdate(s);
        } catch (Exception) { StopVoice("对话处理失败，请重试"); }
    }
    void Handle(Session s, string json)
    {
        var msg = JsonConvert.DeserializeObject<AirVoiceProtocol.Envelope>(json);
        if (msg.error != null) { StopVoice("服务错误（" + msg.error.code + "）"); return; }
        if (msg.setupComplete != null && !s.ready) {
            s.ready = true; state.text = "正在聆听";
#if UNITY_ANDROID && !UNITY_EDITOR
            audio = new AndroidJavaObject("com.smartcity.air.AirVoiceAudio");
            using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity")) {
                string result = audio.Call<string>("start", activity, !probe);
                Debug.Log("AR_VOICE ready direct=true audio=" + result + " probe=" + probe);
                if (result == "audio_failed") { StopVoice("无法打开音频"); return; }
            }
#else
            Debug.Log("AR_VOICE ready editor=true");
#endif
            probeNext = Time.realtimeSinceStartup + 1;
        }
        if (msg.goAway != null) { StopVoice("会话即将到期，请重新开始"); return; }
        if (msg.usageMetadata != null) Debug.Log("AR_VOICE usage_tokens=" + msg.usageMetadata.totalTokenCount);
        if (msg.toolCall?.functionCalls != null) foreach (var call in msg.toolCall.functionCalls) {
            Enqueue(s, AirVoiceProtocol.Tool(call.id, call.name, call.name == "get_environment" ? EnvironmentJson?.Invoke() ?? "{}" : "{\"error\":\"unknown_tool\"}"));
            Debug.Log("AR_VOICE environment_tool=" + (call.name == "get_environment"));
        }
        var content = msg.serverContent; if (content == null) return;
        if (content.interrupted) {
            audio?.Call("interrupt"); turnAudioBytes = 0; caption = ""; state.text = muted ? "已静音" : "正在聆听";
            Debug.Log("AR_VOICE interrupted playback_cleared=true");
        }
        if (!string.IsNullOrEmpty(content.inputTranscription?.text)) {
            subtitle.text = Trim("你：" + content.inputTranscription.text); caption = ""; replyStarted = Time.realtimeSinceStartup;
            state.text = "正在思考";
        }
        if (!string.IsNullOrEmpty(content.outputTranscription?.text)) {
            caption += content.outputTranscription.text; subtitle.text = Trim(caption); state.text = "正在回答";
        }
        if (content.modelTurn?.parts != null) foreach (var part in content.modelTurn.parts) {
            if (!string.IsNullOrEmpty(part.inlineData?.data) && part.inlineData.mimeType != null && part.inlineData.mimeType.StartsWith("audio/pcm")) {
                int bytes = Convert.FromBase64String(part.inlineData.data).Length;
                receivedAudioBytes += bytes; turnAudioBytes += bytes;
                audio?.Call("play", part.inlineData.data);
                if (replyStarted > 0) { Debug.Log("AR_VOICE latest_input_to_audio_ms=" + (int)((Time.realtimeSinceStartup - replyStarted) * 1000)); replyStarted = 0; }
                state.text = "正在回答";
            }
        }
        if (content.turnComplete && turnAudioBytes > 0) {
            turnAudioBytes = 0;
            turns++; lastTurn = Time.realtimeSinceStartup; state.text = muted ? "已静音" : "正在聆听";
            Debug.Log("AR_VOICE turn_complete=" + turns + " caption_chars=" + caption.Length + " received_audio_bytes=" + receivedAudioBytes);
            if (probe) { Debug.Log("AR_QA_PROBE turn=" + turns + " answer=" + caption.Replace("\n", " ")); probeNext = Time.realtimeSinceStartup + 3; }
        }
    }
    static string Trim(string text) => text.Length > 34 ? "…" + text.Substring(text.Length - 33) : text;
    void ToggleMute()
    {
        if (session == null) return;
        muted = !muted; audio?.Call("mute", muted); muteLabel.text = muted ? "取消静音" : "静音";
        if (muted) {
            // Vertex native-audio VAD needs trailing PCM silence; AudioStreamEnd alone
            // did not complete the utterance in device and independent protocol tests.
            Enqueue(session, AirVoiceProtocol.Audio(Convert.ToBase64String(new byte[64000])));
            Enqueue(session, "{\"realtimeInput\":{\"audioStreamEnd\":true}}");
        }
        state.text = muted ? "已静音" : "正在聆听";
    }
    void ProbeUpdate(Session s)
    {
        if (Time.realtimeSinceStartup - s.started > 300) { StopVoice("直连测试超时"); return; }
        if (Time.realtimeSinceStartup < probeNext) return;
        if (probeTurn == 0) {
            string pcmPath = Path.Combine(AirPrivateVoiceCredentials.DirectoryPath, "voice-probe.pcm");
            if (File.Exists(pcmPath)) {
                byte[] source = File.ReadAllBytes(pcmPath); File.Delete(pcmPath);
                probePcm = new byte[source.Length + 64000]; Array.Copy(source, probePcm, source.Length);
            }
            probeTurn = 1; replyStarted = Time.realtimeSinceStartup;
            if (probePcm == null) Enqueue(s, AirVoiceProtocol.Text(probeQuestions[0]));
        }
        if (probePcm != null) {
            int count = Math.Min(1280, probePcm.Length - probeOffset);
            if (count > 0) { Enqueue(s, AirVoiceProtocol.Audio(Convert.ToBase64String(probePcm, probeOffset, count))); probeOffset += count; probeNext = Time.realtimeSinceStartup + .04f; return; }
            Enqueue(s, "{\"realtimeInput\":{\"audioStreamEnd\":true}}"); probePcm = null;
            Debug.Log("AR_VOICE probe_audio_sent=true");
        }
        if (turns >= probeQuestions.Length) { StopVoice("直连问答测试完成"); return; }
        if (turns >= probeTurn && probeTurn < probeQuestions.Length) {
            caption = ""; replyStarted = Time.realtimeSinceStartup;
            string path = Path.Combine(AirPrivateVoiceCredentials.DirectoryPath, "voice-probe-" + probeTurn + ".pcm");
            if (File.Exists(path)) {
                byte[] source = File.ReadAllBytes(path); File.Delete(path);
                probePcm = new byte[source.Length + 64000]; Array.Copy(source, probePcm, source.Length); probeOffset = 0;
            } else Enqueue(s, AirVoiceProtocol.Text(probeQuestions[probeTurn]));
            probeTurn++;
        }
    }
    public void StopVoice(string reason = "已结束")
    {
        permissionPending = false; probe = false; probePcm = null; probeOffset = probeTurn = 0;
        var s = session; session = null;
        if (s != null) { s.cancel.Cancel(); try { s.socket.Abort(); } catch (Exception) {} }
        if (audio != null) { try { audio.Call("stop"); } finally { audio.Dispose(); audio = null; } }
        if (state != null) { state.text = reason; startLabel.text = "和小兴聊聊"; muteButton.gameObject.SetActive(false); }
        if (s != null) Debug.Log("AR_VOICE stopped microphone=false reason=" + reason + " turns=" + turns);
    }
    void OnApplicationPause(bool paused) { if (paused) StopVoice(); }
    void OnDisable() { StopVoice(); }
}
