using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PKHeX.Avalonia.Models;
using PKHeX.Avalonia.Services;
using PKHeX.Core;

namespace PKHeX.Avalonia;

public partial class MainWindow : Window
{
    private SaveFile? _sav;
    private string? _path;
    private IReadOnlyList<string> _species = [];
    private IReadOnlyList<string> _natures = [];
    private IReadOnlyList<string> _abilities = [];
    private IReadOnlyList<string> _items = [];
    private IReadOnlyList<string> _balls = [];
    private IReadOnlyList<string> _moves = [];
    private SlotEntry? _current;

    public MainWindow()
    {
        try { GameInfo.CurrentLanguage = "fr"; } catch { /* langue non dispo */ }

        InitializeComponent();

        var s = GameInfo.Strings;
        _species = s.Species;
        _natures = s.Natures;
        _abilities = s.abilitylist;
        _items = s.itemlist;
        _balls = s.balllist;
        _moves = s.movelist;

        SpeciesBox.ItemsSource = _species;
        NatureBox.ItemsSource = _natures;
        AbilityBox.ItemsSource = _abilities;
        ItemBox.ItemsSource = _items;
        BallBox.ItemsSource = _balls;
        Move1Box.ItemsSource = _moves;
        Move2Box.ItemsSource = _moves;
        Move3Box.ItemsSource = _moves;
        Move4Box.ItemsSource = _moves;
        GenderBox.ItemsSource = new[] { "♂", "♀", "—" };
        TrGenderBox.ItemsSource = new[] { "♂", "♀" };

        OpenButton.Click += OnOpenClick;
        SaveButton.Click += OnSaveClick;
        ApplyButton.Click += OnApplyClick;
        ShinyButton.Click += OnShinyClick;
        ImportButton.Click += OnImportClick;
        ExportButton.Click += OnExportClick;
        TrApplyButton.Click += OnTrainerApplyClick;
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
            {
                data = await File.ReadAllBytesAsync(local);
            }
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

    /// <summary>Charge une sauvegarde depuis un chemin (utilisé aussi via argument CLI).</summary>
    public void LoadFromPath(string path)
    {
        try
        {
            var data = File.ReadAllBytes(path);
            if (!SaveUtil.TryGetSaveFile(data, out var sav, path))
            {
                Status($"Format non reconnu : {Path.GetFileName(path)}");
                return;
            }
            _sav = sav;
            _path = path;
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

        // Onglet Dresseur
        TrainerPanel.IsEnabled = true;
        TrOtBox.Text = sav.OT;
        TrTidBox.Value = sav.DisplayTID;
        TrSidBox.Value = sav.DisplaySID;
        TrGenderBox.SelectedIndex = Math.Clamp(sav.Gender, (byte)0, (byte)1);
        TrMoneyBox.Maximum = sav.MaxMoney;
        TrMoneyBox.Value = sav.Money;
        TrHoursBox.Value = sav.PlayedHours;
        TrMinutesBox.Value = sav.PlayedMinutes;

        Status($"Sauvegarde chargée : {Path.GetFileName(_path) ?? sav.Version.ToString()}");
    }

    private void OnTrainerApplyClick(object? sender, RoutedEventArgs e)
    {
        if (_sav is not { } sav)
            return;
        sav.OT = TrOtBox.Text ?? sav.OT;
        sav.DisplayTID = (uint)Val(TrTidBox, (int)sav.DisplayTID);
        sav.DisplaySID = (uint)Val(TrSidBox, (int)sav.DisplaySID);
        if (TrGenderBox.SelectedIndex >= 0)
            sav.Gender = (byte)TrGenderBox.SelectedIndex;
        sav.Money = (uint)Val(TrMoneyBox, (int)sav.Money);
        sav.PlayedHours = Val(TrHoursBox, sav.PlayedHours);
        sav.PlayedMinutes = Val(TrMinutesBox, sav.PlayedMinutes);
        SaveInfo.Text = $"{sav.Version}  ·  OT : {sav.OT}  ·  TID : {sav.DisplayTID}  ·  Gén. {sav.Generation}";
        Status("Infos dresseur appliquées (Exporter pour enregistrer sur disque).");
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

        // Sélectionne automatiquement le premier emplacement occupé.
        int firstOccupied = entries.FindIndex(x => !x.IsEmpty);
        if (firstOccupied >= 0)
            SlotList.SelectedIndex = firstOccupied;
    }

    private void OnSlotSelected(object? sender, SelectionChangedEventArgs e)
    {
        _current = SlotList.SelectedItem as SlotEntry;
        var pk = _current?.Entity;
        if (pk is null || pk.Species == 0)
        {
            EditorPanel.IsEnabled = false;
            EditorSprite.Source = null;
            EditorTitle.Text = "Éditeur";
            LegalityStatus.Text = "—";
            LegalityReport.Text = string.Empty;
            return;
        }

        EditorPanel.IsEnabled = true;
        LoadEditor(pk);
        RefreshLegality(pk);
    }

    private void LoadEditor(PKM pk)
    {
        EditorSprite.Source = SpriteLoader.Get(pk);
        EditorTitle.Text = pk.Species < _species.Count ? _species[pk.Species] : $"#{pk.Species}";

        SpeciesBox.SelectedIndex = pk.Species < _species.Count ? pk.Species : -1;
        FormBox.Value = pk.Form;
        LevelBox.Value = pk.CurrentLevel;
        GenderBox.SelectedIndex = Math.Clamp(pk.Gender, (byte)0, (byte)2);
        NicknameBox.Text = pk.Nickname;
        OtBox.Text = pk.OriginalTrainerName;

        NatureBox.SelectedIndex = (int)pk.Nature < _natures.Count ? (int)pk.Nature : -1;
        AbilityBox.SelectedIndex = pk.Ability >= 0 && pk.Ability < _abilities.Count ? pk.Ability : -1;
        ItemBox.SelectedIndex = pk.HeldItem >= 0 && pk.HeldItem < _items.Count ? pk.HeldItem : -1;
        BallBox.SelectedIndex = pk.Ball < _balls.Count ? pk.Ball : -1;
        FriendshipBox.Value = pk.CurrentFriendship;

        Move1Box.SelectedIndex = pk.Move1 < _moves.Count ? pk.Move1 : -1;
        Move2Box.SelectedIndex = pk.Move2 < _moves.Count ? pk.Move2 : -1;
        Move3Box.SelectedIndex = pk.Move3 < _moves.Count ? pk.Move3 : -1;
        Move4Box.SelectedIndex = pk.Move4 < _moves.Count ? pk.Move4 : -1;

        IvHp.Maximum = pk.MaxIV; IvAtk.Maximum = pk.MaxIV; IvDef.Maximum = pk.MaxIV;
        IvSpa.Maximum = pk.MaxIV; IvSpd.Maximum = pk.MaxIV; IvSpe.Maximum = pk.MaxIV;
        EvHp.Maximum = pk.MaxEV; EvAtk.Maximum = pk.MaxEV; EvDef.Maximum = pk.MaxEV;
        EvSpa.Maximum = pk.MaxEV; EvSpd.Maximum = pk.MaxEV; EvSpe.Maximum = pk.MaxEV;

        IvHp.Value = pk.IV_HP; IvAtk.Value = pk.IV_ATK; IvDef.Value = pk.IV_DEF;
        IvSpa.Value = pk.IV_SPA; IvSpd.Value = pk.IV_SPD; IvSpe.Value = pk.IV_SPE;
        EvHp.Value = pk.EV_HP; EvAtk.Value = pk.EV_ATK; EvDef.Value = pk.EV_DEF;
        EvSpa.Value = pk.EV_SPA; EvSpd.Value = pk.EV_SPD; EvSpe.Value = pk.EV_SPE;
    }

    private static int Val(NumericUpDown box, int fallback) => (int)(box.Value ?? fallback);

    private void OnApplyClick(object? sender, RoutedEventArgs e)
    {
        if (_sav is not { } sav || _current is not { } entry || entry.Entity is not { } pk)
            return;

        if (SpeciesBox.SelectedIndex >= 0)
            pk.Species = (ushort)SpeciesBox.SelectedIndex;
        pk.Form = (byte)Val(FormBox, pk.Form);
        pk.CurrentLevel = (byte)Math.Clamp(Val(LevelBox, pk.CurrentLevel), 1, 100);
        if (GenderBox.SelectedIndex >= 0)
            pk.Gender = (byte)GenderBox.SelectedIndex;
        pk.OriginalTrainerName = OtBox.Text ?? pk.OriginalTrainerName;

        if (NatureBox.SelectedIndex >= 0)
            pk.Nature = (Nature)NatureBox.SelectedIndex;
        if (AbilityBox.SelectedIndex >= 0)
            pk.Ability = AbilityBox.SelectedIndex;
        if (ItemBox.SelectedIndex >= 0)
            pk.HeldItem = ItemBox.SelectedIndex;
        if (BallBox.SelectedIndex >= 0)
            pk.Ball = (byte)BallBox.SelectedIndex;
        pk.CurrentFriendship = (byte)Val(FriendshipBox, pk.CurrentFriendship);

        if (Move1Box.SelectedIndex >= 0) pk.Move1 = (ushort)Move1Box.SelectedIndex;
        if (Move2Box.SelectedIndex >= 0) pk.Move2 = (ushort)Move2Box.SelectedIndex;
        if (Move3Box.SelectedIndex >= 0) pk.Move3 = (ushort)Move3Box.SelectedIndex;
        if (Move4Box.SelectedIndex >= 0) pk.Move4 = (ushort)Move4Box.SelectedIndex;
        pk.HealPP();

        pk.IV_HP = Val(IvHp, pk.IV_HP); pk.IV_ATK = Val(IvAtk, pk.IV_ATK); pk.IV_DEF = Val(IvDef, pk.IV_DEF);
        pk.IV_SPA = Val(IvSpa, pk.IV_SPA); pk.IV_SPD = Val(IvSpd, pk.IV_SPD); pk.IV_SPE = Val(IvSpe, pk.IV_SPE);
        pk.EV_HP = Val(EvHp, pk.EV_HP); pk.EV_ATK = Val(EvAtk, pk.EV_ATK); pk.EV_DEF = Val(EvDef, pk.EV_DEF);
        pk.EV_SPA = Val(EvSpa, pk.EV_SPA); pk.EV_SPD = Val(EvSpd, pk.EV_SPD); pk.EV_SPE = Val(EvSpe, pk.EV_SPE);

        var nick = NicknameBox.Text ?? string.Empty;
        var expected = pk.Species < _species.Count ? _species[pk.Species] : string.Empty;
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

        int keep = SlotList.SelectedIndex;
        OnBoxChanged(null, null!);
        SlotList.SelectedIndex = keep;
        RefreshLegality(pk);
        Status($"Slot mis à jour : {_species[pk.Species]} Nv.{pk.CurrentLevel}");
    }

    private void OnShinyClick(object? sender, RoutedEventArgs e)
    {
        if (_current?.Entity is not { } pk)
            return;
        pk.SetShiny();
        pk.RefreshChecksum();
        EditorSprite.Source = SpriteLoader.Get(pk);
        RefreshLegality(pk);
        Status("Pokémon rendu chromatique (cliquez sur Appliquer pour l'enregistrer).");
    }

    private async void OnExportClick(object? sender, RoutedEventArgs e)
    {
        if (_current?.Entity is not { } pk || pk.Species == 0)
            return;
        var name = pk.Species < _species.Count ? _species[pk.Species] : $"{pk.Species}";
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Exporter le Pokémon",
            SuggestedFileName = $"{name}.pk{pk.Format}",
        });
        if (file is null)
            return;
        try
        {
            pk.RefreshChecksum();
            var bytes = pk.Data.ToArray();
            var local = file.TryGetLocalPath();
            if (local is not null)
                await File.WriteAllBytesAsync(local, bytes);
            else
            {
                await using var stream = await file.OpenWriteAsync();
                await stream.WriteAsync(bytes);
            }
            Status($"Pokémon exporté : {file.Name}");
        }
        catch (Exception ex)
        {
            Status($"Erreur d'export .pk : {ex.Message}");
        }
    }

    private async void OnImportClick(object? sender, RoutedEventArgs e)
    {
        if (_sav is not { } sav || _current is not { } entry)
            return;
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Importer un Pokémon (.pk*)",
            AllowMultiple = false,
        });
        if (files.Count == 0)
            return;
        try
        {
            var f = files[0];
            byte[] data;
            var local = f.TryGetLocalPath();
            if (local is not null)
            {
                data = await File.ReadAllBytesAsync(local);
            }
            else
            {
                await using var stream = await f.OpenReadAsync();
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                data = ms.ToArray();
            }

            var imported = EntityFormat.GetFromBytes(data);
            if (imported is null)
            {
                Status($"Fichier Pokémon non reconnu : {f.Name}");
                return;
            }

            var converted = EntityConverter.ConvertToType(imported, sav.PKMType, out var result);
            if (converted is null)
            {
                Status($"Conversion impossible ({result}).");
                return;
            }

            converted.RefreshChecksum();
            if (entry.IsParty)
                sav.SetPartySlotAtIndex(converted, entry.Slot);
            else
                sav.SetBoxSlotAtIndex(converted, entry.Box, entry.Slot);

            int keep = SlotList.SelectedIndex;
            OnBoxChanged(null, null!);
            SlotList.SelectedIndex = keep;
            Status($"Pokémon importé dans le slot ({result}).");
        }
        catch (Exception ex)
        {
            Status($"Erreur d'import .pk : {ex.Message}");
        }
    }

    private void RefreshLegality(PKM pk)
    {
        try
        {
            var la = new LegalityAnalysis(pk);
            LegalityStatus.Text = la.Valid ? "✅ Légal" : "❌ Illégal";
            LegalityReport.Text = la.Report();
        }
        catch (Exception ex)
        {
            LegalityStatus.Text = "⚠️ Analyse impossible";
            LegalityReport.Text = ex.Message;
        }
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
}
