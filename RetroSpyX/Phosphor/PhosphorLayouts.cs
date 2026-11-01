// RetroSpy, Phosphor edition: Phosphor's controller drawings by name, for the Layout choice.
// Copyright (C) 2026 f3bandit. GPL-3.0, as the rest of RetroSpy (see LICENSE).
//
// The same list as Phosphor's frontend/src/padlayouts.h (key, name, group), in its order. The drawings themselves
// come from the SuperStation (phosphord's "retrospy" answer); this list is only so the choice can be made before
// connecting. A key the SuperStation doesn't have (an older Phosphor) falls back to Automatic.

using System.Collections.Generic;

namespace RetroSpy.Phosphor
{
    public sealed record PhosphorLayoutName(string Key, string Name, string Group);

    public static class PhosphorLayouts
    {
        public static readonly IReadOnlyList<PhosphorLayoutName> All = new PhosphorLayoutName[]
        {
            new("generic", "Generic (every input)", "Modern"),
            new("xbox", "Xbox-style", "Modern"),
            new("dualshock", "PlayStation (DualShock)", "Modern"),
            new("switchpro", "Switch Pro-style", "Modern"),
            new("nes", "NES / Famicom", "Retro"),
            new("snes", "SNES / Super Famicom", "Retro"),
            new("pce", "PC Engine / TurboGrafx-16", "Retro"),
            new("gen3", "Genesis / Mega Drive (3-button)", "Retro"),
            new("gen6", "Genesis / Mega Drive (6-button)", "Retro"),
            new("neogeo", "Neo Geo CD pad", "Retro"),
            new("jaguar", "Atari Jaguar", "Retro"),
            new("saturn", "Sega Saturn", "Retro"),
            new("psx", "PlayStation (digital)", "Retro"),
            new("n64", "Nintendo 64", "Retro"),
            new("dreamcast", "Dreamcast", "Retro"),
            new("gamecube", "GameCube", "Retro"),
            new("fight6", "6-button fight pad (8BitDo M30, Retro-bit style)", "Fight"),
            new("fight8", "8-button fight pad (Hori Fighting Commander, PowerA Fusion style)", "Fight"),
            new("leverless", "Leverless / all-button (Haute42, Hit Box, Snack Box style)", "Fight"),
            new("stick8", "Arcade stick, 8 buttons (Mayflash F300/F500, Hori RAP style)", "Fight"),
            new("stick6", "Arcade stick, 6 buttons (Astro City style)", "Fight"),
            new("haute42m", "Haute42 COSMOX M Ultra", "Fight"),
        };
    }
}
