using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Style;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using Questionable.Controller;
using Questionable.Data;
using Questionable.Functions;
using Questionable.Model;
using Questionable.Validation;
using Questionable.Windows.QuestComponents;

namespace Questionable.Windows.JournalComponents;

internal sealed class QuestJournalComponent
{
    private readonly Dictionary<JournalData.Genre, JournalCounts> _genreCounts = [];
    private readonly Dictionary<JournalData.Category, JournalCounts> _categoryCounts = [];
    private readonly Dictionary<JournalData.Section, JournalCounts> _sectionCounts = [];

    private readonly JournalData _journalData;
    private readonly QuestRegistry _questRegistry;
    private readonly QuestFunctions _questFunctions;
    private readonly UiUtils _uiUtils;
    private readonly QuestTooltipComponent _questTooltipComponent;
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly QuestJournalUtils _questJournalUtils;
    private readonly QuestValidator _questValidator;

    private List<FilteredSection> _filteredSections = [];

    public QuestJournalComponent(JournalData journalData, QuestRegistry questRegistry, QuestFunctions questFunctions,
        UiUtils uiUtils, QuestTooltipComponent questTooltipComponent, IDalamudPluginInterface pluginInterface,
        QuestJournalUtils questJournalUtils, QuestValidator questValidator)
    {
        _journalData = journalData;
        _questRegistry = questRegistry;
        _questFunctions = questFunctions;
        _uiUtils = uiUtils;
        _questTooltipComponent = questTooltipComponent;
        _pluginInterface = pluginInterface;
        _questJournalUtils = questJournalUtils;
        _questValidator = questValidator;
    }

    internal FilterConfiguration Filter { get; } = new();

    public void DrawQuests()
    {
        using var tab = ImRaii.TabItem("任務###Quests");
        if (!tab)
            return;

        if (ImGui.CollapsingHeader("說明"))
        {
            ImGui.Text("下方清單包含任務日誌中的所有任務。");
            ImGui.BulletText("「支援」表示 Questionable 可以代為執行的任務");
            ImGui.BulletText("「已完成」表示目前角色已完成的任務");
            ImGui.BulletText(
                "即使任務顯示為可接取，也不代表一定能完成，例如不同起始城市的任務線。");
            ImGui.BulletText("「支援」欄位中的文字表示該任務路徑最後一次回報為正常運作的時間。");
            ImGui.TextColoredWrapped(ImGuiColors.DalamudYellow,
                "可透過右鍵選單，將單一任務或整組任務加入優先任務。");

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
        }

        QuestJournalUtils.ShowFilterContextMenu(this);

        ImGui.SameLine();
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        if (ImGui.InputTextWithHint(string.Empty, "搜尋任務與分類", ref Filter.SearchText, 256))
            UpdateFilter();

        if (_filteredSections.Count > 0)
        {
            using var table = ImRaii.Table("Quest", 3, ImGuiTableFlags.NoSavedSettings);
            if (!table)
                return;

            ImGui.TableSetupColumn("名稱", ImGuiTableColumnFlags.NoHide);
            ImGui.TableSetupColumn("支援狀態", ImGuiTableColumnFlags.WidthFixed, 120 * ImGui.GetIO().FontGlobalScale);
            ImGui.TableSetupColumn("已完成", ImGuiTableColumnFlags.WidthFixed, 120 * ImGui.GetIO().FontGlobalScale);
            ImGui.TableHeadersRow();

            foreach (var section in _filteredSections)
                DrawSection(section);
        }
        else
            ImGui.Text("找不到符合搜尋條件的任務或分類。");
    }

    private void DrawSection(FilteredSection filter)
    {
        (int available, int total, int obtainable, int completed) =
            _sectionCounts.GetValueOrDefault(filter.Section, new());
        if (total == 0)
            return;

        ImGui.TableNextRow();
        ImGui.TableNextColumn();

        bool open = ImGui.TreeNodeEx(filter.Section.Name, ImGuiTreeNodeFlags.SpanFullWidth);

        ImGui.TableNextColumn();
        DrawCount(available, total);
        ImGui.TableNextColumn();
        DrawCount(completed, obtainable);

        if (open)
        {
            foreach (var category in filter.Categories)
                DrawCategory(category);

            ImGui.TreePop();
        }
    }

    private void DrawCategory(FilteredCategory filter)
    {
        (int available, int total, int obtainable, int completed) =
            _categoryCounts.GetValueOrDefault(filter.Category, new());
        if (total == 0)
            return;

        ImGui.TableNextRow();
        ImGui.TableNextColumn();

        bool open = ImGui.TreeNodeEx(filter.Category.Name, ImGuiTreeNodeFlags.SpanFullWidth);

        ImGui.TableNextColumn();
        DrawCount(available, total);
        ImGui.TableNextColumn();
        DrawCount(completed, obtainable);

        if (open)
        {
            foreach (var genre in filter.Genres)
                DrawGenre(genre);

            ImGui.TreePop();
        }
    }

    private void DrawGenre(FilteredGenre filter)
    {
        (int supported, int total, int obtainable, int completed) = _genreCounts.GetValueOrDefault(filter.Genre, new());
        if (total == 0)
            return;

        ImGui.TableNextRow();
        ImGui.TableNextColumn();

        bool open = ImGui.TreeNodeEx(filter.Genre.Name, ImGuiTreeNodeFlags.SpanFullWidth);

        _questJournalUtils.ShowQuestGroupContextMenu($"DrawGenre{filter.Genre.Id}", filter.Quests);

        ImGui.TableNextColumn();
        DrawCount(supported, total);
        ImGui.TableNextColumn();
        DrawCount(completed, obtainable);

        if (open)
        {
            foreach (var quest in filter.Quests)
                DrawQuest(quest);

            ImGui.TreePop();
        }
    }

    private void DrawQuest(IQuestInfo questInfo)
    {
        DrawQuest((QuestInfo)questInfo);
    }

    private void DrawQuest(QuestInfo questInfo)
    {
        Quest? quest;
        bool fate = false;
        string lastChecked = "";
        string lastCheckedLong = "";
        string questDescription = $"{questInfo.Name} ({questInfo.QuestId})";
        if (_questRegistry.TryGetQuest(questInfo.QuestId, out quest))
        {
            if (quest.Root.LastChecked.Date != null)
            {
                lastCheckedLong = $"\n最後確認：{quest.Root.LastChecked}";
                var since = (int)quest.Root.LastChecked.Since(DateTime.Now)!.Value.TotalDays;
                if (since < 7)
                    lastChecked = $"{since}d";
                else
                    lastChecked = $"{since / 7}w";
            }
            if ((quest.Root.Comment ?? "").Contains("FATE"))
            {
                fate = true;
            }
        }

        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TreeNodeEx(questDescription,
            ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen | ImGuiTreeNodeFlags.SpanFullWidth);


        if (ImGui.IsItemHovered())
            _questTooltipComponent.Draw(questInfo);

        _questJournalUtils.ShowContextMenu(questInfo, quest, nameof(QuestJournalComponent));

        ImGui.TableNextColumn();
        float spacing;
        // ReSharper disable once UnusedVariable
        using (var font = _pluginInterface.UiBuilder.IconFontFixedWidthHandle.Push())
        {
            spacing = ImGui.GetColumnWidth() / 2 - ImGui.CalcTextSize(FontAwesomeIcon.Check.ToIconString()).X;
        }

        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + spacing);
        string defaultReason;
        var reason = defaultReason = "<no reason specified>";
        if (quest != null)
            reason = (quest.Root.Comment ?? defaultReason).Split('\n', 2)[0];

        if (_questFunctions.IsQuestRemoved(questInfo.QuestId))
        {
            if (_uiUtils.ChecklistItem(lastChecked, ImGuiColors.DalamudGrey, FontAwesomeIcon.Minus))
                ImGui.SetTooltip("目前無法接取此任務。");
        }
        else if (fate)
        {
            if (_uiUtils.ChecklistItem(lastChecked, ImGuiColors.DalamudOrange, FontAwesomeIcon.ExclamationTriangle))
                ImGui.SetTooltip($"此任務需要先完成危命任務。{lastCheckedLong}");
        }
        else if (quest is { Root.Disabled: false })
        {
            List<ValidationIssue> issues = _questValidator.GetIssues(quest.Id);
            if (issues.Any(x => x.Severity == EIssueSeverity.Error))
            {
                if (_uiUtils.ChecklistItem(lastChecked, ImGuiColors.DalamudRed, FontAwesomeIcon.ExclamationTriangle))
                    ImGui.SetTooltip("無法載入此任務。");
            }
            else if (issues.Count > 0)
            {
                if (_uiUtils.ChecklistItem(lastChecked, ImGuiColors.ParsedBlue, FontAwesomeIcon.InfoCircle))
                    ImGui.SetTooltip("此任務有驗證問題。");
            }
            else
                if (_uiUtils.ChecklistItem(lastChecked, true))
                    ImGui.SetTooltip($"此任務已受支援。{lastCheckedLong}" + (!reason.Equals(defaultReason, StringComparison.Ordinal) ? $"\n備註：{reason}" : ""));
        }
        else
        {
            if (quest == null)
                reason = "No quest path.";
            if (_uiUtils.ChecklistItem(lastChecked, false))
                ImGui.SetTooltip($"此任務尚未受支援。{lastCheckedLong}" + (!reason.Equals(defaultReason, StringComparison.Ordinal) ? $"\n原因：{reason}" : ""));
        }

        ImGui.TableNextColumn();
        var (color, icon, text) = _uiUtils.GetQuestStyle(questInfo.QuestId);
        _uiUtils.ChecklistItem(text, color, icon);
    }

    private static void DrawCount(int count, int total)
    {
        string len = 9999.ToString(CultureInfo.CurrentCulture);
        ImGui.PushFont(UiBuilder.MonoFont);

        if (total == 0)
            ImGui.TextColored(ImGuiColors.DalamudGrey, $"{"-".PadLeft(len.Length)} / {"-".PadLeft(len.Length)}");
        else
        {
            string text =
                $"{count.ToString(CultureInfo.CurrentCulture).PadLeft(len.Length)} / {total.ToString(CultureInfo.CurrentCulture).PadLeft(len.Length)}";
            if (count == total)
                ImGui.TextColored(ImGuiColors.ParsedGreen, text);
            else
                ImGui.TextUnformatted(text);
        }

        ImGui.PopFont();
    }

    public void UpdateFilter()
    {
        _filteredSections = _journalData.Sections
            .Select(x => FilterSection(x, Filter))
            .Where(x => x.Categories.Count > 0)
            .ToList();

        RefreshCounts();
    }

    private FilteredSection FilterSection(JournalData.Section section, FilterConfiguration filter)
    {
        IEnumerable<FilteredCategory> filteredCategories;
        if (IsCategorySectionGenreMatch(filter, section.Name))
        {
            filteredCategories = section.Categories
                .Select(x => FilterCategory(x, filter.WithoutName()));
        }
        else
        {
            filteredCategories = section.Categories
                .Select(category => FilterCategory(category, filter));
        }

        return new FilteredSection(section, filteredCategories.Where(x => x.Genres.Count > 0).ToList());
    }

    private FilteredCategory FilterCategory(JournalData.Category category, FilterConfiguration filter)
    {
        IEnumerable<FilteredGenre> filteredGenres;
        if (IsCategorySectionGenreMatch(filter, category.Name))
        {
            filteredGenres = category.Genres
                .Select(x => FilterGenre(x, filter.WithoutName()));
        }
        else
        {
            filteredGenres = category.Genres
                .Select(genre => FilterGenre(genre, filter));
        }

        return new FilteredCategory(category, filteredGenres.Where(x => x.Quests.Count > 0).ToList());
    }

    private FilteredGenre FilterGenre(JournalData.Genre genre, FilterConfiguration filter)
    {
        IEnumerable<IQuestInfo> filteredQuests;
        if (IsCategorySectionGenreMatch(filter, genre.Name))
        {
            filteredQuests = genre.Quests
                .Where(x => IsQuestMatch(filter.WithoutName(), x));
        }
        else
        {
            filteredQuests = genre.Quests
                .Where(x => IsQuestMatch(filter, x));
        }

        return new FilteredGenre(genre, filteredQuests.ToList());
    }

    internal void RefreshCounts()
    {
        _genreCounts.Clear();
        _categoryCounts.Clear();
        _sectionCounts.Clear();

        foreach (var genre in _journalData.Genres)
        {
            int available = genre.Quests.Count(x =>
                _questRegistry.TryGetQuest(x.QuestId, out var quest) &&
                !quest.Root.Disabled &&
                !_questFunctions.IsQuestRemoved(x.QuestId));
            int total = genre.Quests.Count(x => !_questFunctions.IsQuestRemoved(x.QuestId));
            int obtainable = genre.Quests.Count(x => !_questFunctions.IsQuestUnobtainable(x.QuestId));
            int completed = genre.Quests.Count(x => _questFunctions.IsQuestComplete(x.QuestId));
            _genreCounts[genre] = new(available, total, obtainable, completed);
        }

        foreach (var category in _journalData.Categories)
        {
            var counts = _genreCounts
                .Where(x => category.Genres.Contains(x.Key))
                .Select(x => x.Value)
                .ToList();
            int available = counts.Sum(x => x.Available);
            int total = counts.Sum(x => x.Total);
            int obtainable = counts.Sum(x => x.Obtainable);
            int completed = counts.Sum(x => x.Completed);
            _categoryCounts[category] = new(available, total, obtainable, completed);
        }

        foreach (var section in _journalData.Sections)
        {
            var counts = _categoryCounts
                .Where(x => section.Categories.Contains(x.Key))
                .Select(x => x.Value)
                .ToList();
            int available = counts.Sum(x => x.Available);
            int total = counts.Sum(x => x.Total);
            int obtainable = counts.Sum(x => x.Obtainable);
            int completed = counts.Sum(x => x.Completed);
            _sectionCounts[section] = new(available, total, obtainable, completed);
        }
    }

    internal void ClearCounts(int type, int code)
    {
        foreach (var genreCount in _genreCounts.ToList())
            _genreCounts[genreCount.Key] = genreCount.Value with { Completed = 0 };

        foreach (var categoryCount in _categoryCounts.ToList())
            _categoryCounts[categoryCount.Key] = categoryCount.Value with { Completed = 0 };

        foreach (var sectionCount in _sectionCounts.ToList())
            _sectionCounts[sectionCount.Key] = sectionCount.Value with { Completed = 0 };
    }

    private static bool IsCategorySectionGenreMatch(FilterConfiguration filter, string name)
    {
        return string.IsNullOrEmpty(filter.SearchText) ||
               name.Contains(filter.SearchText, StringComparison.CurrentCultureIgnoreCase);
    }

    private bool IsQuestMatch(FilterConfiguration filter, IQuestInfo questInfo)
    {
        if (!string.IsNullOrEmpty(filter.SearchText) &&
            !(questInfo.Name.Contains(filter.SearchText, StringComparison.CurrentCultureIgnoreCase) || questInfo.QuestId.ToString() == filter.SearchText))
            return false;

        if (filter.AvailableOnly && !_questFunctions.IsReadyToAcceptQuest(questInfo.QuestId))
            return false;

        if (filter.HideNoPaths &&
            (!_questRegistry.TryGetQuest(questInfo.QuestId, out var quest) || quest.Root.Disabled))
            return false;

        return true;
    }

    private sealed record FilteredSection(JournalData.Section Section, List<FilteredCategory> Categories);

    private sealed record FilteredCategory(JournalData.Category Category, List<FilteredGenre> Genres);

    private sealed record FilteredGenre(JournalData.Genre Genre, List<IQuestInfo> Quests);

    private sealed record JournalCounts(int Available, int Total, int Obtainable, int Completed)
    {
        public JournalCounts()
            : this(0, 0, 0, 0)
        {
        }
    }

    internal sealed class FilterConfiguration
    {
        public string SearchText = string.Empty;
        public bool AvailableOnly;
        public bool HideNoPaths;

        public bool AdvancedFiltersActive => AvailableOnly || HideNoPaths;

        public FilterConfiguration WithoutName() => new()
        {
            AvailableOnly = AvailableOnly,
            HideNoPaths = HideNoPaths
        };
    }
}
