using System.Globalization;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Common.Math;
using LLib.ImGui;
using Questionable.Controller;
using Questionable.Data;
using Questionable.Model;
using Questionable.Validation;

namespace Questionable.Windows;

internal sealed class QuestValidationWindow : LWindow
{
    private readonly QuestValidator _questValidator;
    private readonly QuestData _questData;
    private readonly QuestController _questController;
    private readonly IDalamudPluginInterface _pluginInterface;

    public QuestValidationWindow(QuestValidator questValidator, QuestData questData,
        QuestController questController, IDalamudPluginInterface pluginInterface)
        : base("任務驗證###QuestionableValidator")
    {
        _questValidator = questValidator;
        _questData = questData;
        _questController = questController;
        _pluginInterface = pluginInterface;

        Size = new Vector2(600, 200);
        SizeCondition = ImGuiCond.Once;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(600, 200),
        };
    }

    public override void DrawContent()
    {
        using var table = ImRaii.Table("QuestSelection", 5, ImGuiTableFlags.Borders | ImGuiTableFlags.ScrollY);
        if (!table)
        {
            ImGui.Text("無法建立驗證清單。");
            return;
        }

        ImGui.TableSetupColumn("任務", ImGuiTableColumnFlags.WidthFixed, 50);
        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 200);
        ImGui.TableSetupColumn("序列", ImGuiTableColumnFlags.WidthFixed, 30);
        ImGui.TableSetupColumn("步驟", ImGuiTableColumnFlags.WidthFixed, 30);
        ImGui.TableSetupColumn("問題", ImGuiTableColumnFlags.None, 200);
        ImGui.TableHeadersRow();

        foreach (ValidationIssue validationIssue in _questValidator.Issues)
        {
            ImGui.TableNextRow();

            if (ImGui.TableNextColumn())
            {
                ImGui.TextUnformatted(validationIssue.ElementId?.ToString() ?? string.Empty);

                if (validationIssue.ElementId != null)
                {
                    IQuestInfo quest = _questData.GetQuestInfo(validationIssue.ElementId);
                    bool copy = ImGuiComponents.IconButton(FontAwesomeIcon.Copy);
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("複製為檔案名稱");
                    if (copy)
                    {
                        string fileName = $"{quest.QuestId}_{quest.SimplifiedName}.json";
                        ImGui.SetClipboardText(fileName);
                    }
                    ImGui.SameLine();
                    bool sim = ImGuiComponents.IconButton(FontAwesomeIcon.Play, new System.Numerics.Vector2(16));
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("模擬任務");
                    if (sim)
                    {
                        _questController.SimulateQuest(quest, validationIssue.Sequence ?? 0, 0);
                    }
                }
            }

            if (ImGui.TableNextColumn())
                ImGui.TextUnformatted(validationIssue.ElementId != null
                    ? _questData.GetQuestInfo(validationIssue.ElementId).Name
                    : validationIssue.AlliedSociety.ToString());

            if (ImGui.TableNextColumn())
                ImGui.TextUnformatted(validationIssue.Sequence?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);

            if (ImGui.TableNextColumn())
                ImGui.TextUnformatted(validationIssue.Step?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);

            if (ImGui.TableNextColumn())
            {
                // ReSharper disable once UnusedVariable
                using (var font = _pluginInterface.UiBuilder.IconFontFixedWidthHandle.Push())
                {
                    if (validationIssue.Severity == EIssueSeverity.Error)
                    {
                        using var color = ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudRed);
                        ImGui.TextUnformatted(FontAwesomeIcon.ExclamationTriangle.ToIconString());
                    }
                    else
                    {
                        using var color = ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.ParsedBlue);
                        ImGui.TextUnformatted(FontAwesomeIcon.InfoCircle.ToIconString());
                    }
                }

                ImGui.SameLine();
                ImGui.TextUnformatted(validationIssue.Description);
            }
        }
    }
}
