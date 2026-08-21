using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Command;
using Dalamud.Interface.Textures;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace OccultWeakness;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/ocweak";

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static ITargetManager TargetManager { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    internal Configuration Configuration { get; }
    private readonly ConfigWindow configWindow;
    private Vector2 lastSavedOverlayPosition;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        MigrateConfiguration();
        EnsureDefaultIcons();

        configWindow = new ConfigWindow(this);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Ouvre la configuration d’Occult Weakness."
        });

        PluginInterface.UiBuilder.Draw += DrawUi;
        PluginInterface.UiBuilder.OpenConfigUi += OpenConfig;
        PluginInterface.UiBuilder.OpenMainUi += OpenConfig;
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= DrawUi;
        PluginInterface.UiBuilder.OpenConfigUi -= OpenConfig;
        PluginInterface.UiBuilder.OpenMainUi -= OpenConfig;
        CommandManager.RemoveHandler(CommandName);
        configWindow.Dispose();
    }

    private void DrawUi()
    {
        DrawWeaknessHudOverlay();
        configWindow.Draw();
    }

    private void OpenConfig() => configWindow.IsOpen = true;
    private void OnCommand(string command, string args) => configWindow.IsOpen = !configWindow.IsOpen;

    private void DrawWeaknessHudOverlay()
    {
        if (!Configuration.Enabled)
            return;

        var target = TargetManager.Target;
        if (target is null || !target.IsValid() || !target.IsTargetable)
            return;

        var entry = FindEntry(target);
        if (entry?.Weaknesses is not { Count: > 0 })
            return;

        var weaknesses = entry.Weaknesses.Where(element => element.IsSupported()).ToList();
        if (weaknesses.Count == 0)
            return;

        var iconSize = Math.Clamp(Configuration.OverlayIconSize, 12f, 64f);
        var spacing = Math.Clamp(Configuration.OverlayIconSpacing, 0f, 20f);
        var totalWidth = weaknesses.Count * iconSize + Math.Max(0, weaknesses.Count - 1) * spacing;
        var totalSize = new Vector2(totalWidth, iconSize);

        var viewport = ImGui.GetMainViewport();
        var defaultPosition = viewport.WorkPos + new Vector2(
            viewport.WorkSize.X * 0.5f + Configuration.OverlayOffsetX,
            Configuration.OverlayOffsetY);

        ImGui.SetNextWindowPos(defaultPosition, ImGuiCond.Always);
        ImGui.SetNextWindowSize(totalSize + new Vector2(12f, 12f), ImGuiCond.Always);

        var flags = ImGuiWindowFlags.NoDecoration |
                    ImGuiWindowFlags.NoSavedSettings |
                    ImGuiWindowFlags.NoFocusOnAppearing |
                    ImGuiWindowFlags.NoNav |
                    ImGuiWindowFlags.NoScrollbar |
                    ImGuiWindowFlags.NoScrollWithMouse;

        if (!configWindow.PositionEditorEnabled)
            flags |= ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoBackground;

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(6f, 6f));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 4f);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0f, 0f, 0f, 0.55f));

        var actualPosition = defaultPosition;
        var open = true;
        if (ImGui.Begin("Occult Weakness HUD###OccultWeaknessHudOverlay", ref open, flags))
        {
            var windowPos = ImGui.GetWindowPos();
            actualPosition = windowPos;
            var drawList = ImGui.GetWindowDrawList();
            var start = windowPos + new Vector2(6f, 6f);

            for (var i = 0; i < weaknesses.Count; i++)
            {
                var element = weaknesses[i];
                if (!Configuration.IconIds.TryGetValue(element, out var iconId) || iconId == 0)
                    continue;

                try
                {
                    var sharedTexture = TextureProvider.GetFromGameIcon(new GameIconLookup(iconId));
                    if (!sharedTexture.TryGetWrap(out var textureWrap, out _))
                        continue;

                    var min = start + new Vector2(i * (iconSize + spacing), 0f);
                    var max = min + new Vector2(iconSize, iconSize);

                    if (Configuration.DrawIconBackground && !configWindow.PositionEditorEnabled)
                        drawList.AddRectFilled(min - Vector2.One, max + Vector2.One, 0xA0000000, 3f);

                    drawList.AddImage(textureWrap.Handle, min, max);
                }
                catch (Exception ex)
                {
                    Log.Verbose(ex, "Impossible de dessiner l’icône {IconId}.", iconId);
                }
            }

            if (configWindow.PositionEditorEnabled)
            {
                drawList.AddRect(windowPos, windowPos + totalSize + new Vector2(12f, 12f), 0xFFFFFFFF, 4f, ImDrawFlags.None, 1f);
                ImGui.SetCursorPos(new Vector2(6f, iconSize + 8f));
                ImGui.TextDisabled("Glisse pour placer");
            }
        }
        ImGui.End();

        ImGui.PopStyleColor();
        ImGui.PopStyleVar(2);

        if (configWindow.PositionEditorEnabled)
        {
            var newOffset = new Vector2(
                actualPosition.X - viewport.WorkPos.X - viewport.WorkSize.X * 0.5f,
                actualPosition.Y - viewport.WorkPos.Y);

            if (Vector2.DistanceSquared(newOffset, lastSavedOverlayPosition) > 0.25f)
            {
                Configuration.OverlayOffsetX = newOffset.X;
                Configuration.OverlayOffsetY = newOffset.Y;
                lastSavedOverlayPosition = newOffset;
                Configuration.Save();
            }
        }
    }

    internal MobWeaknessEntry? FindEntry(IGameObject gameObject)
    {
        var comparison = Configuration.CaseSensitiveNames
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        var name = gameObject.Name.TextValue.Trim();
        return Configuration.Mobs.FirstOrDefault(entry =>
            entry.Enabled &&
            string.Equals(entry.Name.Trim(), name, comparison));
    }

    internal void AddOrUpdateCurrentTarget(List<Element> weaknesses)
    {
        var target = TargetManager.Target;
        if (target is null)
            return;

        var name = target.Name.TextValue.Trim();
        var existing = Configuration.Mobs.FirstOrDefault(x =>
            string.Equals(x.Name.Trim(), name, StringComparison.OrdinalIgnoreCase));

        if (existing is null)
        {
            Configuration.Mobs.Add(new MobWeaknessEntry
            {
                Name = name,
                Weaknesses = weaknesses.ToList(),
                Enabled = true
            });
        }
        else
        {
            existing.Name = name;
            existing.Weaknesses = weaknesses.ToList();
            existing.Enabled = true;
        }

        Configuration.Save();
    }

    internal void ResetOverlayPosition()
    {
        Configuration.OverlayOffsetX = -330f;
        Configuration.OverlayOffsetY = 90f;
        lastSavedOverlayPosition = new Vector2(Configuration.OverlayOffsetX, Configuration.OverlayOffsetY);
        Configuration.Save();
    }

    internal void Save() => Configuration.Save();

    private void MigrateConfiguration()
    {
        var changed = false;

        if (Configuration.Version < 4)
        {
            Configuration.OverlayOffsetX = -330f;
            Configuration.OverlayOffsetY = 90f;
            changed = true;
        }

        if (Configuration.Version < 5)
        {
            foreach (var entry in Configuration.Mobs)
            {
                entry.Weaknesses ??= new List<Element>();
                if (entry.Weaknesses.RemoveAll(element => !element.IsSupported()) > 0)
                    changed = true;
            }

            if (Configuration.IconIds.Remove(Element.Earth))
                changed = true;
            if (Configuration.IconIds.Remove(Element.Water))
                changed = true;
        }

        if (Configuration.Version < 6)
        {
            Configuration.Version = 6;
            changed = true;
        }

        if (Configuration.Version < 7)
        {
            var merged = Configuration.Mobs
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Name))
                .GroupBy(entry => entry.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(group => new MobWeaknessEntry
                {
                    Name = group.First().Name.Trim(),
                    Enabled = group.Any(entry => entry.Enabled),
                    Weaknesses = group
                        .SelectMany(entry => entry.Weaknesses ?? new List<Element>())
                        .Where(element => element.IsSupported())
                        .Distinct()
                        .ToList()
                })
                .ToList();

            Configuration.Mobs = merged;
            Configuration.Version = 7;
            changed = true;
        }

        lastSavedOverlayPosition = new Vector2(Configuration.OverlayOffsetX, Configuration.OverlayOffsetY);

        if (changed)
            Configuration.Save();
    }

    private void EnsureDefaultIcons()
    {
        var itemIds = new Dictionary<Element, uint>
        {
            [Element.Fire] = 2,
            [Element.Ice] = 3,
            [Element.Wind] = 4,
            [Element.Lightning] = 6
        };

        var itemSheet = DataManager.GetExcelSheet<Item>();
        var changed = false;

        foreach (var (element, itemId) in itemIds)
        {
            if (Configuration.IconIds.TryGetValue(element, out var existing) && existing != 0)
                continue;

            if (itemSheet.TryGetRow(itemId, out var item))
            {
                Configuration.IconIds[element] = item.Icon;
                changed = true;
            }
        }

        if (changed)
            Configuration.Save();
    }
}
