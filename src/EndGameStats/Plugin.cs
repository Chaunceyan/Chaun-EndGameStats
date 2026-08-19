using BepInEx;
using BepInEx.Configuration;
using EndGameStats.Core;
using HarmonyLib;
using System.Collections;
using System.Globalization;
using UnityEngine;

namespace EndGameStats;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "chaun.repo.endgamestats";
    public const string PluginName = "End Game Stats";
    public const string PluginVersion = "0.4.0";

    internal static Plugin Instance { get; private set; } = null!;
    internal RunStatsBoard Board { get; private set; } = new();

    private readonly Dictionary<int, ValuableSample> _valuables = new();
    private readonly HashSet<int> _creditedExtractions = new();
    private readonly HashSet<string> _deadPlayers = new(StringComparer.Ordinal);
    private readonly Dictionary<int, EnemyAttribution> _enemyAttribution = new();
    private readonly HashSet<int> _creditedEnemyKills = new();
    private ConfigEntry<KeyboardShortcut> _toggleKey = null!;
    private ConfigEntry<float> _cartSampleInterval = null!;
    private ConfigEntry<float> _releaseAttributionSeconds = null!;
    private ConfigEntry<float> _enemyKillAttributionSeconds = null!;
    private ConfigEntry<string> _language = null!;
    private bool _visible;
    private bool _wasInLevel;
    private float _nextSample;
    private Rect _window = new(40, 60, 1110, 420);
    private Texture2D _panelTexture = null!;
    private Texture2D _screenTexture = null!;
    private Texture2D _borderTexture = null!;
    private Texture2D _rowTexture = null!;
    private Texture2D _scanlineTexture = null!;
    private GUIStyle _windowStyle = null!;
    private Font? _terminalFont;

    private void Awake()
    {
        Instance = this;
        _toggleKey = Config.Bind("Display", "ToggleKey", new KeyboardShortcut(KeyCode.F3),
            "Open or close the live stats board.");
        _language = Config.Bind("Display", "Language", "Auto",
            "UI language: Auto, English, or SimplifiedChinese.");
        _cartSampleInterval = Config.Bind("Tracking", "CartSampleIntervalSeconds", 1f,
            "Low-frequency fallback interval for valuables transported inside carts.");
        _releaseAttributionSeconds = Config.Bind("Tracking", "DamageAttributionSeconds", 3f,
            "Attribute damage to the most recent carrier for this long after release.");
        _enemyKillAttributionSeconds = Config.Bind("Tracking", "EnemyKillAttributionSeconds", 10f,
            "Credit an enemy kill to its most recent player interaction within this many seconds.");

        new Harmony(PluginGuid).PatchAll();
        Logger.LogInfo($"{PluginName} {PluginVersion} loaded");
    }

    private void Update()
    {
        if (_toggleKey.Value.IsDown())
            _visible = !_visible;

        var inLevel = SafeRunIsLevel();
        if (inLevel && !_wasInLevel)
            BeginLevel();
        else if (!inLevel && _wasInLevel)
            _visible = true;
        _wasInLevel = inLevel;

        if (!inLevel || Time.unscaledTime < _nextSample)
            return;

        _nextSample = Time.unscaledTime + Mathf.Max(0.25f, _cartSampleInterval.Value);
        SampleCartParticipants();
    }

    private void BeginLevel()
    {
        Board = new RunStatsBoard();
        _valuables.Clear();
        _creditedExtractions.Clear();
        _deadPlayers.Clear();
        _enemyAttribution.Clear();
        _creditedEnemyKills.Clear();
        _visible = false;
        RegisterExistingValuables();
        Logger.LogInfo("Started a new level stats snapshot");
    }

    private void RegisterExistingValuables()
    {
        if (!ValuableDirector.instance) return;
        foreach (var valuable in ValuableDirector.instance.valuableList)
            RegisterValuable(valuable);
    }

    internal void RegisterValuable(ValuableObject? valuable)
    {
        if (valuable is null || !valuable || _valuables.ContainsKey(valuable.GetInstanceID())) return;
        _valuables.Add(valuable.GetInstanceID(), new ValuableSample(valuable));
    }

    private void SampleCartParticipants()
    {
        RegisterExistingValuables();
        if (GameDirector.instance)
            foreach (var avatar in GameDirector.instance.PlayerList)
                GetPlayer(avatar);

        foreach (var entry in _valuables.ToArray())
        {
            var valuable = entry.Value.Valuable;
            if (!valuable)
            {
                _valuables.Remove(entry.Key);
                continue;
            }
            var detector = valuable.GetComponent<PhysGrabObjectImpactDetector>();
            if (detector && detector.inCart)
            {
                var cart = Traverse.Create(detector).Field("currentCart").GetValue<PhysGrabCart>();
                if (cart)
                    AddParticipants(entry.Value, GetGrabbers(cart.GetComponent<PhysGrabObject>()));
            }
        }
    }

    internal void RecordGrab(PhysGrabObject grabObject, int grabberPhotonViewId, bool released)
    {
        var photonView = Photon.Pun.PhotonView.Find(grabberPhotonViewId);
        var grabber = photonView ? photonView.GetComponent<PhysGrabber>() : null;
        if (grabber is null || !grabber || grabber.playerAvatar is null || !grabber.playerAvatar) return;

        var valuable = grabObject.GetComponent<ValuableObject>();
        if (valuable)
        {
            RegisterValuable(valuable);
            var sample = _valuables[valuable.GetInstanceID()];
            var player = GetPlayer(grabber.playerAvatar);
            if (player is not null) sample.Participants.Add(player.PlayerId);
            sample.LastCarrier = grabber.playerAvatar;
            if (released) sample.LastCarriedAt = Time.unscaledTime;
            return;
        }

        var cart = grabObject.GetComponent<PhysGrabCart>();
        if (!cart)
        {
            var enemyRigidbody = grabObject.GetComponent<EnemyRigidbody>() ??
                                 grabObject.GetComponentInParent<EnemyRigidbody>();
            var enemyHealth = enemyRigidbody ? enemyRigidbody.GetComponent<EnemyHealth>() : null;
            if (enemyHealth)
                RememberEnemyInteractor(enemyHealth!, grabber.playerAvatar);
            return;
        }
        if (released) return;
        foreach (var entry in _valuables)
        {
            var item = entry.Value.Valuable;
            if (!item) continue;
            var detector = item.GetComponent<PhysGrabObjectImpactDetector>();
            var currentCart = detector ? Traverse.Create(detector).Field("currentCart").GetValue<PhysGrabCart>() : null;
            if (currentCart == cart)
                AddParticipants(entry.Value, new[] { grabber });
        }
    }

    internal void RecordDeath(PlayerAvatar avatar)
    {
        var player = GetPlayer(avatar);
        if (player is not null && _deadPlayers.Add(player.PlayerId))
            player.RecordDeath();
    }

    internal PlayerAvatar? CaptureRescuer(PlayerAvatar revived, bool revivedByTruck)
    {
        var revivedPlayer = GetPlayer(revived);
        if (revivedPlayer is null || !_deadPlayers.Remove(revivedPlayer.PlayerId))
            return null;

        if (revivedByTruck)
            return null;

        var deathHead = Traverse.Create(revived).Field("playerDeathHead").GetValue<PlayerDeathHead>();
        if (!deathHead)
            return null;
        var grabObject = Traverse.Create(deathHead).Field("physGrabObject").GetValue<PhysGrabObject>();
        return GetGrabbers(grabObject)
            .Select(grabber => grabber.playerAvatar)
            .FirstOrDefault(player => player && player != revived);
    }

    internal void RecordRescue(PlayerAvatar? rescuer)
    {
        if (rescuer)
            GetPlayer(rescuer)?.RecordRescue();
    }

    internal void RecordDamage(PhysGrabObjectImpactDetector detector, float valueLost, bool loseValue)
    {
        if (!loseValue || valueLost <= 0f)
            return;

        var valuable = detector.GetComponent<ValuableObject>();
        if (!valuable)
            return;

        PlayerAvatar? responsible = null;
        var grabObject = valuable.GetComponent<PhysGrabObject>();
        var grabbers = GetGrabbers(grabObject);
        if (grabbers.Count > 0)
            responsible = grabbers[0].playerAvatar;
        else if (_valuables.TryGetValue(valuable.GetInstanceID(), out var sample) &&
                 Time.unscaledTime - sample.LastCarriedAt <= _releaseAttributionSeconds.Value)
            responsible = sample.LastCarrier;

        if (responsible)
            GetPlayer(responsible)?.AddValuableDamage(valueLost);
        else
            Board.AddUnattributedDamage(valueLost);
    }

    internal void RecordSuccessfulExtraction()
    {
        if (!RoundDirector.instance)
            return;

        foreach (var gameObject in RoundDirector.instance.dollarHaulList.ToArray())
        {
            if (!gameObject) continue;
            var valuable = gameObject.GetComponent<ValuableObject>();
            if (!valuable || !_creditedExtractions.Add(valuable.GetInstanceID())) continue;

            var value = ReadFloat(valuable, "dollarValueCurrent");
            if (!_valuables.TryGetValue(valuable.GetInstanceID(), out var sample) || sample.Participants.Count == 0)
            {
                Board.AddUnattributedExtractedValue(value);
                continue;
            }

            var share = value / sample.Participants.Count;
            foreach (var playerId in sample.Participants)
            {
                var player = Board.Players.FirstOrDefault(candidate => candidate.PlayerId == playerId);
                if (player is not null)
                    player.AddExtractedValue(share);
                else
                    Board.AddUnattributedExtractedValue(share);
            }
        }
    }

    internal void RecordWeaponEnemyHit(HurtCollider hurtCollider, Enemy enemy)
    {
        if (!enemy || hurtCollider.deathPit)
            return;

        var enemyHealth = enemy.GetComponent<EnemyHealth>();
        if (!enemyHealth)
            return;

        var weapon = hurtCollider.GetComponentInParent<PhysGrabObject>();
        if (!weapon)
            return;
        var player = Traverse.Create(weapon).Field("lastPlayerGrabbing").GetValue<PlayerAvatar>();
        RememberEnemyInteractor(enemyHealth, player);
    }

    internal void RecordEnemyDeath(EnemyHealth enemyHealth)
    {
        if (!enemyHealth || !_creditedEnemyKills.Add(enemyHealth.GetInstanceID()))
            return;

        PlayerAvatar? killer = null;
        if (_enemyAttribution.TryGetValue(enemyHealth.GetInstanceID(), out var attribution) &&
            Time.unscaledTime - attribution.At <= Mathf.Max(0f, _enemyKillAttributionSeconds.Value))
            killer = attribution.Player;

        killer ??= Traverse.Create(enemyHealth).Field("onObjectHurtPlayer").GetValue<PlayerAvatar>();
        if (killer)
            GetPlayer(killer)?.RecordEnemyKill();
    }

    private void RememberEnemyInteractor(EnemyHealth enemyHealth, PlayerAvatar? player)
    {
        if (enemyHealth && player)
            _enemyAttribution[enemyHealth.GetInstanceID()] = new EnemyAttribution(player!, Time.unscaledTime);
    }

    private void AddParticipants(ValuableSample sample, IEnumerable<PhysGrabber> grabbers)
    {
        foreach (var grabber in grabbers)
        {
            var player = GetPlayer(grabber.playerAvatar);
            if (player is not null)
                sample.Participants.Add(player.PlayerId);
        }
    }

    private PlayerRunStats? GetPlayer(PlayerAvatar? avatar)
    {
        if (!avatar) return null;
        var name = Traverse.Create(avatar).Field("playerName").GetValue<string>();
        // Steam IDs are populated after the avatar appears. Switching from a
        // temporary key to the Steam ID created a duplicate "Semibot" row on
        // subsequent levels, so use the connection-stable Photon actor ID.
        var id = avatar!.photonView
            ? "photon:" + avatar.photonView.OwnerActorNr.ToString(CultureInfo.InvariantCulture)
            : "local";
        return Board.GetOrAddPlayer(id, string.IsNullOrWhiteSpace(name) ? "Semibot" : name);
    }

    private static List<PhysGrabber> GetGrabbers(PhysGrabObject? grabObject)
    {
        var result = new List<PhysGrabber>();
        if (!grabObject) return result;
        var raw = Traverse.Create(grabObject).Field("playerGrabbing").GetValue();
        if (raw is not IEnumerable values) return result;
        foreach (var value in values)
            if (value is PhysGrabber grabber && grabber)
                result.Add(grabber);
        return result;
    }

    private static float ReadFloat(object instance, string field) =>
        Traverse.Create(instance).Field(field).GetValue<float>();

    private static bool SafeRunIsLevel()
    {
        try { return SemiFunc.RunIsLevel(); }
        catch { return false; }
    }

    private void OnGUI()
    {
        if (_visible)
        {
            if (_windowStyle is null)
                BuildVisualTheme();
            _window = GUI.Window(948210, _window, DrawWindow, string.Empty, _windowStyle);
        }
    }

    private void DrawWindow(int id)
    {
        DrawTerminalFrame();
        var terminalTitle = new GUIStyle(GUI.skin.label)
        {
            font = _terminalFont,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.52f, 1f, 0.52f) }
        };
        GUI.Label(new Rect(20, 5, _window.width - 40, 24),
            T("[ CRT-03 // SALVAGE TELEMETRY ]", "[ CRT-03 // 回收作业终端 ]"), terminalTitle);
        var header = new GUIStyle(GUI.skin.label)
        {
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            font = _terminalFont,
            normal = { textColor = new Color(0.52f, 1f, 0.52f) }
        };
        var playerStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            font = _terminalFont,
            normal = { textColor = new Color(0.38f, 0.86f, 0.42f) }
        };
        var numberStyle = new GUIStyle(playerStyle);

        GUILayout.BeginVertical();
        GUILayout.Label(T("SYS.READOUT / STATUS: ONLINE", "系统读数 / 状态：在线"), header);
        GUILayout.Space(3);
        GUILayout.BeginHorizontal();
        Column(T("PLAYER", "玩家"), 190, header);
        Column(T("DEATHS", "死亡"), 70, header);
        Column(T("RESCUES", "救援"), 70, header);
        Column(T("KILLS", "击杀"), 70, header);
        Column(T("RECOVERED VALUE", "回收贡献"), 140, header);
        Column(T("GOODS DAMAGED", "物品损失"), 140, header);
        Column(T("TITLES", "称号"), 360, header);
        GUILayout.EndHorizontal();
        GUILayout.Space(6);
        foreach (var player in Board.Players.OrderByDescending(p => p.ExtractedValue))
        {
            GUILayout.BeginHorizontal();
            Column(Truncate(player.DisplayName, 20), 190, playerStyle);
            Column(player.Deaths.ToString(CultureInfo.InvariantCulture), 70, numberStyle);
            Column(player.TeammatesRescued.ToString(CultureInfo.InvariantCulture), 70, numberStyle);
            Column(player.EnemyKills.ToString(CultureInfo.InvariantCulture), 70, numberStyle);
            Column(Dollars(player.ExtractedValue), 140, numberStyle);
            Column(Dollars(player.ValuableDamage), 140, numberStyle);
            var titleStyle = new GUIStyle(playerStyle) { fontStyle = FontStyle.Bold };
            titleStyle.normal.textColor = new Color(0.7f, 1f, 0.5f);
            Column(GetTitles(player), 360, titleStyle);
            GUILayout.EndHorizontal();
        }
        GUILayout.FlexibleSpace();
        GUILayout.Label(T(
            $"Unattributed goods damage: {Dollars(Board.UnattributedValuableDamage)}",
            $"未归属的物品损失：{Dollars(Board.UnattributedValuableDamage)}"));
        GUILayout.Label(T(
            $"Unattributed recovered value: {Dollars(Board.UnattributedExtractedValue)}",
            $"未归属的回收价值：{Dollars(Board.UnattributedExtractedValue)}"));
        GUILayout.Label(T(
            $"Press {_toggleKey.Value.MainKey} to close",
            $"按 {_toggleKey.Value.MainKey} 关闭"));
        GUILayout.EndVertical();
        GUI.DragWindow(new Rect(0, 0, 10000, 28));
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value.Substring(0, max - 1) + "…";

    private static string Dollars(double value) =>
        "$" + value.ToString("N0", CultureInfo.InvariantCulture);

    private string GetTitles(PlayerRunStats player)
    {
        var titles = new List<string>();
        AddTitle(titles, player, "Defective Unit", "残次品", p => p.Deaths);
        AddTitle(titles, player, "Guardian Angel", "再生父母", p => p.TeammatesRescued);
        AddTitle(titles, player, "Physics Exorcist", "物理超度师", p => p.EnemyKills);
        AddTitle(titles, player, "Born to Grind", "天生牛马", p => p.ExtractedValue);
        AddTitle(titles, player, "Financial Liability", "负资产", p => p.ValuableDamage);
        return string.Join(" / ", titles);
    }

    private void AddTitle(List<string> titles, PlayerRunStats player, string englishTitle,
        string chineseTitle, Func<PlayerRunStats, double> value)
    {
        var leaders = Board.Leaders(value);
        if (leaders.Count == 0 || value(leaders[0]) <= 0)
            return;
        if (leaders.Any(leader => leader.PlayerId == player.PlayerId))
            titles.Add(T(englishTitle, chineseTitle));
    }

    private static void Column(string text, float width, GUIStyle style) =>
        GUILayout.Label(text, style, GUILayout.Width(width));

    private void BuildVisualTheme()
    {
        _terminalFont = Font.CreateDynamicFontFromOSFont(
            UseChinese() ? new[] { "SimSun", "Microsoft YaHei", "Arial" } : new[] { "Consolas", "Courier New", "Arial" }, 16);
        _panelTexture = SolidTexture(new Color(0.025f, 0.027f, 0.023f, 0.99f));
        _screenTexture = SolidTexture(new Color(0.005f, 0.055f, 0.018f, 0.98f));
        _borderTexture = SolidTexture(new Color(0.12f, 0.16f, 0.11f, 1f));
        _rowTexture = SolidTexture(new Color(0.1f, 0.3f, 0.12f, 0.18f));
        _scanlineTexture = SolidTexture(new Color(0f, 0f, 0f, 0.24f));
        _windowStyle = new GUIStyle(GUI.skin.window)
        {
            normal = { background = _panelTexture, textColor = new Color(0.52f, 1f, 0.52f) },
            font = _terminalFont,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperCenter,
            padding = new RectOffset(22, 22, 48, 22)
        };
    }

    private void DrawTerminalFrame()
    {
        GUI.DrawTexture(new Rect(10, 27, _window.width - 20, _window.height - 37), _borderTexture);
        GUI.DrawTexture(new Rect(17, 34, _window.width - 34, _window.height - 51), _screenTexture);
        GUI.DrawTexture(new Rect(17, 34, _window.width - 34, 2), _borderTexture);
        GUI.DrawTexture(new Rect(17, _window.height - 19, _window.width - 34, 2), _borderTexture);
        for (var y = 36f; y < _window.height - 20; y += 3f)
            GUI.DrawTexture(new Rect(18, y, _window.width - 36, 1), _scanlineTexture);
    }

    private static Texture2D SolidTexture(Color color)
    {
        var texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }

    private bool UseChinese()
    {
        if (_language.Value.Equals("SimplifiedChinese", StringComparison.OrdinalIgnoreCase) ||
            _language.Value.Equals("Chinese", StringComparison.OrdinalIgnoreCase) ||
            _language.Value.Equals("简体中文", StringComparison.OrdinalIgnoreCase))
            return true;
        if (_language.Value.Equals("English", StringComparison.OrdinalIgnoreCase))
            return false;
        return Application.systemLanguage.ToString().IndexOf("Chinese", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private string T(string english, string chinese) => UseChinese() ? chinese : english;

    private sealed class ValuableSample
    {
        public ValuableSample(ValuableObject valuable) => Valuable = valuable;
        public ValuableObject Valuable { get; }
        public PlayerAvatar? LastCarrier;
        public float LastCarriedAt = float.NegativeInfinity;
        public HashSet<string> Participants { get; } = new(StringComparer.Ordinal);
    }

    private sealed class EnemyAttribution
    {
        public EnemyAttribution(PlayerAvatar player, float at)
        {
            Player = player;
            At = at;
        }

        public PlayerAvatar Player { get; }
        public float At { get; }
    }
}
