using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace OccultWeakness;

internal sealed class ConfigWindow : IDisposable
{
    private readonly Plugin plugin;
    private readonly bool[] selectedElements = new bool[Enum.GetValues<Element>().Length];
    private string newMobName = string.Empty;

    internal bool IsOpen { get; set; }
    internal bool PositionEditorEnabled { get; private set; }

    internal ConfigWindow(Plugin plugin)
    {
        this.plugin = plugin;
        selectedElements[(int)Element.Fire] = true;
    }

    public void Dispose() { }

    internal void Draw()
    {
        if (!IsOpen)
            return;

        ImGui.SetNextWindowSize(new Vector2(760, 560) * ImGuiHelpers.GlobalScale, ImGuiCond.FirstUseEver);
        var isOpen = IsOpen;
        if (!ImGui.Begin("Occult Weakness###OccultWeaknessConfig", ref isOpen))
        {
            IsOpen = isOpen;
            ImGui.End();
            return;
        }
        IsOpen = isOpen;

        DrawGeneralSettings();
        ImGui.Separator();
        DrawOverlaySettings();
        ImGui.Separator();
        DrawCurrentTargetSection();
        ImGui.Separator();
        DrawManualAddSection();
        ImGui.Separator();
        DrawMobTable();
        ImGui.Separator();
        DrawIconSettings();

        ImGui.End();
    }

    private void DrawGeneralSettings()
    {
        var enabled = plugin.Configuration.Enabled;
        if (ImGui.Checkbox("Activer le plugin", ref enabled))
        {
            plugin.Configuration.Enabled = enabled;
            plugin.Save();
        }
    }

    private void DrawOverlaySettings()
    {
        ImGui.TextUnformatted("Position et apparence sur l’interface");
        ImGui.TextDisabled("Les icônes restent fixes à l’écran, près de la barre de cible.");

        var iconSize = plugin.Configuration.OverlayIconSize;
        ImGui.SetNextItemWidth(230 * ImGuiHelpers.GlobalScale);
        if (ImGui.SliderFloat("Taille des icônes", ref iconSize, 12f, 64f, "%.0f px"))
        {
            plugin.Configuration.OverlayIconSize = iconSize;
            plugin.Save();
        }

        var spacing = plugin.Configuration.OverlayIconSpacing;
        ImGui.SetNextItemWidth(230 * ImGuiHelpers.GlobalScale);
        if (ImGui.SliderFloat("Espacement", ref spacing, 0f, 20f, "%.0f px"))
        {
            plugin.Configuration.OverlayIconSpacing = spacing;
            plugin.Save();
        }

        var editor = PositionEditorEnabled;
        if (ImGui.Checkbox("Mode placement : glisser les icônes", ref editor))
            PositionEditorEnabled = editor;

        ImGui.SameLine();
        if (ImGui.Button("Réinitialiser la position"))
            plugin.ResetOverlayPosition();

        var offsetX = plugin.Configuration.OverlayOffsetX;
        ImGui.SetNextItemWidth(230 * ImGuiHelpers.GlobalScale);
        if (ImGui.SliderFloat("Position horizontale", ref offsetX, -900f, 900f, "%.0f px"))
        {
            plugin.Configuration.OverlayOffsetX = offsetX;
            plugin.Save();
        }

        var offsetY = plugin.Configuration.OverlayOffsetY;
        ImGui.SetNextItemWidth(230 * ImGuiHelpers.GlobalScale);
        if (ImGui.SliderFloat("Position verticale", ref offsetY, 0f, 700f, "%.0f px"))
        {
            plugin.Configuration.OverlayOffsetY = offsetY;
            plugin.Save();
        }

        var background = plugin.Configuration.DrawIconBackground;
        if (ImGui.Checkbox("Fond sombre derrière les icônes", ref background))
        {
            plugin.Configuration.DrawIconBackground = background;
            plugin.Save();
        }

        ImGui.TextDisabled("Cible un mob enregistré, active le mode placement, puis glisse la rangée à gauche de ta barre de cible.");
    }

    private void DrawCurrentTargetSection()
    {
        ImGui.TextUnformatted("Cible actuelle");
        var target = Plugin.TargetManager.Target;
        if (target is null)
        {
            ImGui.TextDisabled("Aucune cible.");
            return;
        }

        ImGui.TextUnformatted(target.Name.TextValue);
        DrawElementCheckboxes("CurrentTarget", selectedElements);
        if (ImGui.Button("Ajouter / mettre à jour la cible"))
            plugin.AddOrUpdateCurrentTarget(GetSelectedElements());
    }

    private void DrawManualAddSection()
    {
        ImGui.TextUnformatted("Ajout manuel");
        ImGui.SetNextItemWidth(280 * ImGuiHelpers.GlobalScale);
        ImGui.InputText("Nom du mob##NewMob", ref newMobName, 128);

        DrawElementCheckboxes("NewMob", selectedElements);
        if (ImGui.Button("Ajouter manuellement") && !string.IsNullOrWhiteSpace(newMobName))
        {
            plugin.Configuration.Mobs.Add(new MobWeaknessEntry
            {
                Name = newMobName.Trim(),
                Weaknesses = GetSelectedElements(),
                Enabled = true
            });
            newMobName = string.Empty;
            plugin.Save();
        }
    }

    private void DrawMobTable()
    {
        ImGui.TextUnformatted($"Monstres enregistrés ({plugin.Configuration.Mobs.Count})");

        var flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg |
                    ImGuiTableFlags.Resizable | ImGuiTableFlags.ScrollY;

        if (!ImGui.BeginTable("MobWeaknessTable", 4, flags, new Vector2(0, 235 * ImGuiHelpers.GlobalScale)))
            return;

        ImGui.TableSetupColumn("Actif", ImGuiTableColumnFlags.WidthFixed, 55 * ImGuiHelpers.GlobalScale);
        ImGui.TableSetupColumn("Nom");
        ImGui.TableSetupColumn("Faiblesses", ImGuiTableColumnFlags.WidthFixed, 245 * ImGuiHelpers.GlobalScale);
        ImGui.TableSetupColumn("Supprimer", ImGuiTableColumnFlags.WidthFixed, 80 * ImGuiHelpers.GlobalScale);
        ImGui.TableHeadersRow();

        var removeIndex = -1;
        for (var i = 0; i < plugin.Configuration.Mobs.Count; i++)
        {
            var entry = plugin.Configuration.Mobs[i];
            entry.Weaknesses ??= new List<Element>();
            ImGui.PushID(i);
            ImGui.TableNextRow();

            ImGui.TableSetColumnIndex(0);
            var enabled = entry.Enabled;
            if (ImGui.Checkbox("##Enabled", ref enabled))
            {
                entry.Enabled = enabled;
                plugin.Save();
            }

            ImGui.TableSetColumnIndex(1);
            var name = entry.Name;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputText("##Name", ref name, 128))
            {
                entry.Name = name;
                plugin.Save();
            }

            ImGui.TableSetColumnIndex(2);
            var changed = false;
            foreach (var element in ElementExtensions.SupportedElements)
            {
                var selected = entry.Weaknesses.Contains(element);
                if (ImGui.Checkbox($"{element.ShortName()}##{element}", ref selected))
                {
                    if (selected) entry.Weaknesses.Add(element);
                    else entry.Weaknesses.Remove(element);
                    changed = true;
                }
                if (element != Element.Lightning) ImGui.SameLine();
            }
            if (changed) plugin.Save();

            ImGui.TableSetColumnIndex(3);
            if (ImGui.Button("X##Delete"))
                removeIndex = i;

            ImGui.PopID();
        }

        ImGui.EndTable();

        if (removeIndex >= 0)
        {
            plugin.Configuration.Mobs.RemoveAt(removeIndex);
            plugin.Save();
        }
    }

    private static void DrawElementCheckboxes(string id, bool[] values)
    {
        foreach (var element in ElementExtensions.SupportedElements)
        {
            var value = values[(int)element];
            if (ImGui.Checkbox($"{element.DisplayName()}##{id}{element}", ref value))
                values[(int)element] = value;
            if (element != Element.Lightning) ImGui.SameLine();
        }
    }

    private List<Element> GetSelectedElements()
    {
        var result = new List<Element>();
        foreach (var element in ElementExtensions.SupportedElements)
            if (selectedElements[(int)element]) result.Add(element);
        if (result.Count == 0) result.Add(Element.Fire);
        return result;
    }

    private void DrawIconSettings()
    {
        if (!ImGui.CollapsingHeader("Icônes avancées"))
            return;

        ImGui.TextWrapped("Les valeurs sont des Game Icon IDs. Par défaut, le plugin utilise les icônes des quatre éclats élémentaires disponibles.");
        foreach (var element in ElementExtensions.SupportedElements)
        {
            plugin.Configuration.IconIds.TryGetValue(element, out var iconId);
            var value = (int)iconId;
            ImGui.SetNextItemWidth(150 * ImGuiHelpers.GlobalScale);
            if (ImGui.InputInt($"{element.DisplayName()}##Icon{element}", ref value))
            {
                plugin.Configuration.IconIds[element] = (uint)Math.Max(0, value);
                plugin.Save();
            }
        }
    }

    private static void HelpMarker(string text)
    {
        ImGui.TextDisabled("(?)");
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.PushTextWrapPos(420 * ImGuiHelpers.GlobalScale);
            ImGui.TextUnformatted(text);
            ImGui.PopTextWrapPos();
            ImGui.EndTooltip();
        }
    }
}
