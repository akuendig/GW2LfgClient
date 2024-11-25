#nullable enable

using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using Blish_HUD;

namespace Gw2Lfg
{
    public class LandingView : Container
    {
        private const int PADDING = 10;

        public LandingView Build()
        {
            Size = Parent.ContentRegion.Size;

            var panel = new Panel
            {
                Parent = this,
                HeightSizingMode = SizingMode.AutoSize,
                WidthSizingMode = SizingMode.AutoSize,
            };

            // Icon
            //var icon = new Image(AsyncTexture2D.FromAssetId(157128)) // Key icon
            var icon = new Image() // Key icon
            {
                Parent = panel,
                Size = new Point(64, 64),
            };

            // Title
            var titleLabel = new Label
            {
                Parent = panel,
                Text = "API Key Required",
                Top = icon.Bottom + PADDING,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Font = GameService.Content.GetFont(ContentService.FontFace.Menomonia, ContentService.FontSize.Size20, ContentService.FontStyle.Regular),
            };

            // Instructions
            new Label
            {
                Parent = panel,
                Text = "To use the LFG module, please make sure to be logged in with your character,\n" +
                      "provide Blish HUD  with an API key with 'account' permissions,\n" +
                      "and give this addon permissions to your 'account'.\n\n" +
                      "1. Go to Account Settings in Guild Wars 2\n" +
                      "2. Generate a new API key with 'account' permissions\n" +
                      "3. The module will automatically connect once permissions are granted",
                Top = titleLabel.Bottom + PADDING,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                WrapText = true,
                Width = 400,
                Font = GameService.Content.GetFont(ContentService.FontFace.Menomonia, ContentService.FontSize.Size16, ContentService.FontStyle.Regular),
            };

            panel.Resized += (s, e) =>
            {
                panel.Left = (Width - panel.Width) / 2;
                panel.Top = 100;
            };

            return this;
        }
    }
}