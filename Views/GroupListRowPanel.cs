
#nullable enable

using Blish_HUD;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Timers;

namespace Gw2Lfg
{
    public class GroupListRowPanel : Panel
    {
        private readonly Timer _statusUpdateTimer;

        public Proto.Group Group { get; set; }
        public Label StatusLabel;

        public GroupListRowPanel(Proto.Group group)
        {
            Group = group;
            HeightSizingMode = SizingMode.AutoSize;
            ShowBorder = true;

            // Update status every 10 seconds
            // TODO: We need this timer because if the user is inactive, the update_time will not be updated
            _statusUpdateTimer = new Timer(TimeSpan.FromSeconds(10).Milliseconds);
            _statusUpdateTimer.Elapsed += (s, e) => UpdateStatus();
            _statusUpdateTimer.Start();
            
            StatusLabel = new Label
            {
                Parent = this,
                Width = Width,
                Height = 30,
                VerticalAlignment = VerticalAlignment.Middle,
                Top = (Height - 30) / 2,
                Font = GameService.Content.GetFont(ContentService.FontFace.Menomonia, ContentService.FontSize.Size12, ContentService.FontStyle.Regular)
            };
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