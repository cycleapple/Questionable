using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using LLib.GameData;
using Lumina.Excel.Sheets;
using Questionable.Controller;
using Questionable.Data;
using GrandCompany = FFXIVClientStructs.FFXIV.Client.UI.Agent.GrandCompany;

namespace Questionable.Windows.ConfigComponents;

internal sealed class GeneralConfigComponent : ConfigComponent
{
    private static readonly List<(uint Id, string Name)> DefaultMounts = [(0, "隨機坐騎")];
    private static readonly List<(EClassJob ClassJob, string Name)> DefaultClassJobs = [(EClassJob.Adventurer, "自動（最高等級／品級）")];

    private readonly QuestRegistry _questRegistry;
    private readonly TerritoryData _territoryData;

    private readonly uint[] _mountIds;
    private readonly string[] _mountNames;
    private readonly string[] _combatModuleNames = ["無", "Boss Mod (VBM)", "Wrath Combo", "Rotation Solver Reborn"];

    private readonly string[] _grandCompanyNames =
        ["無（手動選擇任務）", "黑渦團", "雙蛇黨", "恆輝隊"];

    private readonly EClassJob[] _classJobIds;
    private readonly string[] _classJobNames;
    private readonly EClassJob[] _craftJobIds;
    private readonly string[] _craftJobNames;
    private readonly EClassJob[] _gatherJobIds;
    private readonly string[] _gatherJobNames;

    public GeneralConfigComponent(
        IDalamudPluginInterface pluginInterface,
        Configuration configuration,
        IDataManager dataManager,
        ClassJobUtils classJobUtils,
        QuestRegistry questRegistry,
        TerritoryData territoryData)
        : base(pluginInterface, configuration)
    {
        _questRegistry = questRegistry;
        _territoryData = territoryData;

        var mounts = dataManager.GetExcelSheet<Mount>()
            .Where(x => x is { RowId: > 0, Icon: > 0 })
            .Select(x => (MountId: x.RowId, Name: x.Singular.ToString()))
            .Where(x => !string.IsNullOrEmpty(x.Name))
            .OrderBy(x => x.Name)
            .ToList();
        _mountIds = DefaultMounts.Select(x => x.Id).Concat(mounts.Select(x => x.MountId)).ToArray();
        _mountNames = DefaultMounts.Select(x => x.Name).Concat(mounts.Select(x => x.Name)).ToArray();

        var sortedClassJobs = classJobUtils.SortedClassJobs.Select(x => x.ClassJob).ToList();
        var classJobs = Enum.GetValues<EClassJob>()
            .Where(x => x != EClassJob.Adventurer)
            .Where(x => !x.IsCrafter() && !x.IsGatherer())
            .Where(x => !x.IsClass())
            .OrderBy(x => sortedClassJobs.IndexOf(x))
            .ToList();
        _classJobIds = DefaultClassJobs.Select(x => x.ClassJob).Concat(classJobs).ToArray();
        _classJobNames = DefaultClassJobs.Select(x => x.Name).Concat(classJobs.Select(x => x.ToFriendlyString())).ToArray();

        var craftJobs = Enum.GetValues<EClassJob>()
            .Where(x => x != EClassJob.Adventurer)
            .Where(x => x.IsCrafter())
            .OrderBy(x => sortedClassJobs.IndexOf(x))
            .ToList();
        _craftJobIds = craftJobs.ToArray();
        _craftJobNames = craftJobs.Select(x => x.ToFriendlyString()).ToArray();

        var gatherJobs = Enum.GetValues<EClassJob>()
            .Where(x => x != EClassJob.Adventurer)
            .Where(x => x == EClassJob.Miner || x == EClassJob.Botanist)
            .OrderBy(x => sortedClassJobs.IndexOf(x))
            .ToList();
        _gatherJobIds = gatherJobs.ToArray();
        _gatherJobNames = gatherJobs.Select(x => x.ToFriendlyString()).ToArray();
    }

    public override void DrawTab()
    {
        using var tab = ImRaii.TabItem("一般###General");
        if (!tab)
            return;


        {
            int selectedCombatModule = (int)Configuration.General.CombatModule;
            if (ImGui.Combo("偏好的戰鬥模組", ref selectedCombatModule, _combatModuleNames,
                    _combatModuleNames.Length))
            {
                Configuration.General.CombatModule = (Configuration.ECombatModule)selectedCombatModule;
                Save();
            }
        }

        int selectedMount = Array.FindIndex(_mountIds, x => x == Configuration.General.MountId);
        if (selectedMount == -1)
        {
            selectedMount = 0;
            Configuration.General.MountId = _mountIds[selectedMount];
            Save();
        }

        if (ImGui.Combo("偏好的坐騎", ref selectedMount, _mountNames, _mountNames.Length))
        {
            Configuration.General.MountId = _mountIds[selectedMount];
            Save();
        }

        int grandCompany = (int)Configuration.General.GrandCompany;
        if (ImGui.Combo("偏好的大國防聯軍", ref grandCompany, _grandCompanyNames,
                _grandCompanyNames.Length))
        {
            Configuration.General.GrandCompany = (GrandCompany)grandCompany;
            Save();
        }

        int combatJob = Array.IndexOf(_classJobIds, Configuration.General.CombatJob);
        if (combatJob == -1)
        {
            Configuration.General.CombatJob = EClassJob.Adventurer;
            Save();

            combatJob = 0;
        }

        if (ImGui.Combo("偏好的戰鬥特職", ref combatJob, _classJobNames, _classJobNames.Length))
        {
            Configuration.General.CombatJob = _classJobIds[combatJob];
            Save();
        }


        int craftingJob = Array.IndexOf(_craftJobIds, Configuration.General.CraftingJob);
        if (craftingJob == -1)
        {
            Configuration.General.CraftingJob = EClassJob.Carpenter;
            Save();

            craftingJob = 8;
        }

        if (ImGui.Combo("偏好的製作職業", ref craftingJob, _craftJobNames, _craftJobNames.Length))
        {
            Configuration.General.CraftingJob = _craftJobIds[craftingJob];
            Save();
        }


        int gatherJob = Array.IndexOf(_gatherJobIds, Configuration.General.GatheringJob);
        if (gatherJob == -1)
        {
            Configuration.General.GatheringJob = EClassJob.Miner;
            Save();

            gatherJob = 16;
        }

        if (ImGui.Combo("偏好的採集職業", ref gatherJob, _gatherJobNames, _gatherJobNames.Length))
        {
            Configuration.General.GatheringJob = _gatherJobIds[gatherJob];
            Save();
        }

        ImGui.Separator();
        ImGui.Text("UI");
        using (ImRaii.PushIndent())
        {
            bool hideInAllInstances = Configuration.General.HideInAllInstances;
            if (ImGui.Checkbox("在所有副本中隱藏任務視窗", ref hideInAllInstances))
            {
                Configuration.General.HideInAllInstances = hideInAllInstances;
                Save();
            }

            bool useEscToCancelQuesting = Configuration.General.UseEscToCancelQuesting;
            if (ImGui.Checkbox("使用 ESC 取消任務執行／移動", ref useEscToCancelQuesting))
            {
                Configuration.General.UseEscToCancelQuesting = useEscToCancelQuesting;
                Save();
            }

            bool showIncompleteSeasonalEvents = Configuration.General.ShowIncompleteSeasonalEvents;
            if (ImGui.Checkbox("顯示尚未完成的季節活動詳情", ref showIncompleteSeasonalEvents))
            {
                Configuration.General.ShowIncompleteSeasonalEvents = showIncompleteSeasonalEvents;
                Save();
            }

            bool hideSponsorButton = Configuration.General.HideSponsorButton;
            if (ImGui.Checkbox("隱藏贊助按鈕", ref hideSponsorButton))
            {
                Configuration.General.HideSponsorButton = hideSponsorButton;
                Save();
            }
        }

        ImGui.Separator();
        ImGui.Text("任務執行");
        using (ImRaii.PushIndent())
        {
            bool configureTextAdvance = Configuration.General.ConfigureTextAdvance;
            if (ImGui.Checkbox("自動套用 TextAdvance 的建議設定",
                    ref configureTextAdvance))
            {
                Configuration.General.ConfigureTextAdvance = configureTextAdvance;
                Save();
            }

            bool skipLowPriorityInstances = Configuration.General.SkipLowPriorityDuties;
            if (ImGui.Checkbox("解鎖部分選擇性迷宮與大型任務（不等待手動完成）", ref skipLowPriorityInstances))
            {
                Configuration.General.SkipLowPriorityDuties = skipLowPriorityInstances;
                Save();
            }

            ImGui.SameLine();
            using (ImRaii.PushFont(UiBuilder.IconFont))
            {
                ImGui.TextDisabled(FontAwesomeIcon.InfoCircle.ToIconString());
            }

            if (ImGui.IsItemHovered())
            {
                using (ImRaii.Tooltip())
                {
                    ImGui.Text("Questionable 會自動接取部分選擇性任務（例如風脈任務或 2.0 聯盟大型任務）。");
                    ImGui.Text("啟用後，Questionable 會繼續執行其他任務，不會等待你手動完成副本。");

                    ImGui.Separator();
                    ImGui.Text("此設定會影響下列迷宮與大型任務：");
                    foreach (var lowPriorityCfc in _questRegistry.LowPriorityContentFinderConditionQuests)
                    {
                        if (_territoryData.TryGetContentFinderCondition(lowPriorityCfc.ContentFinderConditionId, out var cfcData))
                        {
                            ImGui.BulletText($"{cfcData.Name}");
                        }
                    }
                }
            }

            bool useTickets = Configuration.General.UseTickets;
            if (ImGui.Checkbox("可使用時優先使用傳送網使用券", ref useTickets))
            {
                Configuration.General.UseTickets = useTickets;
                Save();
            }

            if (ImGui.IsItemHovered())
            {
                using (ImRaii.Tooltip())
                {
                    ImGui.Text("建議優先使用遊戲內的傳送設定；此選項僅為方便使用。");
                }
            }

#if false
            ImGui.Spacing();
            bool autoStepRefreshEnabled = Configuration.General.AutoStepRefreshEnabled;
            if (ImGui.Checkbox("Automatically refresh quest steps when stuck (WIP see tooltip)", ref autoStepRefreshEnabled))
            {
                Configuration.General.AutoStepRefreshEnabled = autoStepRefreshEnabled;
                Save();
            }

            ImGui.SameLine();
            using (ImRaii.PushFont(UiBuilder.IconFont))
            {
                ImGui.TextDisabled(FontAwesomeIcon.InfoCircle.ToIconString());
            }

            if (ImGui.IsItemHovered())
            {
                using (ImRaii.Tooltip())
                {
                    ImGui.Text("Questionable will automatically refresh a quest step if it appears to be stuck after the configured delay.");
                    ImGui.Text("This helps resume automated quest completion when interruptions occur.");
                    ImGui.Text("WIP feature, rather than remove it, this is a warning that it isn't fully complete.");
                }
            }

            using (ImRaii.Disabled(!autoStepRefreshEnabled))
            {
                ImGui.Indent();
                int autoStepRefreshDelay = Configuration.General.AutoStepRefreshDelaySeconds;
                ImGui.SetNextItemWidth(150f);
                if (ImGui.SliderInt("Refresh delay (seconds)", ref autoStepRefreshDelay, 30, 180))
                {
                    Configuration.General.AutoStepRefreshDelaySeconds = autoStepRefreshDelay;
                    Save();
                }

                ImGui.TextColored(new System.Numerics.Vector4(0.7f, 0.7f, 0.7f, 1.0f),
                    $"Quest steps will refresh automatically after {autoStepRefreshDelay} seconds if no progress is made.");
                ImGui.Unindent();
            }
#endif
        }
    }
}
