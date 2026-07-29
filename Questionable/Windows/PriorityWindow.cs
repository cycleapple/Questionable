using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using LLib.ImGui;
using Questionable.Controller;
using Questionable.Functions;
using Questionable.Model;
using Questionable.Model.Questing;
using Questionable.Windows.QuestComponents;
using Questionable.Windows.Utils;

namespace Questionable.Windows;

internal sealed class PriorityWindow : LWindow
{
    private const string ClipboardPrefix = "qst:priority:";
    private const string LegacyClipboardPrefix = "qst:v1:";
    private const char ClipboardSeparator = ';';

    private readonly QuestController _questController;
    private readonly QuestFunctions _questFunctions;
    private readonly QuestSelector _questSelector;
    private readonly QuestTooltipComponent _questTooltipComponent;
    private readonly UiUtils _uiUtils;
    private readonly IChatGui _chatGui;
    private readonly IDalamudPluginInterface _pluginInterface;

    private ElementId? _draggedItem;

    public PriorityWindow(QuestController questController, QuestFunctions questFunctions, QuestSelector questSelector,
        QuestTooltipComponent questTooltipComponent, UiUtils uiUtils, IChatGui chatGui,
        IDalamudPluginInterface pluginInterface)
        : base("任務優先順序###QuestionableQuestPriority")
    {
        _questController = questController;
        _questFunctions = questFunctions;
        _questSelector = questSelector;
        _questTooltipComponent = questTooltipComponent;
        _uiUtils = uiUtils;
        _chatGui = chatGui;
        _pluginInterface = pluginInterface;

        _questSelector.SuggestionPredicate = quest =>
            !quest.Info.IsMainScenarioQuest &&
            !questFunctions.IsQuestUnobtainable(quest.Id) &&
            questController.ManualPriorityQuests.All(x => x.Id != quest.Id);
        _questSelector.DefaultPredicate = quest => questFunctions.IsQuestAccepted(quest.Id);
        _questSelector.QuestSelected = quest => _questController.ManualPriorityQuests.Add(quest);

        Size = new Vector2(400, 400);
        SizeCondition = ImGuiCond.Once;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(400, 30),
            MaximumSize = default
        };
    }

    public override void DrawContent()
    {

        if (ImGui.CollapsingHeader("說明"))
        {
            ImGui.TextWrapped(
                "Questionable 通常會依照以下順序執行：");
            ImGui.BulletText("下方手動加入的優先任務（依排列順序）");
            ImGui.BulletText("系統判定的「優先」任務：職業任務、2.0 蠻神與團隊任務");
            ImGui.BulletText(
                "「待辦清單」中支援的任務\n（任務情報中固定顯示於畫面的任務）");
            ImGui.BulletText("主線任務（若有，且未在任務日誌中標記為「忽略」）");
            ImGui.TextWrapped(
                "若目前沒有進行中的主線任務，且此處也未加入優先任務，Questionable 會優先接取下一個主線任務。");
        }
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.Text("優先執行的任務：");
        _questSelector.DrawSelection();
        DrawQuestList();

        List<ElementId> clipboardItems = ParseClipboardItems();
        ImGui.BeginDisabled(clipboardItems.Count == 0);
        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Download, "從剪貼簿匯入"))
            ImportFromClipboard(clipboardItems);
        ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.BeginDisabled(_questController.ManualPriorityQuests.Count == 0);
        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Upload, "匯出至剪貼簿"))
            ExportToClipboard();
        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Check, "移除已完成任務"))
            _questController.ManualPriorityQuests.RemoveAll(q => _questFunctions.IsQuestComplete(q.Id));
        ImGui.SameLine();

        using (ImRaii.Disabled(!ImGui.IsKeyDown(ImGuiKey.ModCtrl)))
        {
            if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Trash, "全部清除"))
                _questController.ClearQuestPriority();
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("按住 Ctrl 即可使用此按鈕。");

        ImGui.EndDisabled();
    }

    private void DrawQuestList()
    {
        List<Quest> priorityQuests = _questController.ManualPriorityQuests;
        Quest? itemToRemove = null;
        Quest? itemToAdd = null;
        int indexToAdd = 0;

        float width = ImGui.GetContentRegionAvail().X;
        List<(Vector2 TopLeft, Vector2 BottomRight)> itemPositions = [];

        for (int i = 0; i < priorityQuests.Count; ++i)
        {
            Vector2 topLeft = ImGui.GetCursorScreenPos() +
                              new Vector2(0, -ImGui.GetStyle().ItemSpacing.Y / 2);
            var quest = priorityQuests[i];
            using (ImRaii.PushId($"Quest{quest.Id}"))
            {
                var style = _uiUtils.GetQuestStyle(quest.Id);
                bool hovered;
                using (var _ = _pluginInterface.UiBuilder.IconFontFixedWidthHandle.Push())
                {
                    ImGui.AlignTextToFramePadding();
                    ImGui.TextColored(style.Color, style.Icon.ToIconString());
                    hovered = ImGui.IsItemHovered();
                }

                ImGui.SameLine();
                ImGui.AlignTextToFramePadding();
                ImGui.Text(quest.Info.Name);
                hovered |= ImGui.IsItemHovered();

                if (hovered)
                    _questTooltipComponent.Draw(quest.Info);

                if (priorityQuests.Count > 1)
                {
                    using (ImRaii.PushFont(UiBuilder.IconFont))
                    {
                        ImGui.SameLine(ImGui.GetContentRegionAvail().X +
                                       ImGui.GetStyle().WindowPadding.X -
                                       ImGui.CalcTextSize(FontAwesomeIcon.ArrowsUpDown.ToIconString()).X -
                                       ImGui.CalcTextSize(FontAwesomeIcon.Times.ToIconString()).X -
                                       ImGui.GetStyle().FramePadding.X * 4 -
                                       ImGui.GetStyle().ItemSpacing.X);
                    }

                    if (_draggedItem == quest.Id)
                    {
                        ImGuiComponents.IconButton("##Move", FontAwesomeIcon.ArrowsUpDown,
                            ImGui.ColorConvertU32ToFloat4(ImGui.GetColorU32(ImGuiCol.ButtonActive)));
                    }
                    else
                        ImGuiComponents.IconButton("##Move", FontAwesomeIcon.ArrowsUpDown);

                    if (_draggedItem == null && ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
                        _draggedItem = quest.Id;

                    ImGui.SameLine();
                }
                else
                {
                    using (ImRaii.PushFont(UiBuilder.IconFont))
                    {
                        ImGui.SameLine(ImGui.GetContentRegionAvail().X +
                                       ImGui.GetStyle().WindowPadding.X -
                                       ImGui.CalcTextSize(FontAwesomeIcon.Times.ToIconString()).X -
                                       ImGui.GetStyle().FramePadding.X * 2);
                    }
                }

                if (ImGuiComponents.IconButton($"##Remove{i}", FontAwesomeIcon.Times))
                    itemToRemove = quest;
            }

            Vector2 bottomRight = new Vector2(topLeft.X + width,
                ImGui.GetCursorScreenPos().Y - ImGui.GetStyle().ItemSpacing.Y + 2);
            itemPositions.Add((topLeft, bottomRight));
        }

        if (!ImGui.IsMouseDragging(ImGuiMouseButton.Left))
            _draggedItem = null;
        else if (_draggedItem != null)
        {
            var draggedItem = priorityQuests.Single(x => x.Id == _draggedItem);
            int oldIndex = priorityQuests.IndexOf(draggedItem);

            var (topLeft, bottomRight) = itemPositions[oldIndex];
            ImGui.GetWindowDrawList().AddRect(topLeft, bottomRight, ImGui.GetColorU32(ImGuiColors.DalamudGrey), 3f,
                ImDrawFlags.RoundCornersAll);

            int newIndex = itemPositions.FindIndex(x => ImGui.IsMouseHoveringRect(x.TopLeft, x.BottomRight, true));
            if (newIndex >= 0 && oldIndex != newIndex)
            {
                itemToAdd = priorityQuests.Single(x => x.Id == _draggedItem);
                indexToAdd = newIndex;
            }
        }

        if (itemToRemove != null)
        {
            priorityQuests.Remove(itemToRemove);
        }

        if (itemToAdd != null)
        {
            priorityQuests.Remove(itemToAdd);
            priorityQuests.Insert(indexToAdd, itemToAdd);
        }
    }

    private static List<ElementId> ParseClipboardItems()
    {
        string clipboardText = ImGui.GetClipboardText().Trim();
        return DecodeQuestPriority(clipboardText);
    }

    public static List<ElementId> DecodeQuestPriority(string clipboardText)
    {
        List<ElementId> clipboardItems = [];
        try
        {
            if (!string.IsNullOrEmpty(clipboardText))
            {
                string? prefixToRemove = null;

                if (clipboardText.StartsWith(ClipboardPrefix, StringComparison.InvariantCulture))
                {
                    prefixToRemove = ClipboardPrefix;
                }
                else if (clipboardText.StartsWith(LegacyClipboardPrefix, StringComparison.InvariantCulture))
                {
                    prefixToRemove = LegacyClipboardPrefix;
                }

                if (prefixToRemove != null)
                {
                    clipboardText = clipboardText.Substring(prefixToRemove.Length);
                    string text = Encoding.UTF8.GetString(Convert.FromBase64String(clipboardText));
                    foreach (string part in text.Split(ClipboardSeparator))
                    {
                        ElementId elementId = ElementId.FromString(part);
                        clipboardItems.Add(elementId);
                    }
                }
            }
        }
        catch (Exception)
        {
            clipboardItems.Clear();
        }

        return clipboardItems;
    }

    public string EncodeQuestPriority()
    {
        return ClipboardPrefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(
            string.Join(ClipboardSeparator, _questController.ManualPriorityQuests.Select(x => x.Id.ToString()))));
    }

    private void ExportToClipboard()
    {
        string clipboardText = EncodeQuestPriority();
        ImGui.SetClipboardText(clipboardText);
        _chatGui.Print("已將任務清單複製到剪貼簿。", CommandHandler.MessageTag, CommandHandler.TagColor);
    }

    private void ImportFromClipboard(List<ElementId> questElements)
    {
        _questController.ImportQuestPriority(questElements);
    }
}
