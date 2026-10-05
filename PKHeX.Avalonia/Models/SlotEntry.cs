using Avalonia.Media.Imaging;
using PKHeX.Avalonia.Services;
using PKHeX.Core;

namespace PKHeX.Avalonia.Models;

/// <summary>Un emplacement (boîte ou équipe) affiché dans la liste.</summary>
public sealed class SlotEntry(int box, int slot, bool isParty, PKM entity, string label)
{
    public int Box { get; } = box;
    public int Slot { get; } = slot;
    public bool IsParty { get; } = isParty;
    public PKM Entity { get; } = entity;
    public string Label { get; } = label;

    public bool IsEmpty => Entity.Species == 0;

    public Bitmap? Image => SpriteLoader.Get(Entity);

    public string Text
    {
        get
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
