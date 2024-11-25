using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using System;

namespace Gw2Lfg.Views.Components
{
    public class GroupPanel : Panel
    {
        private const int PADDING = 10;
        private readonly Timer _statusUpdateTimer;

        public Proto.Group Group { get; set; }
        public Label StatusLabel;

        public GroupPanel(Proto.Group group)
        {
            Group = group;
            HeightSizingMode = SizingMode.AutoSize;
            ShowBorder = true;
        }

        public void UpdateStatus()
        {
            var lastHeartbeat = DateTimeOffset.FromUnixTimeSeconds(Group.UpdatedAtSec);
            var now = DateTimeOffset.UtcNow;
            var timeSinceHeartbeat = now - lastHeartbeat;

            if (timeSinceHeartbeat < TimeSpan.FromMinutes(2))
            {
                StatusLabel.Text = "Active";
                StatusLabel.TextColor = Color.Green;
            }
            else if (timeSinceHeartbeat < TimeSpan.FromMinutes(5))
            {
                StatusLabel.Text = "Away";
                StatusLabel.TextColor = Color.Yellow;
            }
            else
            {
                StatusLabel.Text = "Inactive";
                StatusLabel.TextColor = Color.Red;
            }
        }

        protected override void DisposeControl()
        {
            _statusUpdateTimer?.Dispose();
            base.DisposeControl();
        }
    }
}
