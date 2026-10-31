using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
#if Z47_NET
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
#endif
#if Z47_NET && Z47_FRIENDS
using Unity.Services.Friends;
using Unity.Services.Friends.Notifications;
#endif

namespace Zone47
{
    // ================================================================ tiny byte writer / reader (our own wire format)
    public class NetW
    {
        public byte[] b = new byte[512]; public int n;
        void Need(int k) { if (n + k > b.Length) Array.Resize(ref b, Math.Max(b.Length * 2, n + k)); }
        public NetW U8(int v) { Need(1); b[n++] = (byte)v; return this; }
        public NetW Bool(bool v) => U8(v ? 1 : 0);
        public NetW I16(int v) { Need(2); b[n++] = (byte)v; b[n++] = (byte)(v >> 8); return this; }
        public NetW I32(int v) { Need(4); for (int i = 0; i < 4; i++) b[n++] = (byte)(v >> (8 * i)); return this; }
        public NetW U64(ulong v) { Need(8); for (int i = 0; i < 8; i++) b[n++] = (byte)(v >> (8 * i)); return this; }
        public NetW F(float f) => I32(BitConverter.SingleToInt32Bits(f));
        public NetW V3(Vector3 v) { F(v.x); F(v.y); F(v.z); return this; }
        public NetW Str(string s)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(s ?? "");
            int len = Math.Min(bytes.Length, 200); I16(len); Need(len); Array.Copy(bytes, 0, b, n, len); n += len; return this;
        }
    }

    public class NetR
    {
        readonly byte[] b; readonly int len; int p;
        public NetR(byte[] b, int len) { this.b = b; this.len = len; }
        public bool Ok => p <= len;
        public int U8() { if (p + 1 > len) { p = len + 1; return 0; } return b[p++]; }
        public bool Bool() => U8() != 0;
        public int I16() { if (p + 2 > len) { p = len + 1; return 0; } int v = (short)(b[p] | (b[p + 1] << 8)); p += 2; return v; }
        public int I32() { if (p + 4 > len) { p = len + 1; return 0; } int v = b[p] | (b[p + 1] << 8) | (b[p + 2] << 16) | (b[p + 3] << 24); p += 4; return v; }
        public ulong U64() { if (p + 8 > len) { p = len + 1; return 0; } ulong v = 0; for (int i = 0; i < 8; i++) v |= (ulong)b[p + i] << (8 * i); p += 8; return v; }
        public float F() => BitConverter.Int32BitsToSingle(I32());
        public Vector3 V3() => new Vector3(F(), F(), F());
        public string Str()
        {
            int l = I16(); if (l < 0 || p + l > len) { p = len + 1; return ""; }
            var s = System.Text.Encoding.UTF8.GetString(b, p, l); p += l; return s;
        }
    }

    /// Online (phase 1): private rooms through Unity Relay, join by code. The host runs the bots, the score and all damage;
    /// every phone simulates its own soldier and sends where it is. No NetworkObjects: everything goes through one named message.
    public static class Net
    {
        public const int Version = 3;                          // 3: drone strikes follow players (TRACK / DROP)
        public const int MaxPlayers = 10;
        public static readonly string[] Weapons = { "m4", "ak", "awp", "glock", "carbine", "shotgun" };
        public static int WeaponIndex(string id) { int i = Array.IndexOf(Weapons, id); return i < 0 ? 0 : i; }

#if Z47_NET
        public static bool Available => true;
#else
        public static bool Available => false;
#endif
        // ------------------------------------------------------------ state
        public static bool Online { get; private set; }           // connected to a room (lobby or match)
        public static bool IsHost { get; private set; }
        public static bool IsClient => Online && !IsHost;
        public static bool Busy { get; private set; }             // connecting
        public static string Code = "";
        public static string Status = "";
        public static ulong MyId;
        public static event Action Changed;                        // room roster / settings changed
        public static event Action<string> Lost;                   // connection to the room ended (reason)

        public class Member { public ulong id; public string name = "?", flag = "tr", title = ""; public int frame, team, level = 1, netId = -1; public bool ready, lobbyReady; }
        /// Party: everyone ready (the host always is)?
        public static bool AllReady { get { foreach (var m in Room) if (m.id != 0 && !m.lobbyReady) return false; return true; } }
        public static string Waiting { get { foreach (var m in Room) if (m.id != 0 && !m.lobbyReady) return m.name; return null; } }
        public static readonly List<Member> Room = new List<Member>();
        public static Member Me { get { foreach (var m in Room) if (m.id == MyId) return m; return null; } }
        public static int RoomPreset = 3, RoomMap = 0, RoomWeather = 0, RoomDiff = 1;
        public static bool HostInMatch;                            // (host) a match is being played: new joiners are turned away

        // match
        public static bool InMatch { get; private set; }
        static int matchFlip;
        /// Every phone sees its own team as team 0 (blue): network team = local team XOR Flip.
        public static int Flip => InMatch ? matchFlip : 0;
        public static int MatchPreset = 3;
        public static int MapOverride = -1;
        public static int MatchWeather = 1;
        public static readonly int[] TeamSize = { 5, 5 };       // by network team
        public static bool Go;                                   // everyone loaded: countdown can start
        public static bool ApplyingRemote;                       // host is applying damage sent by a client
        public class Spec { public int netId, team; public bool human; public ulong owner; public string name, flag, weapon; }
        public static readonly List<Spec> Specs = new List<Spec>();
        static readonly Dictionary<int, Actor> actors = new Dictionary<int, Actor>();
        public static Actor ById(int id) => id >= 0 && actors.TryGetValue(id, out var a) && a != null ? a : null;
        public static int IdOf(Actor a) => a != null ? a.netId : -1;
        public static void Register(Actor a, int id) { a.netId = id; actors[id] = a; }

        // message types
        const byte HELLO = 1, ROOM = 2, TEAM = 3, START = 4, BYE = 5, READY = 6, GO = 7, RDY = 8,
                   STATE = 10, WORLD = 11, SHOT = 12, NADE = 13, DMG = 14, HP = 15, KILL = 16, SPAWN = 17, STRIKE = 18, END = 19, LEAVE = 20, TRACK = 21, DROP = 22;

        static string T(string tr, string en) => Settings.Turkish ? tr : en;
        static Action<string> joinDone;

        // ================================================================ connection
#if Z47_NET
        static NetworkManager nm;
        static UnityTransport transport;
        const string MsgName = "z47";

        static void EnsureManager()
        {
            if (nm != null) return;
            var go = new GameObject("Z47Net");
            UnityEngine.Object.DontDestroyOnLoad(go);
            transport = go.AddComponent<UnityTransport>();
            nm = go.AddComponent<NetworkManager>();
            if (nm.NetworkConfig == null) nm.NetworkConfig = new NetworkConfig();
            nm.NetworkConfig.NetworkTransport = transport;
            nm.NetworkConfig.EnableSceneManagement = false;
            nm.NetworkConfig.ConnectionApproval = false;
            nm.NetworkConfig.PlayerPrefab = null;
            nm.NetworkConfig.TickRate = 30;
            nm.NetworkConfig.ClientConnectionBufferTimeout = 15;
            nm.OnClientConnectedCallback += OnConnected;
            nm.OnClientDisconnectCallback += OnDisconnected;
            nm.OnTransportFailure += () => Drop(T("Bağlantı hatası", "Connection failure"));
            go.AddComponent<NetRunner>();
        }

        public static async Task SignIn()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized) await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        static RelayServerEndpoint Pick(List<RelayServerEndpoint> eps)
        {
            if (eps == null || eps.Count == 0) throw new Exception("relay: no endpoints");
            foreach (var e in eps) if (e.ConnectionType == "dtls") return e;
            return eps[0];
        }

        static string Explain(Exception e)
        {
            string m = e.Message ?? "";
            if (m.Contains("not found") || m.Contains("404") || m.ToLower().Contains("join code")) return T("Oda bulunamadı (kodu kontrol et)", "Room not found (check the code)");
            if (m.ToLower().Contains("project") || m.ToLower().Contains("cloud")) return T("Unity Cloud projesi bağlı değil", "Unity Cloud project not linked");
            if (m.ToLower().Contains("network") || m.ToLower().Contains("timeout")) return T("İnternet bağlantısı yok", "No internet connection");
            return T("Hata: ", "Error: ") + (m.Length > 90 ? m.Substring(0, 90) : m);
        }

        public static async void HostRoom(Action<string> done)
        {
            if (Busy || Online) return;
            Busy = true; Status = T("Oda kuruluyor...", "Creating room...");
            try
            {
                EnsureManager();
                await SignIn();
                var alloc = await RelayService.Instance.CreateAllocationAsync(MaxPlayers - 1);
                string code = await RelayService.Instance.GetJoinCodeAsync(alloc.AllocationId);
                var ep = Pick(alloc.ServerEndpoints);
                transport.SetHostRelayData(ep.Host, (ushort)ep.Port, alloc.AllocationIdBytes, alloc.Key, alloc.ConnectionData, ep.Secure);
                IsHost = true;                                    // before StartHost: its own connect callback must not be taken for a client's
                if (!nm.StartHost()) throw new Exception("StartHost failed");
                nm.CustomMessagingManager.RegisterNamedMessageHandler(MsgName, OnMessage);
                Online = true; MyId = NetworkManager.ServerClientId; Code = code; HostInMatch = false;
                Room.Clear(); var me = MakeMe(); me.id = MyId; me.team = 0; me.lobbyReady = true; Room.Add(me);
                PullHostSettings();
                Busy = false; Status = "";
                Changed?.Invoke();
                done?.Invoke(null);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Net] host failed: " + e);
                Busy = false; Shutdown(); Status = Explain(e);
                done?.Invoke(Status);
            }
        }

        public static async void JoinRoom(string code, Action<string> done)
        {
            if (Busy || Online) return;
            code = (code ?? "").Trim().ToUpperInvariant();
            if (code.Length < 4) { done?.Invoke(T("Kod eksik", "Code too short")); return; }
            Busy = true; Status = T("Odaya bağlanılıyor...", "Joining room...");
            try
            {
                EnsureManager();
                await SignIn();
                var ja = await RelayService.Instance.JoinAllocationAsync(code);
                var ep = Pick(ja.ServerEndpoints);
                transport.SetClientRelayData(ep.Host, (ushort)ep.Port, ja.AllocationIdBytes, ja.Key, ja.ConnectionData, ja.HostConnectionData, ep.Secure);
                if (!nm.StartClient()) throw new Exception("StartClient failed");
                nm.CustomMessagingManager.RegisterNamedMessageHandler(MsgName, OnMessage);
                IsHost = false; Code = code; joinDone = done;
                NetRunner.I.StartCoroutine(JoinTimeout());
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Net] join failed: " + e);
                Busy = false; Shutdown(); Status = Explain(e);
                done?.Invoke(Status);
            }
        }

        static IEnumerator JoinTimeout()
        {
            float t = 0f;
            while (Busy && t < 15f) { t += Time.unscaledDeltaTime; yield return null; }
            if (Busy) { Busy = false; Shutdown(); Status = T("Bağlantı zaman aşımı", "Connection timed out"); var d = joinDone; joinDone = null; d?.Invoke(Status); }
        }

        static void OnConnected(ulong id)
        {
            if (IsHost) return;                                   // host waits for HELLO instead
            if (id != nm.LocalClientId) return;
            MyId = nm.LocalClientId; Online = true;
            var me = MakeMe();
            var w = new NetW().U8(HELLO).I32(Version).Str(me.name).Str(me.flag).U8(me.frame).Str(me.title).I16(me.level);
            SendHost(w, true);
        }

        static void OnDisconnected(ulong id)
        {
            if (IsHost)
            {
                if (id == NetworkManager.ServerClientId) return;
                var m = Room.Find(x => x.id == id);
                if (m == null) return;
                Room.Remove(m);
                if (InMatch)
                {
                    // whoever left is taken over by a bot (keeps the squad card and the teams intact)
                    if (ById(m.netId) is BotController b) b.TakeOverByBot();
                    HUD.I?.ShowToast(m.name + T(" ayrıldı (bot devraldı)", " left (bot took over)"));
                }
                BroadcastRoom();
                return;
            }
            string reason = nm != null && !string.IsNullOrEmpty(nm.DisconnectReason) ? nm.DisconnectReason : T("Oda sahibiyle bağlantı koptu", "Lost connection to the host");
            if (Busy) { Busy = false; Shutdown(); Status = reason; var d = joinDone; joinDone = null; d?.Invoke(reason); return; }
            Drop(reason);
        }

        /// Leave the room (and the match). Safe to call any time.
        public static void Leave()
        {
            if (!Online && !Busy && nm == null) { EndMatchLocal(); return; }
            Busy = false;
            Shutdown();
        }

        static void Shutdown()
        {
            bool was = Online;
            Online = false; IsHost = false; HostInMatch = false; Code = "";
            Room.Clear();
            EndMatchLocal();
            if (nm != null)
            {
                try { if (nm.CustomMessagingManager != null) nm.CustomMessagingManager.UnregisterNamedMessageHandler(MsgName); } catch { }
                if (nm.IsListening) nm.Shutdown(true);
            }
            if (was) Changed?.Invoke();
        }

        static void Drop(string reason)
        {
            if (!Online && !Busy) return;
            bool inMatch = InMatch;
            Shutdown();
            Status = reason;
            Lost?.Invoke(reason);
            if (inMatch && Boot.I != null) Boot.I.ToMenu(reason);
        }

        // ------------------------------------------------------------ transport
        static void Send(ulong to, NetW w, bool reliable)
        {
            if (nm == null || !nm.IsListening || nm.CustomMessagingManager == null) return;
            var fw = new FastBufferWriter(w.n + 8, Allocator.Temp, w.n + 16);
            try
            {
                fw.WriteValueSafe(w.n);
                fw.WriteBytesSafe(w.b, w.n);
                nm.CustomMessagingManager.SendNamedMessage(MsgName, to, fw, reliable ? NetworkDelivery.ReliableFragmentedSequenced : NetworkDelivery.UnreliableSequenced);
            }
            catch (Exception e) { Debug.LogWarning("[Net] send: " + e.Message); }
            finally { fw.Dispose(); }
        }

        static void OnMessage(ulong sender, FastBufferReader r)
        {
            try
            {
                r.ReadValueSafe(out int len);
                if (len <= 0 || len > 64 * 1024) return;
                var buf = new byte[len];
                r.ReadBytesSafe(ref buf, len);
                Handle(sender, new NetR(buf, len));
            }
            catch (Exception e) { Debug.LogWarning("[Net] bad message: " + e.Message); }
        }
#else
        public static void HostRoom(Action<string> done) { done?.Invoke(T("Online paketleri kurulu değil", "Online packages not installed")); }
        public static void JoinRoom(string code, Action<string> done) { done?.Invoke(T("Online paketleri kurulu değil", "Online packages not installed")); }
        public static void Leave() { EndMatchLocal(); }
        static void Send(ulong to, NetW w, bool reliable) { }
        static void Drop(string reason) { Lost?.Invoke(reason); }
#endif
        static void SendHost(NetW w, bool reliable) => Send(0, w, reliable);
        /// Host: to every client (optionally skipping one).
        static void SendAll(NetW w, bool reliable, ulong except = ulong.MaxValue)
        {
            if (!IsHost) return;
            foreach (var m in Room) if (m.id != MyId && m.id != except) Send(m.id, w, reliable);
        }

        // ================================================================ room
        static Member MakeMe() => new Member { name = Settings.Callsign, flag = Profile.Flag, frame = Profile.SafeFrame, title = Profile.CurrentTitle, level = Progress.Level };

        /// Host: the room uses the host's own match settings (team preset, map, weather, difficulty).
        public static void PullHostSettings()
        {
            RoomPreset = Settings.TeamPreset; RoomMap = Settings.Map; RoomWeather = Settings.Weather; RoomDiff = Settings.Difficulty;
            if (IsHost) BroadcastRoom();
        }

        static void BroadcastRoom()
        {
            if (!IsHost) return;
            var w = new NetW().U8(ROOM).U8(RoomPreset).U8(RoomMap).U8(RoomWeather).U8(RoomDiff).Bool(HostInMatch).U8(Room.Count);
            foreach (var m in Room) w.U64(m.id).Str(m.name).Str(m.flag).U8(m.frame).Str(m.title).U8(m.team).I16(m.level).Bool(m.lobbyReady || m.id == 0);
            SendAll(w, true);
            Changed?.Invoke();
        }

        public static int TeamCount(int t) { int n = 0; foreach (var m in Room) if (m.team == t) n++; return n; }
        public static int PresetSize(int netTeam) { var p = MatchManager.Presets[Mathf.Clamp(RoomPreset, 0, MatchManager.Presets.Length - 1)]; return netTeam == 0 ? p.a : p.e; }

        public static bool MyReady { get { var m = Me; return m != null && (m.lobbyReady || IsHost); } }
        public static void ToggleReady()
        {
            var me = Me; if (me == null || IsHost) return;
            me.lobbyReady = !me.lobbyReady;
            SendHost(new NetW().U8(RDY).Bool(me.lobbyReady), true);
            Changed?.Invoke();
        }

        public static void SwitchTeam()
        {
            var me = Me; if (me == null) return;
            int want = 1 - me.team;
            if (IsHost) { me.team = want; BroadcastRoom(); }
            else SendHost(new NetW().U8(TEAM).U8(want), true);
        }

        // ================================================================ match start
        /// Host: build the line-up (humans + bots filling the empty slots) and start everyone's match.
        public static void StartGame()
        {
            if (!IsHost || InMatch) return;
            PullHostSettings();
            Specs.Clear();
            var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in Room) taken.Add(m.name);
            int id = 1;
            for (int t = 0; t < 2; t++)
            {
                var humans = Room.FindAll(m => m.team == t);
                int size = Mathf.Max(PresetSize(t), humans.Count);
                TeamSize[t] = size;
                foreach (var m in humans) { m.netId = id; Specs.Add(new Spec { netId = id++, team = t, human = true, owner = m.id, name = m.name, flag = m.flag, weapon = "m4" }); }
                for (int i = humans.Count; i < size; i++)
                {
                    bool tr = t == 0;
                    string wpn = t == 0 ? (i % 4 == 3 ? "awp" : "m4") : (i % 5 == 2 ? "awp" : "ak");
                    Specs.Add(new Spec { netId = id++, team = t, human = false, name = MatchManager.PickName(taken, tr), flag = Countries.RandomBot(tr), weapon = wpn });
                }
            }
            MatchWeather = Weather.Resolve();
            var w = new NetW().U8(START).U8(RoomPreset).U8(RoomMap).U8(MatchWeather).U8(RoomDiff).U8(TeamSize[0]).U8(TeamSize[1]).U8(Specs.Count);
            foreach (var s in Specs) w.I16(s.netId).U8(s.team).Bool(s.human).U64(s.owner).Str(s.name).Str(s.flag).U8(WeaponIndex(s.weapon));
            HostInMatch = true;
            foreach (var m in Room) m.ready = m.id == MyId;
            SendAll(w, true);
            BeginMatch(RoomPreset, RoomMap, MatchWeather);
        }

        static void BeginMatch(int preset, int map, int weather)
        {
            var me = Specs.Find(s => s.human && s.owner == MyId);
            matchFlip = me != null ? me.team : 0;
            MatchPreset = preset; MapOverride = map; MatchWeather = weather;
            actors.Clear(); Go = false; goSentAt = -1f; hostLoadedAt = -1f;
            InMatch = true;
            // names for the loading screen / squad card, in local terms (my team first, me excluded)
            var an = new List<string>(); var af = new List<string>(); var en = new List<string>(); var ef = new List<string>();
            foreach (var s in Specs)
            {
                if (s == me) continue;
                bool ally = (s.team ^ matchFlip) == 0;
                (ally ? an : en).Add(s.name); (ally ? af : ef).Add(s.flag);
            }
            MatchManager.AllyNames = an.ToArray(); MatchManager.AllyFlags = af.ToArray();
            MatchManager.EnemyNames = en.ToArray(); MatchManager.EnemyFlags = ef.ToArray();
            Weather.Pending = weather;
            Social.UpdatePresence();
            Boot.I?.StartMatch();
        }

        /// Local cleanup after a networked match (back to the room or to the menu).
        public static void EndMatchLocal()
        {
            InMatch = false; MapOverride = -1; Go = false; actors.Clear();
            foreach (var m in Room) m.lobbyReady = m.id == 0;
            if (IsHost) { HostInMatch = false; BroadcastRoom(); }
            Social.UpdatePresence();
        }

        static float goSentAt = -1f, hostLoadedAt = -1f;
        /// Called by the match once this phone has built the map and spawned everyone.
        public static void Ready()
        {
            if (!InMatch) return;
            if (IsHost)
            {
                hostLoadedAt = Time.unscaledTime;
                var me = Me; if (me != null) me.ready = true;
                CheckGo();
            }
            else SendHost(new NetW().U8(READY), true);
        }

        static void CheckGo()
        {
            if (!IsHost || Go || hostLoadedAt < 0f) return;
            bool all = true; foreach (var m in Room) if (!m.ready) all = false;
            if (all || Time.unscaledTime - hostLoadedAt > 25f) { Go = true; goSentAt = Time.unscaledTime; SendAll(new NetW().U8(GO), true); }
        }

        // ================================================================ gameplay sends
        static int ShotFlags(bool tracer, bool flesh) => (tracer ? 1 : 0) | (flesh ? 2 : 0);

        public static void SendShot(Actor shooter, Vector3 end, bool tracer, bool flesh)
        {
            if (!InMatch || shooter == null || shooter.remote || shooter.netId < 0) return;
            var w = new NetW().U8(SHOT).I16(shooter.netId).V3(end).U8(ShotFlags(tracer, flesh));
            if (IsHost) SendAll(w, false); else SendHost(w, false);
        }

        public static void SendNade(Actor owner, Vector3 pos, Vector3 vel)
        {
            if (!InMatch || owner == null || owner.remote || owner.netId < 0) return;
            var w = new NetW().U8(NADE).I16(owner.netId).V3(pos).V3(vel);
            if (IsHost) SendAll(w, true); else SendHost(w, true);
        }

        /// Client: ask the host to apply damage this phone's soldier did.
        public static void SendDamage(Actor victim, float amount, Actor from, Vector3 fromPos, bool head, bool expl, bool drone)
        {
            if (!IsClient || victim == null || victim.netId < 0) return;
            SendHost(new NetW().U8(DMG).I16(victim.netId).F(amount).I16(IdOf(from)).V3(fromPos).U8((head ? 1 : 0) | (expl ? 2 : 0) | (drone ? 4 : 0)), true);
        }

        /// Host: damage was applied (victim still alive or not).
        public static void SendHp(Actor victim, Actor from, Vector3 fromPos, bool head, float amount, bool expl, bool drone)
        {
            if (!IsHost || !InMatch || victim == null || victim.netId < 0) return;
            SendAll(new NetW().U8(HP).I16(victim.netId).F(victim.hp).I16(IdOf(from)).V3(fromPos).F(amount).U8((head ? 1 : 0) | (expl ? 2 : 0) | (drone ? 4 : 0)), true);
        }

        public static void SendKill(Actor victim, Actor killer, bool head, string hazard, string wid, int[] localScore, float respawnDelay)
        {
            if (!IsHost || !InMatch || victim == null) return;
            int s0 = localScore[0 ^ matchFlip], s1 = localScore[1 ^ matchFlip];          // to network team order
            SendAll(new NetW().U8(KILL).I16(victim.netId).I16(IdOf(killer)).U8((head ? 1 : 0) | (victim.lastHitExplosive ? 2 : 0) | (victim.lastHitDrone ? 4 : 0))
                .Str(hazard ?? "").Str(wid ?? "").I16(s0).I16(s1).F(respawnDelay), true);
        }

        public static void SendSpawn(Actor a, Vector3 pos, Quaternion rot)
        {
            if (!IsHost || !InMatch || a == null) return;
            SendAll(new NetW().U8(SPAWN).I16(a.netId).V3(pos).F(rot.eulerAngles.y), true);
        }

        public static void SendStrike(Actor caller, AirStrikes.Plan p)
        {
            if (!IsHost || !InMatch) return;
            SendAll(new NetW().U8(STRIKE).I16(IdOf(caller)).V3(p.target).F(p.inAng).F(p.orbitTime).F(p.dirSign).I32(p.seed), true);
        }

        /// Host: a drone strike's aim moved onto a group of soldiers.
        public static void SendTrack(int strikeId, Vector3 p)
        {
            if (!IsHost || !InMatch) return;
            SendAll(new NetW().U8(TRACK).I32(strikeId).V3(p), false);
        }

        /// Host: final target and the exact bomb spots of a strike.
        public static void SendDrop(int strikeId, Vector3 target, Vector3[] pts)
        {
            if (!IsHost || !InMatch || pts == null) return;
            var w = new NetW().U8(DROP).I32(strikeId).V3(target).U8(Mathf.Min(pts.Length, 16));
            for (int i = 0; i < pts.Length && i < 16; i++) w.V3(pts[i]);
            SendAll(w, true);
        }

        public static void SendEnd(int[] localScore)
        {
            if (!IsHost || !InMatch) return;
            SendAll(new NetW().U8(END).I16(localScore[0 ^ matchFlip]).I16(localScore[1 ^ matchFlip]), true);
        }

        // ------------------------------------------------------------ per-frame (NetRunner)
        static float stateT;
        public static void Tick()
        {
            if (!Online || !InMatch) return;
            if (IsHost && !Go) CheckGo();
            if (IsHost && Go && goSentAt > 0f && Time.unscaledTime - goSentAt > 3f) goSentAt = -1f;
            var mm = MatchManager.I; if (mm == null) return;
            stateT -= Time.unscaledDeltaTime;
            if (stateT > 0f) return;
            stateT = 1f / 15f;                                       // 15 snapshots a second
            if (IsHost)
            {
                var w = new NetW().U8(WORLD).F(mm.timeLeft);
                int countAt = w.n; w.U8(0); int count = 0;
                foreach (var kv in actors)
                {
                    var a = kv.Value; if (a == null) continue;
                    a.GetNetState(out var pos, out float yaw, out float pitch, out int flags, out int wpn);
                    w.I16(a.netId).V3(pos).I16(Mathf.RoundToInt(Mathf.Repeat(yaw, 360f) * 90f)).U8(Mathf.RoundToInt(Mathf.Clamp(pitch, -89f, 89f)) + 90).U8(flags).U8(wpn).U8(Mathf.Clamp(Mathf.CeilToInt(a.hp), 0, 255));
                    count++;
                }
                w.b[countAt] = (byte)count;
                SendAll(w, false);
            }
            else
            {
                var p = PlayerController.Local;
                if (p == null || p.netId < 0) return;
                p.GetNetState(out var pos, out float yaw, out float pitch, out int flags, out int wpn);
                SendHost(new NetW().U8(STATE).I16(p.netId).V3(pos).I16(Mathf.RoundToInt(Mathf.Repeat(yaw, 360f) * 90f)).U8(Mathf.RoundToInt(Mathf.Clamp(pitch, -89f, 89f)) + 90).U8(flags).U8(wpn), false);
            }
        }

        // ================================================================ receive
        static void Handle(ulong sender, NetR r)
        {
            int type = r.U8();
            var mm = MatchManager.I;
            switch (type)
            {
                // ---------------- room (host side)
                case HELLO:
                {
                    if (!IsHost) return;
                    int ver = r.I32(); var m = new Member { id = sender, name = r.Str(), flag = r.Str(), frame = r.U8(), title = r.Str(), level = r.I16() };
                    if (!r.Ok) return;
                    string why = ver != Version ? T("Sürüm farklı, oyunu güncelle", "Version mismatch, update the game")
                               : HostInMatch ? T("Maç devam ediyor, biraz sonra dene", "Match in progress, try again later")
                               : Room.Count >= MaxPlayers ? T("Oda dolu", "Room is full") : null;
                    if (why != null) { Send(sender, new NetW().U8(BYE).Str(why), true); NetRunner.I.StartCoroutine(KickLater(sender)); return; }
                    if (string.IsNullOrWhiteSpace(m.name)) m.name = "Asker";
                    // unique callsigns
                    string baseName = m.name; int k = 2; while (Room.Exists(x => string.Equals(x.name, m.name, StringComparison.OrdinalIgnoreCase))) m.name = baseName + k++;
                    m.team = TeamCount(0) <= TeamCount(1) ? 0 : 1;
                    Room.RemoveAll(x => x.id == sender); Room.Add(m);
                    BroadcastRoom();
                    return;
                }
                case TEAM:
                {
                    if (!IsHost || HostInMatch) return;
                    var m = Room.Find(x => x.id == sender); int t = r.U8();
                    if (m != null && (t == 0 || t == 1)) { m.team = t; BroadcastRoom(); }
                    return;
                }
                case RDY:
                {
                    if (!IsHost || HostInMatch) return;
                    var m = Room.Find(x => x.id == sender); bool on = r.Bool();
                    if (m != null) { m.lobbyReady = on; BroadcastRoom(); }
                    return;
                }
                case READY:
                {
                    if (!IsHost) return;
                    var m = Room.Find(x => x.id == sender); if (m != null) m.ready = true;
                    if (Go) Send(sender, new NetW().U8(GO), true);        // late loader: already started
                    else CheckGo();
                    return;
                }
                // ---------------- room (client side)
                case ROOM:
                {
                    if (IsHost) return;
                    RoomPreset = r.U8(); RoomMap = r.U8(); RoomWeather = r.U8(); RoomDiff = r.U8(); HostInMatch = r.Bool();
                    int n = r.U8(); var list = new List<Member>();
                    for (int i = 0; i < n; i++) list.Add(new Member { id = r.U64(), name = r.Str(), flag = r.Str(), frame = r.U8(), title = r.Str(), team = r.U8(), level = r.I16(), lobbyReady = r.Bool() });
                    if (!r.Ok) return;
                    Room.Clear(); Room.AddRange(list);
                    if (Busy) { Busy = false; Status = ""; var d = joinDone; joinDone = null; d?.Invoke(null); }
                    Changed?.Invoke();
                    return;
                }
                case BYE:
                {
                    if (IsHost) return;
                    string why = r.Str();
#if Z47_NET
                    if (Busy) { Busy = false; Shutdown(); Status = why; var d = joinDone; joinDone = null; d?.Invoke(why); return; }
#endif
                    Drop(why);
                    return;
                }
                case START:
                {
                    if (IsHost) return;
                    int preset = r.U8(), map = r.U8(), weather = r.U8(); RoomDiff = r.U8();
                    TeamSize[0] = r.U8(); TeamSize[1] = r.U8();
                    int n = r.U8(); var list = new List<Spec>();
                    for (int i = 0; i < n; i++) list.Add(new Spec { netId = r.I16(), team = r.U8(), human = r.Bool(), owner = r.U64(), name = r.Str(), flag = r.Str(), weapon = Weapons[Mathf.Clamp(r.U8(), 0, Weapons.Length - 1)] });
                    if (!r.Ok) return;
                    Specs.Clear(); Specs.AddRange(list);
                    BeginMatch(preset, map, weather);
                    return;
                }
                case GO:
                    if (!IsHost) Go = true;
                    return;
            }

            // ---------------- match
            if (!InMatch || mm == null) return;
            switch (type)
            {
                case STATE:
                {
                    if (!IsHost) return;
                    int id = r.I16(); var pos = r.V3(); float yaw = r.I16() / 90f; if (yaw < 0) yaw += 65536f / 90f; float pitch = r.U8() - 90; int flags = r.U8(), wpn = r.U8();
                    if (!r.Ok) return;
                    if (ById(id) is BotController b && b.puppet && b.owner == sender) b.SetNetState(pos, yaw, pitch, flags, wpn);
                    return;
                }
                case WORLD:
                {
                    if (IsHost) return;
                    float tl = r.F(); int n = r.U8();
                    mm.timeLeft = tl;
                    for (int i = 0; i < n; i++)
                    {
                        int id = r.I16(); var pos = r.V3(); int yr = r.I16(); float yaw = (yr < 0 ? yr + 65536 : yr) / 90f; float pitch = r.U8() - 90; int flags = r.U8(), wpn = r.U8(), hp = r.U8();
                        if (!r.Ok) return;
                        var a = ById(id);
                        if (a is BotController b && b.puppet) { b.SetNetState(pos, yaw, pitch, flags, wpn); if (b.Alive && hp > 0) b.hp = hp; }
                    }
                    return;
                }
                case SHOT:
                {
                    int id = r.I16(); var end = r.V3(); int fl = r.U8();
                    if (!r.Ok) return;
                    var a = ById(id);
                    if (IsHost)
                    {
                        if (a == null || a.netId < 0 || !OwnedBy(a, sender)) return;
                        SendAll(new NetW().U8(SHOT).I16(id).V3(end).U8(fl), false, sender);
                        mm.Noise(a.EyePos, a); BotController.Noise(a, 60f);
                    }
                    if (a is BotController b && b.puppet) b.RemoteShot(end, (fl & 1) != 0, (fl & 2) != 0);
                    return;
                }
                case NADE:
                {
                    int id = r.I16(); var pos = r.V3(); var vel = r.V3();
                    if (!r.Ok) return;
                    var a = ById(id);
                    if (a == null) return;
                    if (IsHost) { if (!OwnedBy(a, sender)) return; SendAll(new NetW().U8(NADE).I16(id).V3(pos).V3(vel), true, sender); }
                    Grenade.Spawn(a, pos, vel);
                    return;
                }
                case DMG:
                {
                    if (!IsHost) return;
                    int vid = r.I16(); float amount = r.F(); int fid = r.I16(); var fromPos = r.V3(); int fl = r.U8();
                    if (!r.Ok || amount <= 0f || amount > 400f) return;
                    var victim = ById(vid); var from = ById(fid);
                    if (victim == null || from == null || !OwnedBy(from, sender)) return;
                    ApplyingRemote = true; Actor.ExplosionDamage = (fl & 2) != 0; Actor.DroneDamage = (fl & 4) != 0;
                    try { victim.Damage(amount, from, fromPos, (fl & 1) != 0); }
                    finally { ApplyingRemote = false; Actor.ExplosionDamage = false; Actor.DroneDamage = false; }
                    return;
                }
                case HP:
                {
                    if (IsHost) return;
                    int vid = r.I16(); float hp = r.F(); int fid = r.I16(); var fromPos = r.V3(); float amount = r.F(); int fl = r.U8();
                    if (!r.Ok) return;
                    var victim = ById(vid); if (victim == null) return;
                    victim.NetHurt(hp, ById(fid), fromPos, (fl & 1) != 0, amount, (fl & 2) != 0, (fl & 4) != 0);
                    return;
                }
                case KILL:
                {
                    if (IsHost) return;
                    int vid = r.I16(), kid = r.I16(), fl = r.U8(); string hazard = r.Str(), wid = r.Str(); int s0 = r.I16(), s1 = r.I16(); float delay = r.F();
                    if (!r.Ok) return;
                    var victim = ById(vid); if (victim == null) return;
                    mm.score[0 ^ matchFlip] = s0; mm.score[1 ^ matchFlip] = s1;
                    mm.NetKill(victim, ById(kid), (fl & 1) != 0, (fl & 2) != 0, (fl & 4) != 0, hazard.Length > 0 ? hazard : null, wid.Length > 0 ? wid : null, delay);
                    return;
                }
                case SPAWN:
                {
                    if (IsHost) return;
                    int id = r.I16(); var pos = r.V3(); float yaw = r.F();
                    if (!r.Ok) return;
                    var a = ById(id); if (a == null) return;
                    a.Respawn(pos, Quaternion.Euler(0, yaw, 0)); a.spawnTime = Time.time;
                    return;
                }
                case STRIKE:
                {
                    if (IsHost) return;
                    int cid = r.I16(); var p = new AirStrikes.Plan { target = r.V3(), inAng = r.F(), orbitTime = r.F(), dirSign = r.F(), seed = r.I32() };
                    if (!r.Ok) return;
                    if (AirStrikes.I == null) AirStrikes.Begin(mm);
                    AirStrikes.I.RunPlan(ById(cid), p);
                    return;
                }
                case TRACK:
                {
                    if (IsHost) return;
                    int sid = r.I32(); var p = r.V3();
                    if (!r.Ok) return;
                    AirStrikes.I?.NetTrack(sid, p);
                    return;
                }
                case DROP:
                {
                    if (IsHost) return;
                    int sid = r.I32(); var tgt = r.V3(); int n = r.U8(); var pts = new Vector3[n];
                    for (int i = 0; i < n; i++) pts[i] = r.V3();
                    if (!r.Ok) return;
                    AirStrikes.I?.NetDrop(sid, tgt, pts);
                    return;
                }
                case END:
                {
                    if (IsHost) return;
                    int s0 = r.I16(), s1 = r.I16();
                    mm.score[0 ^ matchFlip] = s0; mm.score[1 ^ matchFlip] = s1;
                    mm.NetEnd();
                    return;
                }
            }
        }

        static bool OwnedBy(Actor a, ulong client) => a is BotController b && b.puppet && b.owner == client;

        static IEnumerator KickLater(ulong id)
        {
            yield return new WaitForSecondsRealtime(0.5f);
#if Z47_NET
            if (nm != null && nm.IsListening && IsHost) nm.DisconnectClient(id);
#endif
        }
    }

    /// Drives Net.Tick() and hosts its coroutines.
    public class NetRunner : MonoBehaviour
    {
        public static NetRunner I;
        void Awake() { I = this; }
        void Update() { Net.Tick(); }
        void OnApplicationQuit() { Net.Leave(); }
    }
}

// ================================================================ friends (was Z47Social.cs)
namespace Zone47
{
    /// What we publish as our presence "activity" (friends see it in their list).
    [Serializable, UnityEngine.Scripting.Preserve]
    public class Z47Activity
    {
        public string s = "lobby";      // lobby / party / match
        public string nm = "";          // callsign shown to friends
        public string code = "";        // party room code (friends can join)
        public string flag = "tr";
        public int lv = 1;
        public int n = 1;               // party size
        [UnityEngine.Scripting.Preserve] public Z47Activity() { }
    }

    /// Invite sent as a friend message.
    [Serializable, UnityEngine.Scripting.Preserve]
    public class Z47Invite
    {
        public string t = "inv";
        public string code = "";
        public string name = "";
        [UnityEngine.Scripting.Preserve] public Z47Invite() { }
    }

    /// Friends (Unity Friends service): your tag (Name#1234), friend list with presence, requests, party invites.
    public static class Social
    {
        public class Friend
        {
            public string id, name, tag, flag = "", code = "";
            public int level, size;
            public int status;          // 0 offline, 1 lobby, 2 in a party, 3 in a match
            public DateTime lastSeen;
        }
        public static readonly List<Friend> Friends = new List<Friend>();
        public static readonly List<Friend> Incoming = new List<Friend>();
        public static readonly List<Friend> Outgoing = new List<Friend>();
        public static event Action Changed;
        public static event Action<string, string> Invited;          // (from name, room code)

#if Z47_NET && Z47_FRIENDS
        public static bool Available => true;
#else
        public static bool Available => false;
#endif
        public static bool Ready { get; private set; }
        public static bool Starting { get; private set; }
        public static string MyTag = "";
        /// Numeric IDs without our own server: every player's hidden Unity name is "Z47_NN" (NN = 10..99, fixed per account),
        /// Unity adds "#dddd". The ID people see and type is NN followed by those digits: Z47_37#4821 -> 374821.
        /// (100 groups x 10,000 suffixes, instead of 10,000 if everyone had the same name.)
        public const string IdPrefix = "Z47_";
        public static string IdOf(string tag)
        {
            if (string.IsNullOrEmpty(tag) || !tag.StartsWith(IdPrefix, StringComparison.OrdinalIgnoreCase)) return "";
            int h = tag.IndexOf('#'); if (h < 0) return "";
            string g = tag.Substring(IdPrefix.Length, h - IdPrefix.Length), n = tag.Substring(h + 1);
            if (g.Length != 2 || n.Length == 0) return "";
            foreach (char c in g + n) if (!char.IsDigit(c)) return "";
            return g + n;
        }
        public static string MyNumber => IdOf(MyTag);

        /// The hidden Unity name for this account (stable: derived from the Unity player id, survives reinstalls).
        static string HiddenName(string playerId)
        {
            uint h = 2166136261;
            foreach (char c in playerId ?? "") { h ^= c; h *= 16777619; }
            return IdPrefix + (10 + h % 90);
        }

        /// What people type -> the full Unity tag. "374821", "37 4821", "ID 374821", "#374821" all work.
        public static string NormalizeTag(string s)
        {
            var d = new System.Text.StringBuilder();
            foreach (char c in s ?? "") if (char.IsDigit(c)) d.Append(c);
            string n = d.ToString();
            if (n.Length < 5 || n.Length > 10) return "";
            return IdPrefix + n.Substring(0, 2) + "#" + n.Substring(2);
        }

        /// Friend names: from their presence (callsign), remembered on this phone so offline friends keep their name.
        static string NameFor(string id, string tag, string fromPresence)
        {
            string key = "z_fn_" + id;
            if (!string.IsNullOrEmpty(fromPresence)) { if (PlayerPrefs.GetString(key, "") != fromPresence) PlayerPrefs.SetString(key, fromPresence); return fromPresence; }
            string saved = string.IsNullOrEmpty(id) ? "" : PlayerPrefs.GetString(key, "");
            if (saved.Length > 0) return saved;
            string num = IdOf(tag);
            if (num.Length > 0) return "ID " + num;
            int h = (tag ?? "").LastIndexOf('#');
            return h > 0 ? tag.Substring(0, h) : (string.IsNullOrEmpty(tag) ? "?" : tag);      // old-style names (before the update)
        }
        public static string Error = "";
        public static int OnlineCount { get { int n = 0; foreach (var f in Friends) if (f.status > 0) n++; return n; } }

        static string T(string tr, string en) => Settings.Turkish ? tr : en;

        /// Unity player names: no spaces; we also keep it ASCII so the tag is easy to type.
        public static string CleanName(string s)
        {
            if (string.IsNullOrEmpty(s)) s = "Asker";
            var map = new Dictionary<char, char> { { 'ç', 'c' }, { 'Ç', 'C' }, { 'ğ', 'g' }, { 'Ğ', 'G' }, { 'ı', 'i' }, { 'İ', 'I' }, { 'ö', 'o' }, { 'Ö', 'O' }, { 'ş', 's' }, { 'Ş', 'S' }, { 'ü', 'u' }, { 'Ü', 'U' } };
            var sb = new System.Text.StringBuilder();
            foreach (char c0 in s)
            {
                char c = map.TryGetValue(c0, out var m) ? m : c0;
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_' || c == '-') sb.Append(c);
            }
            string r = sb.ToString();
            if (r.Length < 3) r = (r + "Asker").Substring(0, Math.Max(3, Math.Min(8, r.Length + 5)));
            return r.Length > 20 ? r.Substring(0, 20) : r;
        }

        static void Fire() { try { Changed?.Invoke(); } catch (Exception e) { Debug.LogWarning(e); } }

        // ---------------------------------------------------------------- reflection helpers (model classes vary between SDK versions)
        static object Get(object o, string name)
        {
            if (o == null) return null;
            var t = o.GetType();
            var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (p != null) return p.GetValue(o);
            var f = t.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            return f != null ? f.GetValue(o) : null;
        }
        static T Activity<T>(object presence) where T : class, new()
        {
            if (presence == null) return null;
            try
            {
                var m = presence.GetType().GetMethod("GetActivity", BindingFlags.Public | BindingFlags.Instance);
                if (m == null || !m.IsGenericMethodDefinition) return null;
                return m.MakeGenericMethod(typeof(T)).Invoke(presence, null) as T;
            }
            catch { return null; }
        }

        static Friend ToFriend(object rel)
        {
            var member = Get(rel, "Member");
            var f = new Friend { id = Get(member, "Id") as string ?? "" };
            string full = Get(Get(member, "Profile"), "Name") as string ?? "";
            f.tag = full;
            var pres = Get(member, "Presence");
            string av = Get(pres, "Availability")?.ToString() ?? "";
            var act = Activity<Z47Activity>(pres);
            f.name = NameFor(f.id, full, act != null ? act.nm : null);
            if (Get(pres, "LastSeen") is DateTime ls) f.lastSeen = ls;
            bool online = av == "Online" || av == "Busy" || av == "Away";
            f.status = !online ? 0 : act != null && act.s == "match" ? 3 : act != null && act.s == "party" ? 2 : 1;
            if (act != null) { f.flag = act.flag ?? ""; f.level = act.lv; f.code = act.code ?? ""; f.size = act.n; }
            return f;
        }

#if Z47_NET && Z47_FRIENDS
        static IFriendsService Svc => FriendsService.Instance;

        /// Signs in (anonymous, per device), sets our player name from the callsign and starts the friends service.
        public static async void Init()
        {
            if (Ready || Starting) return;
            Starting = true; Error = "";
            try
            {
                await Net.SignIn();
                var auth = AuthenticationService.Instance;
                string want = HiddenName(auth.PlayerId);
                string cur = auth.PlayerName;
                if (string.IsNullOrEmpty(cur)) { try { cur = await auth.GetPlayerNameAsync(); } catch { cur = null; } }
                if (string.IsNullOrEmpty(cur) || !cur.StartsWith(want + "#", StringComparison.OrdinalIgnoreCase)) cur = await auth.UpdatePlayerNameAsync(want);
                MyTag = cur ?? "";
                await Svc.InitializeAsync();
                Svc.RelationshipAdded += e => Pull();
                Svc.RelationshipDeleted += e => Pull();
                Svc.PresenceUpdated += e => Pull();
                Svc.MessageReceived += OnMessage;
                Net.Changed += PresenceSoon;
                Ready = true;
                Pull();
                UpdatePresence();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Social] init failed: " + e);
                Error = e.Message != null && e.Message.ToLower().Contains("project") ? T("Unity Cloud projesi bağlı değil", "Unity Cloud project not linked") : T("Arkadaş sistemine bağlanılamadı", "Couldn't reach the friends service");
            }
            Starting = false;
            Fire();
        }

        /// Callsign changed: new player name (keeps a fresh #tag).
        public static void Rename()
        {
            if (!Ready) return;
            UpdatePresence();          // the ID never changes; friends see the new callsign through presence
            Fire();
        }

        /// Re-read the lists the SDK keeps up to date.
        public static void Pull()
        {
            if (!Ready) return;
            try
            {
                Friends.Clear(); Incoming.Clear(); Outgoing.Clear();
                foreach (var r in Svc.Friends) Friends.Add(ToFriend(r));
                foreach (var r in Svc.IncomingFriendRequests) Incoming.Add(ToFriend(r));
                foreach (var r in Svc.OutgoingFriendRequests) Outgoing.Add(ToFriend(r));
                Friends.Sort((a, b) => a.status != b.status ? (b.status > 0 ? 1 : 0) - (a.status > 0 ? 1 : 0) : string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception e) { Debug.LogWarning("[Social] pull: " + e.Message); }
            Fire();
        }

        public static async void Refresh()
        {
            if (!Ready) return;
            try { await Svc.ForceRelationshipsRefreshAsync(); } catch { }
            Pull();
        }

        public static async void AddByName(string tag, Action<string> done)
        {
            if (!Ready) { done?.Invoke(T("Arkadaş sistemi hazır değil", "Friends not ready")); return; }
            tag = NormalizeTag(tag);
            if (tag.Length == 0) { done?.Invoke(T("ID'yi yaz (kartında yazan 6 haneli sayı)", "Type the ID (the 6-digit number on their card)")); return; }
            if (string.Equals(tag, MyTag, StringComparison.OrdinalIgnoreCase)) { done?.Invoke(T("Bu senin ID'n :)", "That's your own ID :)")); return; }
            try { await Svc.AddFriendByNameAsync(tag); Pull(); done?.Invoke(null); }
            catch (Exception e) { Debug.LogWarning("[Social] add: " + e.Message); done?.Invoke(T("Bu ID'de oyuncu yok · kontrol et", "No player with that ID · check it")); }
        }

        public static async void Accept(string id) { try { await Svc.AddFriendAsync(id); } catch (Exception e) { Debug.LogWarning(e.Message); } Pull(); }
        public static async void Decline(string id) { try { await Svc.DeleteIncomingFriendRequestAsync(id); } catch (Exception e) { Debug.LogWarning(e.Message); } Pull(); }
        public static async void CancelRequest(string id) { try { await Svc.DeleteOutgoingFriendRequestAsync(id); } catch (Exception e) { Debug.LogWarning(e.Message); } Pull(); }
        public static async void Remove(string id) { try { await Svc.DeleteFriendAsync(id); } catch (Exception e) { Debug.LogWarning(e.Message); } Pull(); }

        /// Invite a friend to my party (creates the party first if I'm not in one).
        public static void Invite(string friendId, Action<string> done)
        {
            if (!Ready) { done?.Invoke(T("Arkadaş sistemi hazır değil", "Friends not ready")); return; }
            if (Net.Online) { SendInvite(friendId, done); return; }
            Net.HostRoom(err => { if (err != null) done?.Invoke(err); else SendInvite(friendId, done); });
        }

        static async void SendInvite(string friendId, Action<string> done)
        {
            try
            {
                await Svc.MessageAsync(friendId, new Z47Invite { code = Net.Code, name = Settings.Callsign });
                done?.Invoke(null);
            }
            catch (Exception e) { Debug.LogWarning("[Social] invite: " + e.Message); done?.Invoke(T("Davet gönderilemedi", "Couldn't send the invite")); }
        }

        static void OnMessage(IMessageReceivedEvent e)
        {
            try
            {
                var inv = e.GetAs<Z47Invite>();
                if (inv == null || inv.t != "inv" || string.IsNullOrEmpty(inv.code)) return;
                if (Net.Online && Net.Code == inv.code) return;           // already there
                Invited?.Invoke(string.IsNullOrEmpty(inv.name) ? "?" : inv.name, inv.code);
            }
            catch (Exception ex) { Debug.LogWarning("[Social] message: " + ex.Message); }
        }

        static float presenceAt = -1f;
        static void PresenceSoon() { presenceAt = Time.unscaledTime + 0.8f; }
        /// Called every frame by NetRunner-less pump (SocialPump): sends a pending presence update.
        public static void Tick() { if (presenceAt > 0f && Time.unscaledTime >= presenceAt) { presenceAt = -1f; UpdatePresence(); } }

        public static async void UpdatePresence()
        {
            if (!Ready) return;
            var a = new Z47Activity
            {
                s = Net.InMatch ? "match" : Net.Online ? "party" : "lobby",
                code = Net.Online && !Net.InMatch ? Net.Code : "",
                flag = Profile.Flag, lv = Progress.Level, n = Math.Max(1, Net.Room.Count), nm = Settings.Callsign,
            };
            try
            {
                // SetPresenceAsync<T>(Availability, T): the enum's namespace moved between SDK versions, so it is looked up from the method itself
                var m = Array.Find(typeof(IFriendsService).GetMethods(), x => x.Name == "SetPresenceAsync" && x.IsGenericMethodDefinition && x.GetParameters().Length == 2);
                if (m == null) return;
                var avType = m.GetParameters()[0].ParameterType;
                var task = m.MakeGenericMethod(typeof(Z47Activity)).Invoke(Svc, new object[] { Enum.Parse(avType, "Online", true), a }) as Task;
                if (task != null) await task;
            }
            catch (Exception e) { Debug.LogWarning("[Social] presence: " + e.Message); }
        }

        public static void GoOffline()
        {
            if (!Ready) return;
            try
            {
                var m = Array.Find(typeof(IFriendsService).GetMethods(), x => x.Name == "SetPresenceAvailabilityAsync" && x.GetParameters().Length == 1);
                if (m != null) m.Invoke(Svc, new object[] { Enum.Parse(m.GetParameters()[0].ParameterType, "Offline", true) });
            }
            catch { }
        }
#else
        public static void Rename() { }
        public static void Init() { Error = T("Arkadaş sistemi kurulu değil", "Friends not installed"); }
        public static void Pull() { }
        public static void Refresh() { }
        public static void Tick() { }
        public static void UpdatePresence() { }
        public static void GoOffline() { }
        public static void AddByName(string tag, Action<string> done) { done?.Invoke(T("Arkadaş sistemi kurulu değil", "Friends not installed")); }
        public static void Accept(string id) { }
        public static void Decline(string id) { }
        public static void CancelRequest(string id) { }
        public static void Remove(string id) { }
        public static void Invite(string friendId, Action<string> done) { done?.Invoke(T("Arkadaş sistemi kurulu değil", "Friends not installed")); }
#endif
    }

    /// Keeps Social ticking and says goodbye (offline presence) when the app closes / goes to background.
    public class SocialPump : MonoBehaviour
    {
        static SocialPump inst;
        public static void Ensure()
        {
            if (inst != null) return;
            var g = new GameObject("Z47Social"); DontDestroyOnLoad(g);
            inst = g.AddComponent<SocialPump>();
        }
        void Update() { Social.Tick(); }
        void OnApplicationPause(bool paused) { if (paused) Social.GoOffline(); else Social.UpdatePresence(); }
        void OnApplicationQuit() { Social.GoOffline(); }
    }
}
