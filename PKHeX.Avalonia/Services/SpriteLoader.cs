using System;
using System.Collections.Generic;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using PKHeX.Core;

namespace PKHeX.Avalonia.Services;

/// <summary>
/// Charge les sprites de Pokémon nativement via Avalonia/SkiaSharp,
/// à partir des PNG embarqués (contourne System.Drawing.Common, Windows-only).
/// </summary>
public static class SpriteLoader
{
    private const string Root = "avares://PKHeX.Avalonia/Assets/sprites/";
    private static readonly Dictionary<string, Bitmap?> Cache = new();

    public static Bitmap? Get(PKM pk)
        => pk.Species == 0 ? null : Get(pk.Species, pk.Form, pk.IsShiny);

    public static Bitmap? Get(ushort species, byte form, bool shiny)
    {
        var dir = shiny ? "shiny/" : string.Empty;
        // Forme spécifique, puis sprite de base, puis base non-shiny en dernier recours.
        var candidates = new[]
        {
            $"{dir}b_{species}-{form}",
            $"{dir}b_{species}",
            $"b_{species}-{form}",
            $"b_{species}",
        };
        foreach (var name in candidates)
        {
            var bmp = Load($"{Root}{name}.png");
            if (bmp is not null)
                return bmp;
        }
        return null;
    }

    private static Bitmap? Load(string uriText)
    {
        if (Cache.TryGetValue(uriText, out var cached))
            return cached;

        Bitmap? result = null;
        try
        {
            var uri = new Uri(uriText);
            if (AssetLoader.Exists(uri))
            {
                using var stream = AssetLoader.Open(uri);
                result = new Bitmap(stream);
            }
        }
        catch
        {
            result = null;
        }

        Cache[uriText] = result;
        return result;
    }
}
