// K26K Hardware Monitor - embedded fonts for the Sci-Fi and Fancy designs.
// Copyright (C) 2026 K26K Hardware Monitor contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
//
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace LibreHardwareMonitor.Windows.Forms.UI.Design;

/// <summary>
/// Loads JetBrains Mono (SIL Open Font License, see Resources/Fonts/OFL.txt) from the embedded resources.
/// GDI+ uses a PrivateFontCollection, GDI (TextRenderer, used by the tree) needs AddFontMemResourceEx,
/// and Skia gets typefaces from the same bytes.
/// </summary>
public static class FontManager
{
    public const string FamilyName = "JetBrains Mono";

    private const string ResourcePrefix = "LibreHardwareMonitor.Windows.Forms.Resources.Fonts.";

    private static readonly object Sync = new();
    private static readonly Dictionary<string, byte[]> Bytes = new();
    private static readonly Dictionary<string, SKTypeface> Typefaces = new();
    private static PrivateFontCollection _collection;
    private static bool _loaded;

    /// <summary>Regular, Medium or Bold.</summary>
    public static SKTypeface Typeface(string weight = "Regular")
    {
        EnsureLoaded();
        lock (Sync)
        {
            if (Typefaces.TryGetValue(weight, out SKTypeface typeface))
                return typeface;

            typeface = Bytes.TryGetValue(weight, out byte[] data)
                ? SKTypeface.FromStream(new MemoryStream(data))
                : SKTypeface.FromFamilyName("Consolas");

            Typefaces[weight] = typeface;
            return typeface;
        }
    }

    /// <summary>A GDI+ font of the embedded family; falls back to Consolas if the font could not be loaded.</summary>
    public static Font CreateFont(float sizeInPoints, FontStyle style = FontStyle.Regular)
    {
        EnsureLoaded();
        FontFamily family = _collection?.Families.Length > 0 ? _collection.Families[0] : new FontFamily("Consolas");
        return new Font(family, sizeInPoints, style, GraphicsUnit.Point);
    }

    private static void EnsureLoaded()
    {
        lock (Sync)
        {
            if (_loaded)
                return;

            _loaded = true;
            _collection = new PrivateFontCollection();
            Assembly assembly = typeof(FontManager).Assembly;

            foreach (string weight in new[] { "Regular", "Medium", "Bold" })
            {
                using Stream stream = assembly.GetManifestResourceStream(ResourcePrefix + "JetBrainsMono-" + weight + ".ttf");
                if (stream == null)
                    continue;

                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                byte[] data = ms.ToArray();
                Bytes[weight] = data;

                // The memory must stay valid for the lifetime of the process.
                IntPtr memory = Marshal.AllocCoTaskMem(data.Length);
                Marshal.Copy(data, 0, memory, data.Length);

                try
                {
                    _collection.AddMemoryFont(memory, data.Length);
                    uint count = 0;
                    AddFontMemResourceEx(memory, (uint)data.Length, IntPtr.Zero, ref count);
                }
                catch
                {
                    // Font loading is optional; the designs fall back to Consolas.
                }
            }
        }
    }

    [DllImport("gdi32.dll")]
    private static extern IntPtr AddFontMemResourceEx(IntPtr pbFont, uint cbFont, IntPtr pdv, [In] ref uint pcFonts);
}
