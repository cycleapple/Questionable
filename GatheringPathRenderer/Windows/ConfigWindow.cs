using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;

namespace GatheringPathRenderer.Windows;

internal sealed class ConfigWindow : Window
{
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly Configuration _configuration;

    public ConfigWindow(IDalamudPluginInterface pluginInterface, Configuration configuration)
        : base("採集路線設定", ImGuiWindowFlags.AlwaysAutoResize)
    {
        _pluginInterface = pluginInterface;
        _configuration = configuration;

        AllowPinning = false;
        AllowClickthrough = false;
    }

    public override void Draw()
    {
        string authorName = _configuration.AuthorName;
        if (ImGui.InputText("新檔案的作者名稱", ref authorName, 256))
        {
            _configuration.AuthorName = authorName;
            Save();
        }
    }

    private void Save() => _pluginInterface.SavePluginConfig(_configuration);
}
