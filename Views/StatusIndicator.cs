
using System;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;

namespace Gw2Lfg
{
    public class StatusIndicator : Container
    {
        private readonly Label _label;

        public StatusIndicator()
        {
            WidthSizingMode = SizingMode.AutoSize;
            _label = new Label
            {
                Parent = this,
                AutoSizeWidth = true,
                VerticalAlignment = VerticalAlignment.Middle
            };
        }

        public void UpdateStatus(bool connected, DateTimeOffset lastHeartbeat)
        {
            var now = DateTimeOffset.UtcNow;
            var timeSinceHeartbeat = now - lastHeartbeat;

            if (!connected)
            {
                _label.Text = "Disconnected";
                _label.TextColor = Color.Red;
            }
            else if (timeSinceHeartbeat > TimeSpan.FromSeconds(30))
            {
                _label.Text = "Connection Lost";
                _label.TextColor = Color.Red;
            }
            else
            {
                _label.Text = "Connected";
                _label.TextColor = Color.Green;
            }
        }
    }
}