using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
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
    private PlayerBag? _bag;
    private ushort[] _pouchItemIds = [];

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
        LegalizeButton.Click += OnLegalizeClick;
        ImportButton.Click += OnImportClick;
        ExportButton.Click += OnExportClick;
        ClearButton.Click += OnClearSlotClick;
        TrApplyButton.Click += OnTrainerApplyClick;
        PouchSelector.SelectionChanged += OnPouchChanged;
        ItemSetButton.Click += OnItemSetClick;
        InvApplyButton.Click += OnInventoryApplyClick;
        DexSpeciesPicker.SelectionChanged += OnDexSpeciesChanged;
        DexApplyButton.Click += OnDexApplyClick;
        DexCompleteButton.Click += OnDexCompleteClick;
        DexSeenAllButton.Click += OnDexSeenAllClick;
        DexClearButton.Click += OnDexClearClick;
        BatchRunButton.Click += OnBatchRunClick;
        BoxSelector.SelectionChanged += OnBoxChanged;
        SlotList.SelectionChanged += OnSlotSelected;

        // Glisser-déposer entre emplacements
        DragDrop.SetAllowDrop(SlotList, true);
        SlotList.AddHandler(PointerPressedEvent, OnSlotPointerPressed, RoutingStrategies.Tunnel);
        SlotList.AddHandler(PointerMovedEvent, OnSlotPointerMoved, RoutingStrategies.Tunnel);
        SlotList.AddHandler(DragDrop.DragOverEvent, OnSlotDragOver);
        SlotList.AddHandler(DragDrop.DropEvent, OnSlotDrop);
    }

    // ---- Glisser-déposer ----
    private SlotEntry? _pressedEntry;
    private Point _pressPos;

    private void OnSlotPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _pressedEntry = FindEntry(e.Source);
        _pressPos = e.GetPosition(null);
    }

    private async void OnSlotPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_pressedEntry is null)
            return;
        if (!e.GetCurrentPoint(SlotList).Properties.IsLeftButtonPressed)
        {
            _pressedEntry = null;
            return;
        }
        var pos = e.GetPosition(null);
        if (Math.Abs(pos.X - _pressPos.X) < 5 && Math.Abs(pos.Y - _pressPos.Y) < 5)
            return;

        var source = _pressedEntry;
        _pressedEntry = null;
        if (source.IsEmpty)
            return;

        var data = new DataObject();
        data.Set("pkhex/slot", source);
        await DragDrop.DoDragDrop(e, data, DragDropEffects.Move);
    }

    private void OnSlotDragOver(object? sender, DragEventArgs e)
        => e.DragEffects = e.Data.Contains("pkhex/slot") ? DragDropEffects.Move : DragDropEffects.None;

    private void OnSlotDrop(object? sender, DragEventArgs e)
    {
        if (_sav is not { } sav)
            return;
        if (e.Data.Get("pkhex/slot") is not SlotEntry source)
            return;
        var target = FindEntry(e.Source);
        if (target is null || ReferenceEquals(target, source))
            return;

        // Échange les deux emplacements (clones pour éviter l'aliasing).
        var a = source.Entity.Clone();
        var b = target.Entity.Clone();
        SetSlot(sav, target, a);
        SetSlot(sav, source, b);

        int keep = SlotList.SelectedIndex;
        OnBoxChanged(null, null!);
        SlotList.SelectedIndex = keep;
        Status("Emplacements échangés.");
    }

    private static void SetSlot(SaveFile sav, SlotEntry s, PKM pk)
    {
        if (s.IsParty)
            sav.SetPartySlotAtIndex(pk, s.Slot);
        else
            sav.SetBoxSlotAtIndex(pk, s.Box, s.Slot);
    }

    private static SlotEntry? FindEntry(object? source)
    {
        var c = source as Control;
        while (c is not null)
        {
            if (c.DataContext is SlotEntry se)
                return se;
            c = c.Parent as Control;
        }
        return null;
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

        // Onglet Inventaire
        LoadInventory(sav);

        // Onglet Pokédex
        LoadPokedex(sav);

        // Onglet Batch
        BatchPanel.IsEnabled = true;
        BatchScope.ItemsSource = new[] { "Boîte courante", "Toutes les boîtes", "Équipe" };
        BatchScope.SelectedIndex = 0;

        Status($"Sauvegarde chargée : {Path.GetFileName(_path) ?? sav.Version.ToString()}");
    }

    private readonly record struct SlotRef(int Box, int Slot, bool IsParty, PKM Entity);

    private List<SlotRef> GetBatchScope(SaveFile sav)
    {
        var list = new List<SlotRef>();
        switch (BatchScope.SelectedIndex)
        {
            case 2: // Équipe
                for (int s = 0; s < sav.PartyCount; s++)
                    Add(list, -1, s, true, sav.GetPartySlotAtIndex(s));
                break;
            case 1: // Toutes les boîtes
                for (int b = 0; b < sav.BoxCount; b++)
                    for (int s = 0; s < sav.BoxSlotCount; s++)
                        Add(list, b, s, false, sav.GetBoxSlotAtIndex(b, s));
                break;
            default: // Boîte courante
                int box = BoxSelector.SelectedIndex;
                if (sav.HasParty && box == sav.BoxCount) // "Équipe" sélectionnée dans l'onglet Pokémon
                {
                    for (int s = 0; s < sav.PartyCount; s++)
                        Add(list, -1, s, true, sav.GetPartySlotAtIndex(s));
                }
                else if (box >= 0)
                {
                    for (int s = 0; s < sav.BoxSlotCount; s++)
                        Add(list, box, s, false, sav.GetBoxSlotAtIndex(box, s));
                }
                break;
        }
        return list;

        static void Add(List<SlotRef> l, int b, int s, bool party, PKM pk)
        {
            if (pk.Species != 0)
                l.Add(new SlotRef(b, s, party, pk));
        }
    }

    private void OnBatchRunClick(object? sender, RoutedEventArgs e)
    {
        if (_sav is not { } sav)
            return;
        var text = BatchInput.Text ?? string.Empty;
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length == 0)
        {
            BatchResults.Text = "Aucune instruction.";
            return;
        }

        var scope = GetBatchScope(sav);
        if (scope.Count == 0)
        {
            BatchResults.Text = "Aucun Pokémon dans la portée choisie.";
            return;
        }

        try
        {
            var pkms = scope.ConvertAll(x => x.Entity);
            var proc = EntityBatchProcessor.Execute(lines, pkms);
            var sets = StringInstructionSet.GetBatchSets(lines);

            // Réécriture des entités modifiées dans la sauvegarde.
            foreach (var s in scope)
            {
                s.Entity.RefreshChecksum();
                SetSlot(sav, new SlotEntry(s.Box, s.Slot, s.IsParty, s.Entity, ""), s.Entity);
            }

            BatchResults.Text = proc.GetEditorResults(sets);

            // Rafraîchit la vue courante
            int keep = SlotList.SelectedIndex;
            OnBoxChanged(null, null!);
            if (keep >= 0)
                SlotList.SelectedIndex = keep;
            Status("Édition en masse appliquée (Exporter pour enregistrer).");
        }
        catch (Exception ex)
        {
            BatchResults.Text = $"Erreur : {ex.Message}";
        }
    }

    private void LoadPokedex(SaveFile sav)
    {
        if (!sav.HasPokeDex)
        {
            DexPanel.IsEnabled = false;
            DexCounts.Text = "Cette sauvegarde n'a pas de Pokédex pris en charge.";
            return;
        }
        DexPanel.IsEnabled = true;

        var names = new List<string>();
        for (ushort i = 1; i <= sav.MaxSpeciesID; i++)
            names.Add(i < _species.Count ? $"{i:000} {_species[i]}" : $"{i:000}");
        DexSpeciesPicker.ItemsSource = names;
        DexSpeciesPicker.SelectedIndex = 0;
        RefreshDexCounts(sav);
    }

    private void RefreshDexCounts(SaveFile sav)
        => DexCounts.Text = $"Vus : {sav.SeenCount} / {sav.MaxSpeciesID}   ·   Capturés : {sav.CaughtCount} / {sav.MaxSpeciesID}";

    private void OnDexSpeciesChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_sav is not { } sav || DexSpeciesPicker.SelectedIndex < 0)
            return;
        var species = (ushort)(DexSpeciesPicker.SelectedIndex + 1);
        DexSeen.IsChecked = sav.GetSeen(species);
        DexCaught.IsChecked = sav.GetCaught(species);
    }

    // Le Pokédex est spécifique à chaque génération : le dex Gen5 (Zukan5), Gen6+ (ZukanBase<T>) et
    // Gen1/2 (SaveFile.SetSeen/SetCaught) ont des API incompatibles. On invoque au mieux, par réflexion,
    // la première méthode connue disponible sur l'objet "Zukan", en remplissant ses paramètres par défaut.
    private static object? GetZukan(SaveFile sav) => sav.GetType().GetProperty("Zukan")?.GetValue(sav);

    private static object? DefaultArg(Type t)
        => t.IsValueType ? Activator.CreateInstance(t) : null;

    private static bool TryInvokeZukan(SaveFile sav, params string[] methodNames)
    {
        var zukan = GetZukan(sav);
        if (zukan is null)
            return false;
        foreach (var name in methodNames)
        {
            var mi = zukan.GetType()
                .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == name);
            if (mi is null)
                continue;
            var args = Array.ConvertAll(mi.GetParameters(),
                p => p.HasDefaultValue ? p.DefaultValue : DefaultArg(p.ParameterType));
            mi.Invoke(zukan, args);
            return true;
        }
        return false;
    }

    // Invoque une méthode par-espèce (species + éventuels paramètres par défaut).
    private static bool TryInvokeZukanSpecies(SaveFile sav, ushort species, params string[] methodNames)
    {
        var zukan = GetZukan(sav);
        if (zukan is null)
            return false;
        foreach (var name in methodNames)
        {
            var mi = zukan.GetType()
                .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == name && m.GetParameters().Length > 0
                    && m.GetParameters()[0].ParameterType == typeof(ushort));
            if (mi is null)
                continue;
            var ps = mi.GetParameters();
            var args = new object?[ps.Length];
            args[0] = species;
            for (int i = 1; i < ps.Length; i++)
                args[i] = ps[i].HasDefaultValue ? ps[i].DefaultValue : DefaultArg(ps[i].ParameterType);
            mi.Invoke(zukan, args);
            return true;
        }
        return false;
    }

    private void OnDexApplyClick(object? sender, RoutedEventArgs e)
    {
        if (_sav is not { } sav || DexSpeciesPicker.SelectedIndex < 0)
            return;
        var species = (ushort)(DexSpeciesPicker.SelectedIndex + 1);
        bool caught = DexCaught.IsChecked == true;
        int before = sav.CaughtCount;

        if (caught)
        {
            // Gen6+ : SetDexEntryAll ; Gen5 : GiveAll/SetCaught ; sinon repli base.
            if (!TryInvokeZukanSpecies(sav, species, "SetDexEntryAll", "GiveAll", "SetDex", "SetCaught"))
                sav.SetCaught(species, true);
        }
        else
        {
            if (!TryInvokeZukanSpecies(sav, species, "ClearDexEntryAll", "ClearSeen"))
                sav.SetCaught(species, false);
        }

        RefreshDexCounts(sav);
        OnDexSpeciesChanged(null, null!);
        Status($"Pokédex mis à jour pour {_species[species]} (capturés {before} → {sav.CaughtCount}).");
    }

    private void OnDexCompleteClick(object? sender, RoutedEventArgs e)
    {
        if (_sav is not { } sav)
            return;
        // Gen6+ : CompleteDex. Gen5 : SeenAll + CaughtAll. Repli : boucle base.
        bool ok = TryInvokeZukan(sav, "CompleteDex");
        if (!ok)
        {
            bool a = TryInvokeZukan(sav, "SeenAll", "SetAllSeen");
            bool b = TryInvokeZukan(sav, "CaughtAll");
            ok = a || b;
        }
        if (!ok)
        {
            for (ushort i = 1; i <= sav.MaxSpeciesID; i++)
            {
                sav.SetSeen(i, true);
                sav.SetCaught(i, true);
            }
        }
        RefreshDexCounts(sav);
        OnDexSpeciesChanged(null, null!);
        ReportDex(sav, "Pokédex complété");
    }

    private void OnDexSeenAllClick(object? sender, RoutedEventArgs e)
    {
        if (_sav is not { } sav)
            return;
        if (!TryInvokeZukan(sav, "SeenAll", "SetAllSeen"))
        {
            for (ushort i = 1; i <= sav.MaxSpeciesID; i++)
                sav.SetSeen(i, true);
        }
        RefreshDexCounts(sav);
        OnDexSpeciesChanged(null, null!);
        ReportDex(sav, "Toutes les espèces marquées vues");
    }

    private void OnDexClearClick(object? sender, RoutedEventArgs e)
    {
        if (_sav is not { } sav)
            return;
        bool z = TryInvokeZukan(sav, "SeenNone");
        TryInvokeZukan(sav, "CaughtNone");
        if (!z)
        {
            for (ushort i = 1; i <= sav.MaxSpeciesID; i++)
            {
                sav.SetCaught(i, false);
                sav.SetSeen(i, false);
            }
        }
        RefreshDexCounts(sav);
        OnDexSpeciesChanged(null, null!);
        ReportDex(sav, "Pokédex effacé");
    }

    private void ReportDex(SaveFile sav, string action)
    {
        if (sav.CaughtCount == 0 && sav.SeenCount == 0 && action != "Pokédex effacé")
            Status($"{action} : aucun changement — génération peut-être non prise en charge.");
        else
            Status($"{action}. Vus={sav.SeenCount}, Capturés={sav.CaughtCount}.");
    }

    private void LoadInventory(SaveFile sav)
    {
        _bag = sav.Inventory;
        var pouches = _bag.Pouches;
        if (pouches.Count == 0)
        {
            InventoryPanel.IsEnabled = false;
            PouchSelector.ItemsSource = null;
            ItemListView.ItemsSource = null;
            return;
        }

        InventoryPanel.IsEnabled = true;
        var names = new List<string>();
        foreach (var p in pouches)
            names.Add($"{p.Type} ({p.Count})");
        PouchSelector.ItemsSource = names;
        PouchSelector.SelectedIndex = 0;
    }

    private void OnPouchChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_bag is null || PouchSelector.SelectedIndex < 0 || PouchSelector.SelectedIndex >= _bag.Pouches.Count)
            return;
        var pouch = _bag.Pouches[PouchSelector.SelectedIndex];

        // Liste des objets présents
        var rows = new List<string>();
        foreach (var it in pouch.Items)
        {
            if (it.Count <= 0 || it.Index <= 0)
                continue;
            var name = it.Index < _items.Count ? _items[it.Index] : $"#{it.Index}";
            rows.Add($"{name}  ×{it.Count}");
        }
        ItemListView.ItemsSource = rows.Count > 0 ? rows : new List<string> { "(poche vide)" };

        // Sélecteur d'objets valides pour cette poche
        var valid = pouch.GetAllItems();
        _pouchItemIds = valid.ToArray();
        var pickerNames = new List<string>(_pouchItemIds.Length);
        foreach (var id in _pouchItemIds)
            pickerNames.Add(id < _items.Count ? _items[id] : $"#{id}");
        ItemPicker.ItemsSource = pickerNames;
        if (pickerNames.Count > 0)
            ItemPicker.SelectedIndex = 0;
    }

    private void OnItemSetClick(object? sender, RoutedEventArgs e)
    {
        if (_bag is null || PouchSelector.SelectedIndex < 0 || ItemPicker.SelectedIndex < 0)
            return;
        var pouch = _bag.Pouches[PouchSelector.SelectedIndex];
        ushort id = _pouchItemIds[ItemPicker.SelectedIndex];
        int count = Val(ItemCount, 0);

        // Objet déjà présent ?
        InventoryItem? existing = null;
        foreach (var it in pouch.Items)
        {
            if (it.Index == id)
            {
                existing = it;
                break;
            }
        }

        if (existing is not null)
        {
            if (count <= 0)
                existing.Clear();
            else
                existing.Count = count;
        }
        else if (count > 0)
        {
            // Premier emplacement libre
            InventoryItem? free = null;
            foreach (var it in pouch.Items)
            {
                if (it.Index == 0 || it.Count == 0)
                {
                    free = it;
                    break;
                }
            }
            if (free is null)
            {
                Status("Poche pleine : aucun emplacement libre.");
                return;
            }
            free.Index = id;
            free.Count = count;
        }

        int keep = PouchSelector.SelectedIndex;
        OnPouchChanged(null, null!);
        PouchSelector.SelectedIndex = keep;
        Status($"Objet défini : {_items[id]} ×{count} (Appliquer pour enregistrer).");
    }

    private void OnInventoryApplyClick(object? sender, RoutedEventArgs e)
    {
        if (_sav is not { } sav || _bag is null)
            return;
        _bag.CopyTo(sav);
        // Rafraîchit les compteurs de poches
        LoadInventory(sav);
        Status("Inventaire appliqué (Exporter pour enregistrer sur disque).");
    }

    private void OnClearSlotClick(object? sender, RoutedEventArgs e)
    {
        if (_sav is not { } sav || _current is not { } entry)
            return;
        var blank = sav.BlankPKM;
        if (entry.IsParty)
            sav.SetPartySlotAtIndex(blank, entry.Slot);
        else
            sav.SetBoxSlotAtIndex(blank, entry.Box, entry.Slot);
        OnBoxChanged(null, null!);
        Status("Emplacement vidé.");
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

    // Légalisation best-effort : applique les corrections « intelligentes » de PKHeX.Core
    // (moveset, relearn, lieu de capture, amitié, Ball, PP, stats) via le moteur batch.
    // Ce n'est pas l'AutoLegalityMod complet (qui reconstruit un encounter légal) : cela corrige
    // les incohérences courantes d'un Pokémon déjà proche du légal.
    private static readonly string[] LegalizeLines =
    [
        ".Moves=$suggest",
        ".RelearnMoves=$suggest",
        ".MetLocation=$suggest",
        ".CurrentFriendship=$suggest",
        ".Ball=$suggest",
        ".HealPP=$suggest",
        ".Stats=$suggest",
    ];

    private void OnLegalizeClick(object? sender, RoutedEventArgs e)
    {
        if (_sav is not { } sav || _current is not { } entry || entry.Entity is not { } pk)
            return;
        try
        {
            EntityBatchProcessor.Execute(LegalizeLines, new[] { pk });
            pk.RefreshChecksum();
            if (entry.IsParty)
                sav.SetPartySlotAtIndex(pk, entry.Slot);
            else
                sav.SetBoxSlotAtIndex(pk, entry.Box, entry.Slot);

            LoadEditor(pk);
            RefreshLegality(pk);

            int keep = SlotList.SelectedIndex;
            OnBoxChanged(null, null!);
            if (keep >= 0)
                SlotList.SelectedIndex = keep;

            var la = new LegalityAnalysis(pk);
            Status(la.Valid
                ? "Légalisation best-effort : ✅ le Pokémon est maintenant légal."
                : "Légalisation best-effort appliquée — quelques problèmes subsistent (voir rapport).");
        }
        catch (Exception ex)
        {
            Status($"Erreur de légalisation : {ex.Message}");
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
