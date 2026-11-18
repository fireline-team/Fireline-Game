using System;
using System.Collections.Generic;
using Fireline.Missions;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Mission checklist presentation; mission owns the clock and completion state.</summary>
public sealed class MinimapMissionHud : IDisposable
{
    public readonly struct Entry
    {
        public readonly string Label;
        public readonly bool Complete, Child;
        public readonly char Symbol;
        public readonly Color Color;
        public Entry(string label, bool complete, bool child, char symbol, Color color)
        { Label = label; Complete = complete; Child = child; Symbol = symbol; Color = color; }
    }
    private sealed class Row { public RectTransform Rect; public RawImage Icon; public TextMeshProUGUI Text; }
    private readonly RectTransform timerPanel, listPanel, viewport, content;
    private readonly TextMeshProUGUI timer;
    private readonly List<Row> rows = new List<Row>();
    private readonly List<Entry> entries = new List<Entry>();
    private readonly List<ObjectiveChecklistItem> targets = new List<ObjectiveChecklistItem>();
    private readonly HashSet<MissionObjectiveSite> visited = new HashSet<MissionObjectiveSite>();
    private readonly Dictionary<char, Texture2D> glyphs = new Dictionary<char, Texture2D>();
    private FireRescueMission current;
    private int runVersion = -1;
    private float scroll, maxScroll;
    private bool extracting;
    private int focusRow = -1;
    public IReadOnlyList<Entry> Entries => entries;
    public string TimerText => timer.text;
    public static string FormatRemaining(float duration, float elapsed)
    {
        int seconds = Mathf.CeilToInt(Mathf.Max(0, duration - elapsed));
        return $"{seconds / 60:00}:{seconds % 60:00}";
    }

    public MinimapMissionHud(Transform parent)
    {
        timerPanel = Panel("Mission countdown", parent);
        timer = Text("Time remaining", timerPanel, 36);
        timer.alignment = TextAlignmentOptions.Center;
        Stretch(timer.rectTransform, 8);
        listPanel = Panel("Mission checklist", parent);
        viewport = Rect("Checklist viewport", listPanel);
        Stretch(viewport, 8);
        viewport.gameObject.AddComponent<RectMask2D>();
        content = Rect("Checklist rows", viewport);
        content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 1);
        glyphs['O'] = MinimapSymbols.CreateTexture('O', MinimapSymbols.Objective);
        glyphs['F'] = MinimapSymbols.CreateTexture('F', MinimapSymbols.Fire);
        glyphs['D'] = MinimapSymbols.CreateTexture('D', MinimapSymbols.Crate);
        glyphs['E'] = MinimapSymbols.CreateTexture('E', MinimapSymbols.Evac);
    }

    public void Refresh(FireRescueMission mission, float mapSize, float revealDistance, float duration, int timerFontSize, int objectiveFontSize, int subtaskFontSize)
    {
        bool show = mission != null && mission.State != FireRescueMission.Phase.NotStarted
            && mission.State != FireRescueMission.Phase.InvalidSetup;
        timerPanel.gameObject.SetActive(show); listPanel.gameObject.SetActive(show);
        if (!show) return;
        if (current != mission || runVersion != mission.RunVersion)
        {
            current = mission; runVersion = mission.RunVersion;
            visited.Clear(); scroll = 0; extracting = false;
        }
        timer.enableAutoSizing = false;
        timer.fontSize = Mathf.Clamp(timerFontSize, 16, 96);
        timer.text = FormatRemaining(duration, mission.ElapsedSeconds);
        timer.color = duration - mission.ElapsedSeconds <= 60 ? new Color(1, 0.45f, 0.3f) : Color.white;
        BuildEntries(mission, revealDistance);
        float timerHeight = Mathf.Max(56, timer.GetPreferredValues(timer.text, Mathf.Max(48, mapSize - 16), 1000).y + 16);
        float available = Mathf.Max(40, Screen.height - mapSize - timerHeight - 64);
        float height = 0, focusOffset = -1;
        for (int i = 0; i < entries.Count; i++)
        {
            if (i == rows.Count) rows.Add(CreateRow());
            var row = rows[i]; var entry = entries[i];
            if (i == focusRow) focusOffset = height;
            row.Rect.gameObject.SetActive(true);
            float indent = entry.Child ? 18 : 0;
            string label = entry.Label.Replace("<", "‹").Replace(">", "›");
            row.Text.text = entry.Complete
                ? "<s color=#FF4D57><b>" + label + "</b></s>"
                    + (entry.Child ? "" : " <color=#FF7880><size=75%>COMPLETE</size></color>")
                : label;
            row.Text.color = entry.Complete ? new Color(0.78f, 0.8f, 0.82f) : Color.white;
            row.Text.enableAutoSizing = false;
            row.Text.fontSize = Mathf.Clamp(entry.Child ? subtaskFontSize : objectiveFontSize, 12, 64);
            float width = Mathf.Max(48, mapSize - 52 - indent);
            float rowHeight = Mathf.Max(36, row.Text.GetPreferredValues(row.Text.text, width, 1000).y + 8);
            row.Rect.anchoredPosition = new Vector2(0, -height);
            row.Rect.sizeDelta = new Vector2(0, rowHeight);
            row.Icon.texture = glyphs.TryGetValue(entry.Symbol, out var glyph) ? glyph : glyphs['O'];
            row.Icon.color = entry.Complete ? new Color(0.6f, 0.6f, 0.6f) : Color.white;
            row.Icon.rectTransform.anchoredPosition = new Vector2(indent + 2, -6);
            row.Text.rectTransform.offsetMin = new Vector2(indent + 28, 0);
            row.Text.rectTransform.offsetMax = new Vector2(-2, 0);
            height += rowHeight;
        }
        for (int i = entries.Count; i < rows.Count; i++) rows[i].Rect.gameObject.SetActive(false);
        float listHeight = Mathf.Min(height + 16, available);
        listPanel.sizeDelta = new Vector2(mapSize, listHeight);
        listPanel.anchoredPosition = new Vector2(-18, mapSize + 26);
        timerPanel.sizeDelta = new Vector2(mapSize, timerHeight);
        timerPanel.anchoredPosition = new Vector2(-18, mapSize + listHeight + 34);
        content.sizeDelta = new Vector2(0, height);
        maxScroll = Mathf.Max(0, height - (listHeight - 16));
        if (focusOffset >= 0) scroll = Mathf.Clamp(focusOffset, 0, maxScroll);
        bool nowExtracting = mission.State == FireRescueMission.Phase.Evacuate || mission.State == FireRescueMission.Phase.Complete;
        if (nowExtracting && !extracting) scroll = maxScroll;
        extracting = nowExtracting;
        scroll = Mathf.Clamp(scroll, 0, maxScroll);
        content.anchoredPosition = new Vector2(0, scroll);
    }

    private void BuildEntries(FireRescueMission mission, float revealDistance)
    {
        entries.Clear();
        focusRow = -1;
        int index = 0;
        foreach (var site in mission.Objectives)
        {
            if (site == null) continue;
            index++;
            targets.Clear();
            bool complete = true;
            var names = new List<string>();
            foreach (var step in site.Steps)
            {
                if (step == null) continue;
                complete &= step.IsComplete;
                if (!names.Contains(step.DisplayName)) names.Add(step.DisplayName);
                if (step is IObjectiveChecklistSource source) source.CollectChecklist(targets);
            }
            if (!visited.Contains(site))
                foreach (var player in PlayerHealth.ActivePlayers)
                {
                    if (player == null || !player.isActiveAndEnabled || player.IsDowned || player.gameObject.scene != mission.gameObject.scene) continue;
                    bool near = Vector2.Distance(player.transform.position, site.transform.position) <= revealDistance;
                    foreach (var target in targets) near |= Vector2.Distance(player.transform.position, target.Position) <= revealDistance;
                    if (near) { visited.Add(site); focusRow = entries.Count; break; }
                }
            entries.Add(new Entry($"{index}. {string.Join(" / ", names)}", complete, false, 'O', MinimapSymbols.Objective));
            if (visited.Contains(site))
                foreach (var target in targets)
                    entries.Add(new Entry(target.Label, target.Complete, true, target.Symbol, target.Color));
        }
        if (mission.State == FireRescueMission.Phase.Evacuate || mission.State == FireRescueMission.Phase.Complete)
            entries.Add(new Entry("Extraction Ready", mission.State == FireRescueMission.Phase.Complete, false, 'E', MinimapSymbols.Evac));
    }

    public void TickScroll()
    {
        if (maxScroll <= 0 || !listPanel.gameObject.activeInHierarchy || Mouse.current == null) return;
        if (!RectTransformUtility.RectangleContainsScreenPoint(listPanel, Mouse.current.position.ReadValue())) return;
        scroll = Mathf.Clamp(scroll - Mouse.current.scroll.ReadValue().y * 0.25f, 0, maxScroll);
        content.anchoredPosition = new Vector2(0, scroll);
    }
    private Row CreateRow()
    {
        var rect = Rect("Objective row", content);
        rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(0.5f, 1);
        var icon = Rect("Map indicator", rect).gameObject.AddComponent<RawImage>();
        icon.raycastTarget = false;
        icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = icon.rectTransform.pivot = new Vector2(0, 1);
        icon.rectTransform.sizeDelta = new Vector2(20, 20);
        var text = Text("Objective", rect, 24); Stretch(text.rectTransform, 0);
        return new Row { Rect = rect, Icon = icon, Text = text };
    }
    private static RectTransform Rect(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); return rect;
    }
    private static RectTransform Panel(string name, Transform parent)
    {
        var panel = Rect(name, parent);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(1, 0);
        var image = panel.gameObject.AddComponent<Image>();
        image.color = new Color32(10, 15, 21, 245); image.raycastTarget = false;
        return panel;
    }
    private static TextMeshProUGUI Text(string name, Transform parent, int size)
    {
        var text = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.enableAutoSizing = false; text.fontSize = size; text.richText = true; text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        return text;
    }
    private static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.one * inset; rect.offsetMax = -Vector2.one * inset;
    }
    public void Dispose()
    {
        foreach (var texture in glyphs.Values) if (texture != null) UnityEngine.Object.Destroy(texture);
    }
}
