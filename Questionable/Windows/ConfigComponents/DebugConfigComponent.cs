using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace Questionable.Windows.ConfigComponents;

internal sealed class DebugConfigComponent : ConfigComponent
{
    public DebugConfigComponent(IDalamudPluginInterface pluginInterface, Configuration configuration)
        : base(pluginInterface, configuration)
    {
    }

    public override void DrawTab()
    {
        using var tab = ImRaii.TabItem("進階###Debug");
        if (!tab)
            return;

        ImGui.TextColored(ImGuiColors.DalamudRed,
            "啟用此處的任何選項都可能造成非預期行為，請自行承擔風險。");

        ImGui.Separator();

        bool debugOverlay = Configuration.Advanced.DebugOverlay;
        if (ImGui.Checkbox("啟用偵錯覆蓋層", ref debugOverlay))
        {
            Configuration.Advanced.DebugOverlay = debugOverlay;
            Save();
        }

        using (ImRaii.Disabled(!debugOverlay))
        {
            using (ImRaii.PushIndent())
            {
                bool combatDataOverlay = Configuration.Advanced.CombatDataOverlay;
                if (ImGui.Checkbox("啟用戰鬥資料覆蓋層", ref combatDataOverlay))
                {
                    Configuration.Advanced.CombatDataOverlay = combatDataOverlay;
                    Save();
                }
            }
        }

        bool highlightNpc = Configuration.Advanced.HighlightSelectedNpc;
        if (ImGui.Checkbox("標示與目前任務階段相關的 NPC", ref highlightNpc))
        {
            Configuration.Advanced.HighlightSelectedNpc = highlightNpc;
            Save();
        }

        using (ImRaii.Disabled(!highlightNpc))
        {
            using (ImRaii.PushIndent())
            {
                var highlightColorNames = Enum.GetNames<ObjectHighlightColor>();
                var highlightColorValues = Enum.GetValues<ObjectHighlightColor>();
                var selectedHighlightColor = Array.IndexOf(highlightColorValues, Configuration.Advanced.HighlightColor);
                ImGui.SetNextItemWidth(150f);
                if (ImGui.Combo("標示顏色", ref selectedHighlightColor, highlightColorNames, highlightColorNames.Length))
                {
                    Configuration.Advanced.HighlightColor = (ObjectHighlightColor)selectedHighlightColor;
                    Save();
                }
            }
        }

        bool neverFly = Configuration.Advanced.NeverFly;
        if (ImGui.Checkbox("停用飛行（即使該區域已解鎖飛行）", ref neverFly))
        {
            Configuration.Advanced.NeverFly = neverFly;
            Save();
        }

        bool additionalStatusInformation = Configuration.Advanced.AdditionalStatusInformation;
        if (ImGui.Checkbox("顯示額外狀態資訊", ref additionalStatusInformation))
        {
            Configuration.Advanced.AdditionalStatusInformation = additionalStatusInformation;
            Save();
        }

        if (additionalStatusInformation)
        {
            bool showTracked = Configuration.Advanced.ShowTracked;
            bool showDailies = Configuration.Advanced.ShowDailies;
            bool showDirector = Configuration.Advanced.ShowDirector;
            bool showActionManager = Configuration.Advanced.ShowActionManager;
            bool showNewGamePlus = Configuration.Advanced.ShowNewGamePlus;
            using (ImRaii.PushIndent())
            {
                ImGui.AlignTextToFramePadding();
                if (ImGui.Checkbox("顯示追蹤中的任務", ref showTracked))
                {
                    Configuration.Advanced.ShowTracked = showTracked;
                    Save();
                }
                if (ImGui.Checkbox("顯示已接取／已完成的每日任務", ref showDailies))
                {
                    Configuration.Advanced.ShowDailies = showDailies;
                    Save();
                }
                if (ImGui.Checkbox("顯示 Director 資訊", ref showDirector))
                {
                    Configuration.Advanced.ShowDirector = showDirector;
                    Save();
                }
                if (ImGui.Checkbox("顯示 Action Manager", ref showActionManager))
                {
                    Configuration.Advanced.ShowActionManager = showActionManager;
                    Save();
                }
                if (ImGui.Checkbox("顯示「新生冒險錄」章節", ref showNewGamePlus))
                {
                    Configuration.Advanced.ShowNewGamePlus = showNewGamePlus;
                    Save();
                }
            }
        }

        ImGui.Separator();

        ImGui.Text("AutoDuty 設定");
        using (ImRaii.PushIndent())
        {
            ImGui.AlignTextToFramePadding();
            bool disableAutoDutyBareMode = Configuration.Advanced.DisableAutoDutyBareMode;
            if (ImGui.Checkbox("使用循環前／循環／循環後設定", ref disableAutoDutyBareMode))
            {
                Configuration.Advanced.DisableAutoDutyBareMode = disableAutoDutyBareMode;
                Save();
            }

            ImGui.SameLine();
            ImGuiComponents.HelpMarker(
                "Questionable 執行副本時通常會停用 AutoDuty 的循環設定，因為這些設定可能造成問題（甚至使電腦關機）。");
        }

        ImGui.Separator();
        ImGui.Text("略過任務／互動");
        using (ImRaii.PushIndent())
        {
            bool skipAetherCurrents = Configuration.Advanced.SkipAetherCurrents;
            if (ImGui.Checkbox("不接取風脈泉／風脈泉任務", ref skipAetherCurrents))
            {
                Configuration.Advanced.SkipAetherCurrents = skipAetherCurrents;
                Save();
            }

            ImGui.SameLine();
            ImGuiComponents.HelpMarker("若 Questionable 未在主線任務途中完成，你必須手動取得遺漏的風脈泉或任務；目前沒有自動補齊所有風脈泉的方法。");

            bool skipClassJobQuests = Configuration.Advanced.SkipClassJobQuests;
            if (ImGui.Checkbox("不接取職業／特職／職能任務", ref skipClassJobQuests))
            {
                Configuration.Advanced.SkipClassJobQuests = skipClassJobQuests;
                Save();
            }

            ImGui.SameLine();
            ImGuiComponents.HelpMarker("「重生之境」、「蒼穹之禁城」及「紅蓮之狂潮」的部分技能（含 70 級技能）需完成職業任務才能解鎖。若打算使用任務搜索器或招募隊員參加副本，不建議啟用。");

            bool skipARealmRebornHardModePrimals = Configuration.Advanced.SkipARealmRebornHardModePrimals;
            if (ImGui.Checkbox("不接取 2.0 高難度蠻神任務", ref skipARealmRebornHardModePrimals))
            {
                Configuration.Advanced.SkipARealmRebornHardModePrimals = skipARealmRebornHardModePrimals;
                Save();
            }

            ImGui.SameLine();
            ImGuiComponents.HelpMarker("完成高難度伊弗利特、迦樓羅與泰坦是進行 2.5 任務及開始「蒼穹之禁城」的必要條件。");

            bool skipCrystalTowerRaids = Configuration.Advanced.SkipCrystalTowerRaids;
            if (ImGui.Checkbox("不接取水晶塔任務", ref skipCrystalTowerRaids))
            {
                Configuration.Advanced.SkipCrystalTowerRaids = skipCrystalTowerRaids;
                Save();
            }

            ImGui.SameLine();
            ImGuiComponents.HelpMarker("完成水晶塔團隊任務是進行 2.55 任務及開始「蒼穹之禁城」的必要條件。");

            bool preventQuestCompletion = Configuration.Advanced.PreventQuestCompletion;
            if (ImGui.Checkbox("防止完成任務", ref preventQuestCompletion))
            {
                Configuration.Advanced.PreventQuestCompletion = preventQuestCompletion;
                Save();
            }

            ImGui.SameLine();
            ImGuiComponents.HelpMarker("啟用後，Questionable 不會繳交並完成任務；除最後的交付步驟外，其餘流程仍會自動執行。");

            bool namazuPreferCraft = Configuration.Advanced.NamazuPreferCraft;
            if (ImGui.Checkbox("鯰魚族：優先使用能工巧匠而非大地使者", ref namazuPreferCraft))
            {
                Configuration.Advanced.NamazuPreferCraft = namazuPreferCraft;
                Save();
            }

            ImGui.SameLine();
            ImGuiComponents.HelpMarker("鯰魚族友好部族任務可由能工巧匠或大地使者完成，此選項用來設定偏好。");

            bool showWindowOnStart = Configuration.Advanced.ShowWindowOnStart;
            if (ImGui.Checkbox("啟動時顯示視窗", ref showWindowOnStart))
            {
                Configuration.Advanced.ShowWindowOnStart = showWindowOnStart;
                Save();
            }

            ImGui.SameLine();
            ImGuiComponents.HelpMarker("啟用後，載入插件時會顯示 Questionable 的進度視窗。");

            bool startMinimized = Configuration.Advanced.StartMinimized;
            if (ImGui.Checkbox("啟動時最小化", ref startMinimized))
            {
                Configuration.Advanced.StartMinimized = startMinimized;
                Save();
            }

            ImGui.SameLine();
            ImGuiComponents.HelpMarker("啟用後，Questionable 的進度視窗會以最小化狀態載入。");

            #if DEBUG
            bool openEditor = Configuration.Advanced.OpenEditor;
            if (ImGui.Checkbox("開始任務時開啟編輯器", ref openEditor))
            {
                Configuration.Advanced.OpenEditor = openEditor;
                Save();
            }

            ImGui.SameLine();
            ImGuiComponents.HelpMarker("啟用後，Questionable 會以預設文字編輯器開啟目前任務的路徑檔案。");
            #endif
        }
    }
}
