using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using Questionable.Controller;
using Questionable.External;

namespace Questionable.Windows.ConfigComponents;

internal sealed class PluginConfigComponent : ConfigComponent
{
    private static readonly IReadOnlyList<PluginInfo> RequiredPlugins =
    [
        new("vnavmesh",
            "vnavmesh",
            """
            vnavmesh 負責區域內導航，將角色移動至下一個任務目標。
            """,
            new Uri("https://github.com/awgil/ffxiv_navmesh/"),
            new Uri("https://puni.sh/api/repository/veyn")),
        new("Lifestream",
            "Lifestream",
            """
            用於在城市內透過都市傳送網移動。
            """,
            new Uri("https://github.com/NightmareXIV/Lifestream"),
            new Uri("https://github.com/NightmareXIV/MyDalamudPlugins/raw/main/pluginmaster.json")),
        new("TextAdvance",
            "TextAdvance",
            """
            自動接取與繳交任務，並略過過場動畫與對話。
            """,
            new Uri("https://github.com/NightmareXIV/TextAdvance"),
            new Uri("https://github.com/NightmareXIV/MyDalamudPlugins/raw/main/pluginmaster.json")),
    ];

    private static readonly ReadOnlyDictionary<Configuration.ECombatModule, PluginInfo> CombatPlugins =
        new Dictionary<Configuration.ECombatModule, PluginInfo>
        {
            {
                Configuration.ECombatModule.BossMod,
                new("Boss Mod (VBM)",
                    "BossMod",
                    string.Empty,
                    new Uri("https://github.com/awgil/ffxiv_bossmod"),
                    new Uri("https://puni.sh/api/repository/veyn"))
            },
            {
                Configuration.ECombatModule.WrathCombo,
                new PluginInfo("Wrath Combo",
                    "WrathCombo",
                    string.Empty,
                    new Uri("https://github.com/PunishXIV/WrathCombo"),
                    new Uri("https://puni.sh/api/plugins"))
            },
            {
                Configuration.ECombatModule.RotationSolverReborn,
                new("Rotation Solver Reborn",
                    "RotationSolver",
                    string.Empty,
                    new Uri("https://github.com/FFXIV-CombatReborn/RotationSolverReborn"),
                    new Uri(
                        "https://raw.githubusercontent.com/FFXIV-CombatReborn/CombatRebornRepo/main/pluginmaster.json"))
            },
        }.AsReadOnly();

    private readonly IReadOnlyList<PluginInfo> _recommendedPlugins;

    private readonly Configuration _configuration;
    private readonly CombatController _combatController;
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly UiUtils _uiUtils;
    private readonly ICommandManager _commandManager;

    public PluginConfigComponent(
        IDalamudPluginInterface pluginInterface,
        Configuration configuration,
        CombatController combatController,
        UiUtils uiUtils,
        ICommandManager commandManager,
        AutomatonIpc automatonIpc,
        PandorasBoxIpc pandorasBoxIpc)
        : base(pluginInterface, configuration)
    {
        _configuration = configuration;
        _combatController = combatController;
        _pluginInterface = pluginInterface;
        _uiUtils = uiUtils;
        _commandManager = commandManager;
        _recommendedPlugins =
        [
            new PluginInfo("CBT (formerly known as Automaton)",
                "Automaton",
                """
                CBT 是一組與自動化相關的調整功能。
                """,
                new Uri("https://github.com/Jaksuhn/Automaton"),
                new Uri("https://puni.sh/api/repository/croizat"),
                "/cbt",
                [
                    new PluginDetailInfo("已啟用「Sniper no sniping」",
                        "自動完成「紅蓮之狂潮」加入的射擊小遊戲",
                        () => automatonIpc.IsAutoSnipeEnabled)
                ]),
            new PluginInfo("Pandora's Box",
                "PandorasBox",
                """
                Pandora's Box 是一組便利功能。
                """,
                new Uri("https://github.com/PunishXIV/PandorasBox"),
                new Uri("https://puni.sh/api/plugins"),
                "/pandora",
                [
                    new PluginDetailInfo("已啟用「Auto Active Time Maneuver」",
                        """
                        在單人任務戰鬥、討伐殲滅戰及大型任務中，自動完成即時操作。
                        """,
                        () => pandorasBoxIpc.IsAutoActiveTimeManeuverEnabled)
                ]),
            new("NotificationMaster",
                "NotificationMaster",
                """
                任務需要手動操作時，傳送可自訂的遊戲外通知。
                """,
                new Uri("https://github.com/NightmareXIV/NotificationMaster"),
                null),
            new("Artisan",
                "Artisan",
                """
                自動進行製作
                """,
                new Uri("https://github.com/PunishXIV/Artisan"),
                new Uri("https://puni.sh/api/plugins"),
                "/artisan"),
        ];
    }

    public override void DrawTab()
    {
        using var tab = ImRaii.TabItem("相依插件###Plugins");
        if (!tab)
            return;

        Draw(out bool allRequiredInstalled);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (allRequiredInstalled)
            ImGui.TextColored(ImGuiColors.ParsedGreen, "所有必要插件均已安裝。");
        else
            ImGui.TextColored(ImGuiColors.DalamudRed,
                "缺少必要插件，Questionable 將無法正常運作。");
    }

    public void Draw(out bool allRequiredInstalled)
    {
        float checklistPadding;
        using (_pluginInterface.UiBuilder.IconFontFixedWidthHandle.Push())
        {
            checklistPadding = ImGui.CalcTextSize(FontAwesomeIcon.Check.ToIconString()).X +
                               ImGui.GetStyle().ItemSpacing.X;
        }

        ImGui.Text("Questionable 需要下列插件才能運作：");
        allRequiredInstalled = true;
        using (ImRaii.PushIndent())
        {
            foreach (var plugin in RequiredPlugins)
                allRequiredInstalled &= DrawPlugin(plugin, checklistPadding);
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.Text("Questionable 建議使用 Boss Mod（VBM）進行戰鬥自動化。");

        using (ImRaii.Disabled(_combatController.IsRunning))
        {
            using (ImRaii.PushIndent())
            {
                if (ImGui.RadioButton("不使用循環／戰鬥插件（必須手動戰鬥）",
                        _configuration.General.CombatModule == Configuration.ECombatModule.None))
                {
                    _configuration.General.CombatModule = Configuration.ECombatModule.None;
                    _pluginInterface.SavePluginConfig(_configuration);
                }

                allRequiredInstalled &= DrawCombatPlugin(Configuration.ECombatModule.BossMod, checklistPadding);
                allRequiredInstalled &= DrawCombatPlugin(Configuration.ECombatModule.WrathCombo, checklistPadding);
            }
            ImGui.Text("下列循環／戰鬥插件僅供相容性與測試用途：");
            using (ImRaii.PushIndent())
            {
                allRequiredInstalled &=
                    DrawCombatPlugin(Configuration.ECombatModule.RotationSolverReborn, checklistPadding);
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.Text("建議安裝下列插件，但並非必要：");
        using (ImRaii.PushIndent())
        {
            foreach (var plugin in _recommendedPlugins)
                DrawPlugin(plugin, checklistPadding);
        }
    }

    private bool DrawPlugin(PluginInfo plugin, float checklistPadding)
    {
        using (ImRaii.PushId("plugin_" + plugin.DisplayName))
        {
            IExposedPlugin? installedPlugin = FindInstalledPlugin(plugin);
            bool isInstalled = installedPlugin != null;
            string label = plugin.DisplayName;
            if (installedPlugin != null)
                label += $" v{installedPlugin.Version}";

            _uiUtils.ChecklistItem(label, isInstalled);

            DrawPluginDetails(plugin, checklistPadding, isInstalled);
            return isInstalled;
        }
    }

    private bool DrawCombatPlugin(Configuration.ECombatModule combatModule, float checklistPadding)
    {
        ImGui.Spacing();

        PluginInfo plugin = CombatPlugins[combatModule];
        using (ImRaii.PushId("plugin_" + plugin.DisplayName))
        {
            IExposedPlugin? installedPlugin = FindInstalledPlugin(plugin);
            bool isInstalled = installedPlugin != null;
            string label = plugin.DisplayName;
            if (installedPlugin != null)
                label += $" v{installedPlugin.Version}";

            if (ImGui.RadioButton(label, _configuration.General.CombatModule == combatModule))
            {
                _configuration.General.CombatModule = combatModule;
                _pluginInterface.SavePluginConfig(_configuration);
            }

            ImGui.SameLine(0);
            using (_pluginInterface.UiBuilder.IconFontFixedWidthHandle.Push())
            {
                var iconColor = isInstalled ? ImGuiColors.ParsedGreen : ImGuiColors.DalamudRed;
                var icon = isInstalled ? FontAwesomeIcon.Check : FontAwesomeIcon.Times;

                ImGui.AlignTextToFramePadding();
                ImGui.TextColored(iconColor, icon.ToIconString());
            }

            DrawPluginDetails(plugin, checklistPadding, isInstalled);
            return isInstalled || _configuration.General.CombatModule != combatModule;
        }
    }

    private void DrawPluginDetails(PluginInfo plugin, float checklistPadding, bool isInstalled)
    {
        using (ImRaii.PushIndent(checklistPadding))
        {
            if (!string.IsNullOrEmpty(plugin.Details))
                ImGui.TextUnformatted(plugin.Details);

            bool allDetailsOk = true;
            if (plugin.DetailsToCheck != null)
            {
                foreach (var detail in plugin.DetailsToCheck)
                {
                    bool detailOk = detail.Predicate();
                    allDetailsOk &= detailOk;

                    _uiUtils.ChecklistItem(detail.DisplayName, isInstalled && detailOk);
                    if (!string.IsNullOrEmpty(detail.Details))
                    {
                        using (ImRaii.PushIndent(checklistPadding))
                        {
                            ImGui.TextUnformatted(detail.Details);
                        }
                    }
                }
            }

            ImGui.Spacing();

            if (isInstalled)
            {
                if (!allDetailsOk && plugin.ConfigCommand != null && plugin.ConfigCommand.StartsWith('/'))
                {
                    if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Cog, "開啟設定"))
                        _commandManager.ProcessCommand(plugin.ConfigCommand);
                }
            }
            else
            {
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Globe, "開啟網站"))
                    Util.OpenLink(plugin.WebsiteUri.ToString());

                ImGui.SameLine();
                if (plugin.DalamudRepositoryUri != null)
                {
                    if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Code, "開啟原始碼倉庫"))
                        Util.OpenLink(plugin.DalamudRepositoryUri.ToString());
                }
                else
                {
                    ImGui.AlignTextToFramePadding();
                    ImGuiComponents.HelpMarker("可從 Dalamud 官方插件庫取得");
                }
            }
        }
    }

    private IExposedPlugin? FindInstalledPlugin(PluginInfo pluginInfo)
    {
        return _pluginInterface.InstalledPlugins.FirstOrDefault(x =>
            x.InternalName == pluginInfo.InternalName && x.IsLoaded);
    }

    private sealed record PluginInfo(
        string DisplayName,
        string InternalName,
        string Details,
        Uri WebsiteUri,
        Uri? DalamudRepositoryUri,
        string? ConfigCommand = null,
        List<PluginDetailInfo>? DetailsToCheck = null);

    private sealed record PluginDetailInfo(string DisplayName, string Details, Func<bool> Predicate);
}
