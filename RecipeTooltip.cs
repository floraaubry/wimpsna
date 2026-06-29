using Blish_HUD;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using System;

namespace WhereIsMyPSNA
{
    internal class RecipeTooltip : Panel
    {
        private const int TipWidth  = 360;
        private const int Pad       = 9;
        private const int SmLineH   = 18;
        private const int LgLineH   = 24;
        private const int CoinSize  = 18;
        private const int IconSize  = 36;

        private readonly Image _itemIcon;
        private readonly Label _nameLabel;
        private readonly Label _descLabel;
        private readonly Label _durLabel;
        private readonly Label _bindLabel;

        private readonly Image _goldIcon;
        private readonly Label _goldLabel;
        private readonly Image _silverIcon;
        private readonly Label _silverLabel;
        private readonly Image _copperIcon;
        private readonly Label _copperLabel;

        public RecipeTooltip()
        {
            Parent          = GameService.Graphics.SpriteScreen;
            Visible         = false;
            ZIndex          = 9999;
            Width           = TipWidth;
            Height          = 60;
            BackgroundColor = new Color(0, 0, 0, 220);

            _itemIcon  = new Image { Parent = this, Size = new Point(IconSize, IconSize) };
            _nameLabel = new Label { Parent = this, Font = GameService.Content.DefaultFont18, WrapText = false };
            _descLabel = new Label { Parent = this, Font = GameService.Content.DefaultFont14, TextColor = Color.White,               WrapText = true  };
            _durLabel  = new Label { Parent = this, Font = GameService.Content.DefaultFont14, TextColor = new Color(220, 220, 60),   WrapText = false };
            _bindLabel = new Label { Parent = this, Font = GameService.Content.DefaultFont14, TextColor = Color.LightGray,           WrapText = false };

            _goldIcon    = new Image { Parent = this, Size = new Point(CoinSize, CoinSize) };
            _goldLabel   = new Label { Parent = this, Font = GameService.Content.DefaultFont14, TextColor = new Color(255, 215,   0) };
            _silverIcon  = new Image { Parent = this, Size = new Point(CoinSize, CoinSize) };
            _silverLabel = new Label { Parent = this, Font = GameService.Content.DefaultFont14, TextColor = new Color(192, 192, 192) };
            _copperIcon  = new Image { Parent = this, Size = new Point(CoinSize, CoinSize) };
            _copperLabel = new Label { Parent = this, Font = GameService.Content.DefaultFont14, TextColor = new Color(184, 115,  51) };
        }

        public void SetCoinTextures(
            Microsoft.Xna.Framework.Graphics.Texture2D gold,
            Microsoft.Xna.Framework.Graphics.Texture2D silver,
            Microsoft.Xna.Framework.Graphics.Texture2D copper)
        {
            _goldIcon.Texture   = gold;
            _silverIcon.Texture = silver;
            _copperIcon.Texture = copper;
        }

        public void SetRecipe(RecipeDef def, Microsoft.Xna.Framework.Graphics.Texture2D icon = null)
        {
            _itemIcon.Texture    = icon;
            _nameLabel.Text      = def.Name;
            _nameLabel.TextColor = GetRarityColor(def.Rarity);

            _descLabel.Text    = def.Description;
            _descLabel.Visible = !string.IsNullOrEmpty(def.Description);

            _durLabel.Text    = def.DurationSecs > 0 ? FormatDuration(def.DurationSecs) : "";
            _durLabel.Visible = def.DurationSecs > 0;

            string bindText    = FormatBinding(def.Binding);
            _bindLabel.Text    = bindText;
            _bindLabel.Visible = !string.IsNullOrEmpty(bindText);

            int g = def.VendorValue / 10000;
            int s = (def.VendorValue % 10000) / 100;
            int c = def.VendorValue % 100;

            _goldIcon.Visible    = g > 0;
            _goldLabel.Visible   = g > 0;
            _goldLabel.Text      = g.ToString();

            _silverIcon.Visible  = def.VendorValue >= 100;
            _silverLabel.Visible = def.VendorValue >= 100;
            _silverLabel.Text    = s.ToString();

            _copperIcon.Visible  = def.VendorValue > 0;
            _copperLabel.Visible = def.VendorValue > 0;
            _copperLabel.Text    = c.ToString();

            UpdateLayout();
        }

        private void UpdateLayout()
        {
            int textW = TipWidth - 2 * Pad;
            int y     = Pad;

            void Place(Label lbl, int lineH)
            {
                if (!lbl.Visible) return;
                lbl.Location = new Point(Pad, y);
                lbl.Size     = new Point(textW, lineH);
                y += lineH + 3;
            }

            // Icon + name on same row
            int headerH = Math.Max(IconSize, LgLineH);
            _itemIcon.Location  = new Point(Pad, y + (headerH - IconSize) / 2);
            int nameX           = Pad + IconSize + 4;
            _nameLabel.Location = new Point(nameX, y + (headerH - LgLineH) / 2);
            _nameLabel.Size     = new Point(TipWidth - nameX - Pad, LgLineH);
            y += headerH + 3;

            if (_descLabel.Visible)
            {
                y += 4;
                var font   = _descLabel.Font;
                float spW  = font.MeasureString(" ").Width;
                int lines  = 0;
                foreach (string para in _descLabel.Text.Split('\n'))
                {
                    int paraLines = 1;
                    float lineW   = 0;
                    foreach (string word in para.Split(' '))
                    {
                        if (word.Length == 0) continue;
                        float wordW = font.MeasureString(word).Width;
                        if (lineW > 0 && lineW + spW + wordW > textW)
                        { paraLines++; lineW = wordW; }
                        else
                        { lineW += (lineW > 0 ? spW : 0) + wordW; }
                    }
                    lines += paraLines;
                }
                lines = Math.Max(1, lines);
                _descLabel.Location = new Point(Pad, y);
                _descLabel.Size     = new Point(textW, SmLineH * lines);
                y += SmLineH * lines + 3;
            }

            Place(_durLabel,  SmLineH);
            Place(_bindLabel, SmLineH);

            if (_copperIcon.Visible)
            {
                int x     = Pad;
                int coinY = y + (SmLineH - CoinSize) / 2;

                void PlaceCoin(Image icon, Label label)
                {
                    if (!icon.Visible) return;
                    icon.Location  = new Point(x, coinY);
                    x += CoinSize + 2;
                    int labelW = label.Text.Length * 8 + 4;
                    label.Location = new Point(x, y);
                    label.Size     = new Point(labelW, SmLineH);
                    x += labelW + 4;
                }

                PlaceCoin(_goldIcon,   _goldLabel);
                PlaceCoin(_silverIcon, _silverLabel);
                PlaceCoin(_copperIcon, _copperLabel);

                y += SmLineH + 3;
            }

            Height = y + Pad;
        }

        public void MoveTo(int mouseX, int mouseY)
        {
            var screen = GameService.Graphics.SpriteScreen;
            int x = mouseX + TipWidth > screen.Width  ? mouseX - TipWidth : mouseX;
            int y = mouseY - Height   < 0             ? mouseY             : mouseY - Height;
            Location = new Point(Math.Max(0, x), Math.Max(0, y));
        }

        private static string FormatDuration(int secs)
        {
            int h = secs / 3600;
            int m = (secs % 3600) / 60;
            if (h > 0 && m > 0) return $"Duration: {h}h {m}min";
            if (h > 0)           return $"Duration: {h}h";
            return $"Duration: {m} min";
        }

        private static string FormatBinding(string binding) => binding switch
        {
            "AccountBound"       => "Account Bound",
            "SoulboundOnAcquire" => "Soulbound on Acquire",
            "SoulboundOnUse"     => "Soulbound on Use",
            _                    => "",
        };

        private static Color GetRarityColor(string rarity) => rarity switch
        {
            "Junk"       => new Color(170, 170, 170),
            "Fine"       => new Color( 98, 164, 218),
            "Masterwork" => new Color( 26, 147,   6),
            "Rare"       => new Color(252, 208,  11),
            "Exotic"     => new Color(255, 164,   5),
            "Ascended"   => new Color(251,  62, 141),
            "Legendary"  => new Color( 76,  19, 157),
            _            => Color.White,
        };
    }
}
