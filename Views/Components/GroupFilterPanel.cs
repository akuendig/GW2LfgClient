using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using System;

namespace Gw2Lfg.Components
{
    public class GroupFilterPanel : Panel
    {
        private const int PADDING = 10;
        private TextBox? _searchBox;
        private Dropdown? _contentTypeDropdown;
        public event EventHandler? FiltersChanged;

        public string SearchText => _searchBox?.Text.Trim().ToLower() ?? "";
        public string ContentType => _contentTypeDropdown?.SelectedItem ?? "All";

        public GroupFilterPanel()
        {
            Height = 40;
            WidthSizingMode = SizingMode.Fill;
            BuildUI();
        }

        private void BuildUI()
        {
            _contentTypeDropdown = new Dropdown
            {
                Parent = this,
                Top = 5,
                Height = 30,
                Width = 120,
            };

            PopulateContentTypeDropdown();

            _searchBox = new TextBox
            {
                Parent = this,
                Left = _contentTypeDropdown.Right + PADDING,
                Top = 5,
                Height = 30,
                Width = Width - _contentTypeDropdown.Right - (PADDING * 2),
                PlaceholderText = "Search groups...",
            };

            var debounceTimer = new System.Timers.Timer(300);
            debounceTimer.Elapsed += (s, e) =>
            {
                debounceTimer.Stop();
                FiltersChanged?.Invoke(this, EventArgs.Empty);
            };

            _searchBox.TextChanged += (s, e) =>
            {
                debounceTimer.Stop();
                debounceTimer.Start();
            };

            _contentTypeDropdown.ValueChanged += (s, e) => FiltersChanged?.Invoke(this, EventArgs.Empty);
        }

        private void PopulateContentTypeDropdown()
        {
            _contentTypeDropdown!.Items.Add("All");
            _contentTypeDropdown.Items.Add("Fractals");
            _contentTypeDropdown.Items.Add("Raids");
            _contentTypeDropdown.Items.Add("Strike Missions");
            _contentTypeDropdown.Items.Add("Open World");
            _contentTypeDropdown.SelectedItem = "All";
        }
    }
}
