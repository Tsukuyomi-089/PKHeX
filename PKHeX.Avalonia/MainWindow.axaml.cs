using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using PKHeX.Core;

namespace PKHeX.Avalonia;

public partial class MainWindow : Window
{
    private SaveFile? _sav;
    private string? _path;
    private IReadOnlyList<string> _speciesNames = [];
    private IReadOnlyList<string> _natureNames = [];
    private SlotEntry? _current;

    public MainWindow()
    {
        // Afficher les noms (espèces, natures) en français.
        try { GameInfo.CurrentLanguage = "fr"; } catch { /* langue non dispo : on garde la valeur par défaut */ }

        InitializeComponent();

        _speciesNames = GameInfo.Strings.Species;
        _natureNames = GameInfo.Strings.Natures;

        SpeciesBox.ItemsSource = _speciesNames;
        NatureBox.ItemsSource = _natureNames;

        OpenButton.Click += OnOpenClick;
        SaveButton.Click += OnSaveClick;
        ApplyButton.Click += OnApplyClick;
        BoxSelector.SelectionChanged += OnBoxChanged;
        SlotList.SelectionChanged += OnSlotSelected;
    }

    private async void OnOpenClick(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Ouvrir une sauvegarde Pokémon",
            AllowMultiple = false,
        });
        if (files.Count == 0)
            return;

        var file = files[0];
        var local = file.TryGetLocalPath();
        try
        {
            byte[] data;
            if (local is not null)
                data = await File.ReadAllBytesAsync(local);
            else
            {
                await using var stream = await file.OpenReadAsync();
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                data = ms.ToArray();
            }

            if (!SaveUtil.TryGetSaveFile(data, out var sav, local))
            {
                Status($"Format non reconnu : {file.Name}");
                return;
            }

            _sav = sav;
            _path = local;
            LoadSave();
        }
        catch (Exception ex)
        {
            Status($"Erreur de chargement : {ex.Message}");
        }
    }

    private void LoadSave()
    {
        if (_sav is not { } sav)
            return;

        SaveInfo.Text = $"{sav.Version}  ·  OT : {sav.OT}  ·  TID : {sav.DisplayTID}  ·  Gén. {sav.Generation}";
        SaveButton.IsEnabled = true;
        BoxSelector.IsEnabled = true;

        var boxes = new List<string>();
        for (int i = 0; i < sav.BoxCount; i++)
            boxes.Add($"Boîte {i + 1}");
        if (sav.HasParty)
            boxes.Add("★ Équipe");

        BoxSelector.ItemsSource = boxes;
        BoxSelector.SelectedIndex = boxes.Count > 0 ? 0 : -1;
        Status($"Sauvegarde chargée : {Path.GetFileName(_path) ?? sav.Version.ToString()}");
    }

    private void OnBoxChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_sav is not { } sav || BoxSelector.SelectedIndex < 0)
            return;

        var entries = new List<SlotEntry>();
        int idx = BoxSelector.SelectedIndex;
        bool isParty = sav.HasParty && idx == sav.BoxCount;

        if (isParty)
        {
            for (int s = 0; s < sav.PartyCount; s++)
                entries.Add(new SlotEntry(-1, s, true, sav.GetPartySlotAtIndex(s), $"Slot {s + 1}"));
        }
        else
        {
            for (int s = 0; s < sav.BoxSlotCount; s++)
                entries.Add(new SlotEntry(idx, s, false, sav.GetBoxSlotAtIndex(idx, s), $"Slot {s + 1}"));
        }

        SlotList.ItemsSource = entries;
    }

    private void OnSlotSelected(object? sender, SelectionChangedEventArgs e)
    {
        _current = SlotList.SelectedItem as SlotEntry;
        var pk = _current?.Entity;
        if (pk is null || pk.Species == 0)
        {
            EditorPanel.IsEnabled = false;
            return;
        }

        EditorPanel.IsEnabled = true;
        SpeciesBox.SelectedIndex = pk.Species < _speciesNames.Count ? pk.Species : -1;
        LevelBox.Value = pk.CurrentLevel;
        NicknameBox.Text = pk.Nickname;
        NatureBox.SelectedIndex = (int)pk.Nature < _natureNames.Count ? (int)pk.Nature : -1;
        OtBox.Text = pk.OriginalTrainerName;
        ShinyLabel.Text = pk.IsShiny ? "✨ Oui" : "Non";
    }

    private void OnApplyClick(object? sender, RoutedEventArgs e)
    {
        if (_sav is not { } sav || _current is not { } entry || entry.Entity is not { } pk)
            return;

        int species = SpeciesBox.SelectedIndex;
        if (species >= 0)
            pk.Species = (ushort)species;

        pk.CurrentLevel = (byte)Math.Clamp((int)(LevelBox.Value ?? pk.CurrentLevel), 1, 100);

        if (NatureBox.SelectedIndex >= 0)
            pk.Nature = (Nature)NatureBox.SelectedIndex;

        pk.OriginalTrainerName = OtBox.Text ?? pk.OriginalTrainerName;

        var nick = NicknameBox.Text ?? string.Empty;
        var expected = pk.Species < _speciesNames.Count ? _speciesNames[pk.Species] : string.Empty;
        if (string.IsNullOrEmpty(nick) || nick == expected)
        {
            pk.IsNicknamed = false;
            pk.Nickname = expected;
        }
        else
        {
            pk.IsNicknamed = true;
            pk.Nickname = nick;
        }

        pk.RefreshChecksum();

        if (entry.IsParty)
            sav.SetPartySlotAtIndex(pk, entry.Slot);
        else
            sav.SetBoxSlotAtIndex(pk, entry.Box, entry.Slot);

        OnBoxChanged(null, null!); // rafraîchir la liste
        Status($"Slot mis à jour : {_speciesNames[pk.Species]} Nv.{pk.CurrentLevel}");
    }

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (_sav is not { } sav)
            return;

        var suggested = _path is not null ? Path.GetFileName(_path) : "sauvegarde.sav";
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Exporter la sauvegarde",
            SuggestedFileName = suggested,
        });
        if (file is null)
            return;

        try
        {
            var bytes = sav.Write().ToArray();
            var local = file.TryGetLocalPath();
            if (local is not null)
            {
                await File.WriteAllBytesAsync(local, bytes);
            }
            else
            {
                await using var stream = await file.OpenWriteAsync();
                await stream.WriteAsync(bytes);
            }
            Status($"Exporté : {file.Name} ({bytes.Length} octets)");
        }
        catch (Exception ex)
        {
            Status($"Erreur d'export : {ex.Message}");
        }
    }

    private void Status(string message) => StatusBar.Text = message;

    private sealed record SlotEntry(int Box, int Slot, bool IsParty, PKM Entity, string Label)
    {
        public override string ToString()
        {
            if (Entity.Species == 0)
                return $"{Label} — (vide)";
            var strings = GameInfo.Strings.Species;
            var name = Entity.Species < strings.Count ? strings[Entity.Species] : $"#{Entity.Species}";
            var nick = Entity.IsNicknamed ? $" «{Entity.Nickname}»" : string.Empty;
            var shiny = Entity.IsShiny ? " ✨" : string.Empty;
            return $"{Label} — {name}{nick} · Nv.{Entity.CurrentLevel}{shiny}";
        }
    }
}
