using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace Gw2Lfg.Components
{
    public class GroupListView : FlowPanel
    {
        private const int PADDING = 10;
        private readonly Dictionary<string, GroupPanel> _groupPanels = [];
        private readonly LfgViewModel _viewModel;

        public GroupListView(LfgViewModel viewModel)
        {
            _viewModel = viewModel;
            FlowDirection = ControlFlowDirection.TopToBottom;
            ControlPadding = new Vector2(0, 5);
            ShowBorder = true;
            HeightSizingMode = SizingMode.Fill;
            WidthSizingMode = SizingMode.Fill;
        }

        public void UpdateGroups(IEnumerable<Proto.Group> groups, string searchText, string contentType)
        {
            foreach (var panel in _groupPanels.Values)
            {
                panel.Dispose();
            }
            _groupPanels.Clear();

            foreach (var group in groups)
            {
                var panel = new GroupPanel(group)
                {
                    Parent = this,
                    Width = Width - 20,
                };

                var visible = true;
                if (!string.IsNullOrEmpty(searchText))
                {
                    visible = group.Title.ToLower().Contains(searchText);
                }

                if (visible && contentType != "All")
                {
                    // Add content type filtering logic here
                }

                panel.Visible = visible;
                _groupPanels[group.Id] = panel;
            }

            RecalculateLayout();
        }
    }
}
