using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Core;

public sealed class PixelFont : IDisposable
{
    public const int GlyphWidth = 8;
    public const int GlyphHeight = 8;

    private const int FirstCharacter = 32;
    private const int LastCharacter = 126;
    private const int AtlasColumns = 16;
    private const int AtlasRows = (LastCharacter - FirstCharacter + AtlasColumns) / AtlasColumns;

    private static readonly byte[] BlankGlyph = [0, 0, 0, 0, 0, 0, 0];

    private static readonly System.Collections.Generic.Dictionary<char, byte[]> Glyphs = new()
    {
        ['?'] = [0x0E, 0x11, 0x01, 0x02, 0x04, 0x00, 0x04],
        ['0'] = [0x0E, 0x11, 0x13, 0x15, 0x19, 0x11, 0x0E],
        ['1'] = [0x04, 0x0C, 0x04, 0x04, 0x04, 0x04, 0x0E],
        ['2'] = [0x0E, 0x11, 0x01, 0x02, 0x04, 0x08, 0x1F],
        ['3'] = [0x1E, 0x01, 0x01, 0x0E, 0x01, 0x01, 0x1E],
        ['4'] = [0x02, 0x06, 0x0A, 0x12, 0x1F, 0x02, 0x02],
        ['5'] = [0x1F, 0x10, 0x10, 0x1E, 0x01, 0x01, 0x1E],
        ['6'] = [0x0E, 0x10, 0x10, 0x1E, 0x11, 0x11, 0x0E],
        ['7'] = [0x1F, 0x01, 0x02, 0x04, 0x08, 0x08, 0x08],
        ['8'] = [0x0E, 0x11, 0x11, 0x0E, 0x11, 0x11, 0x0E],
        ['9'] = [0x0E, 0x11, 0x11, 0x0F, 0x01, 0x01, 0x0E],
        ['A'] = [0x0E, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11],
        ['B'] = [0x1E, 0x11, 0x11, 0x1E, 0x11, 0x11, 0x1E],
        ['C'] = [0x0E, 0x11, 0x10, 0x10, 0x10, 0x11, 0x0E],
        ['D'] = [0x1E, 0x11, 0x11, 0x11, 0x11, 0x11, 0x1E],
        ['E'] = [0x1F, 0x10, 0x10, 0x1E, 0x10, 0x10, 0x1F],
        ['F'] = [0x1F, 0x10, 0x10, 0x1E, 0x10, 0x10, 0x10],
        ['G'] = [0x0E, 0x11, 0x10, 0x17, 0x11, 0x11, 0x0F],
        ['H'] = [0x11, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11],
        ['I'] = [0x0E, 0x04, 0x04, 0x04, 0x04, 0x04, 0x0E],
        ['J'] = [0x07, 0x02, 0x02, 0x02, 0x12, 0x12, 0x0C],
        ['K'] = [0x11, 0x12, 0x14, 0x18, 0x14, 0x12, 0x11],
        ['L'] = [0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x1F],
        ['M'] = [0x11, 0x1B, 0x15, 0x15, 0x11, 0x11, 0x11],
        ['N'] = [0x11, 0x19, 0x15, 0x13, 0x11, 0x11, 0x11],
        ['O'] = [0x0E, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E],
        ['P'] = [0x1E, 0x11, 0x11, 0x1E, 0x10, 0x10, 0x10],
        ['Q'] = [0x0E, 0x11, 0x11, 0x11, 0x15, 0x12, 0x0D],
        ['R'] = [0x1E, 0x11, 0x11, 0x1E, 0x14, 0x12, 0x11],
        ['S'] = [0x0F, 0x10, 0x10, 0x0E, 0x01, 0x01, 0x1E],
        ['T'] = [0x1F, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04],
        ['U'] = [0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E],
        ['V'] = [0x11, 0x11, 0x11, 0x11, 0x11, 0x0A, 0x04],
        ['W'] = [0x11, 0x11, 0x11, 0x15, 0x15, 0x15, 0x0A],
        ['X'] = [0x11, 0x11, 0x0A, 0x04, 0x0A, 0x11, 0x11],
        ['Y'] = [0x11, 0x11, 0x0A, 0x04, 0x04, 0x04, 0x04],
        ['Z'] = [0x1F, 0x01, 0x02, 0x04, 0x08, 0x10, 0x1F],
        ['/'] = [0x01, 0x02, 0x02, 0x04, 0x08, 0x08, 0x10],
        [':'] = [0x00, 0x04, 0x04, 0x00, 0x04, 0x04, 0x00],
        ['-'] = [0x00, 0x00, 0x00, 0x1F, 0x00, 0x00, 0x00],
        ['.'] = [0x00, 0x00, 0x00, 0x00, 0x00, 0x0C, 0x0C]
    };

    private readonly Texture2D _texture;

    public PixelFont(GraphicsDevice graphicsDevice)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);

        _texture = new Texture2D(
            graphicsDevice,
            AtlasColumns * GlyphWidth,
            AtlasRows * GlyphHeight);

        Color[] pixels = new Color[_texture.Width * _texture.Height];
        for (int character = FirstCharacter; character <= LastCharacter; character++)
        {
            char glyphCharacter = char.ToUpperInvariant((char)character);
            byte[] rows = Glyphs.TryGetValue(glyphCharacter, out byte[] glyph)
                ? glyph
                : character == ' ' ? BlankGlyph : Glyphs['?'];
            int glyphIndex = character - FirstCharacter;
            int originX = glyphIndex % AtlasColumns * GlyphWidth;
            int originY = glyphIndex / AtlasColumns * GlyphHeight;

            for (int row = 0; row < rows.Length; row++)
            {
                for (int column = 0; column < 5; column++)
                {
                    if ((rows[row] & (1 << (4 - column))) != 0)
                    {
                        int pixelIndex = (originY + row) * _texture.Width + originX + column + 1;
                        pixels[pixelIndex] = Color.White;
                    }
                }
            }
        }

        _texture.SetData(pixels);
    }

    public void DrawString(SpriteBatch spriteBatch, string text, Vector2 position, Color color, int scale = 1)
    {
        ArgumentNullException.ThrowIfNull(spriteBatch);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(scale);

        float startX = position.X;
        int cursorX = 0;
        int cursorY = 0;

        foreach (char character in text)
        {
            if (character == '\n')
            {
                cursorX = 0;
                cursorY += GlyphHeight * scale;
                continue;
            }

            if (character == '\r')
                continue;

            if (character == '\t')
            {
                cursorX += GlyphWidth * scale * 4;
                continue;
            }

            char glyphCharacter = char.ToUpperInvariant(character);
            if (glyphCharacter < FirstCharacter || glyphCharacter > LastCharacter)
                glyphCharacter = '?';

            int glyphIndex = glyphCharacter - FirstCharacter;
            Rectangle source = new(
                glyphIndex % AtlasColumns * GlyphWidth,
                glyphIndex / AtlasColumns * GlyphHeight,
                GlyphWidth,
                GlyphHeight);
            Vector2 drawPosition = new(startX + cursorX, position.Y + cursorY);
            spriteBatch.Draw(_texture, drawPosition, source, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            cursorX += GlyphWidth * scale;
        }
    }

    public void Dispose() => _texture.Dispose();
}
