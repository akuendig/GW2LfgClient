using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using Blish_HUD;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;
using Blish_HUD.Modules.Managers;
using System;
using System.ComponentModel;
using System.Linq;

namespace Gw2Lfg
{
    public class LfgView : View
    {
        private readonly LfgClient _client;
        private readonly LfgViewModel _viewModel;
        private readonly Task _listenerTask;
        private readonly Gw2ApiManager _gw2ApiManager;
        private readonly IDisposable _accountNameSubscription;

        public LfgView(LfgClient client, LfgViewModel viewModel, Gw2ApiManager gw2ApiManager)
        {
            _client = client;
            _viewModel = viewModel;
            _gw2ApiManager = gw2ApiManager;

            _viewModel.ApiKeyChanged += ApiKeyChanged;
        }

        protected override void Unload()
        {
            _listenerTask?.Dispose();
            _accountNameSubscription?.Dispose();
        }

        private void ApiKeyChanged(object sender, PropertyChangedEventArgs e)
        {
            _client.ApiKey=_viewModel.ApiKey;
        }

        protected override void Build(Blish_HUD.Controls.Container buildPanel)
        {
            var leftPanel = new Panel
            {
                Parent = buildPanel,
                Width = (int)(buildPanel.ContentRegion.Width * 0.6f),
                Height = buildPanel.ContentRegion.Height,
            };
            var rightPanel = new Panel
            {
                Parent = buildPanel,
                Left = leftPanel.Right + 40,
                Width = buildPanel.ContentRegion.Width - leftPanel.Width - 40,
                Height = buildPanel.ContentRegion.Height,
                ShowBorder = true,
            };
            BuildGroupPanel(leftPanel);
            BuildGroupManagementPanel(rightPanel);
        }

        private void BuildGroupPanel(Panel parent)
        {
            var groupListPanel = new Panel
            {
                Parent = parent,
                Height = parent.Height - 10,
                Width = parent.Width - 20,
                Left = 10,
                Top = 5,
            };

            var y = 0;
            var filterPanel = new Panel
            {
                Parent = groupListPanel,
                Top = y,
                Height = 40,
                Width = groupListPanel.Width,
            };
            y += filterPanel.Height + 10;

            var contentTypeDropdown = new Dropdown
            {
                Parent = filterPanel,
                Top = 5,
                Height = 30,
                Width = 120,
            };
            contentTypeDropdown.Items.Add("All");
            contentTypeDropdown.Items.Add("Fractals");
            contentTypeDropdown.Items.Add("Raids");
            contentTypeDropdown.Items.Add("Strike Missions");
            contentTypeDropdown.Items.Add("Open World");

            var searchBox = new TextBox
            {
                Parent = filterPanel,
                Left = contentTypeDropdown.Right + 10,
                Top = 5,
                Height = 30,
                Width = filterPanel.Width - contentTypeDropdown.Right - 20,
                PlaceholderText = "Search groups...",
            };

            var groupsFlowPanel = new FlowPanel
            {
                Parent = groupListPanel,
                Top = y,
                Height = groupListPanel.Height - y,
                Width = groupListPanel.Width,
                FlowDirection = ControlFlowDirection.TopToBottom,
                ControlPadding = new Vector2(0, 5),
                ShowBorder = true,
                Padding = new Thickness(10, 90, 0, 10),
            };
            BuildGroupPanels(groupsFlowPanel, _viewModel.Groups);
            var updateGroups = new Action(() =>
            {
                var filteredGroups = _viewModel.Groups.Where(
                    g => g.Title.ToLower().Contains(searchBox.Text.Trim().ToLower())
                ).ToArray();
                BuildGroupPanels(groupsFlowPanel, filteredGroups);
            });
            searchBox.TextChanged += (sender, e) => updateGroups();
            _viewModel.GroupsChanged += (sender, e) => updateGroups();
        }
        private void BuildGroupPanels(FlowPanel parent, IEnumerable<Proto.Group> groups)
        {
            parent.ClearChildren();
            foreach (var group in groups)
            {
                BuildGroupListPanel(parent, group);
            }
        }

        private void BuildGroupListPanel(FlowPanel parent, Proto.Group group)
        {
            var groupPanel = new Panel
            {
                Parent = parent,
                HeightSizingMode = SizingMode.AutoSize,
                Width = parent.Width,
                ShowBorder = true,
            };
            var groupInfoPanel = new Panel
            {
                Parent = groupPanel,
                Left = 10,
                Top = 5,
                Width = groupPanel.Width - (100 + 10),
            };
            var titleLabel = new Label
            {
                Parent = groupInfoPanel,
                Text = group.Title,
                AutoSizeHeight = true,
                Width = groupInfoPanel.Width,
                Font = GameService.Content.GetFont(ContentService.FontFace.Menomonia, ContentService.FontSize.Size16, ContentService.FontStyle.Regular),
            };
            int y = titleLabel.Bottom;
            string kpString = KpString(group.KillProofId, group.KillProofMinimum);
            if (kpString.Length > 0)
            {
                var requirementsLabel = new Label
                {
                    Parent = groupInfoPanel,
                    Text = KpString(group.KillProofId, group.KillProofMinimum),
                    Top = y,
                    AutoSizeHeight = true,
                    Width = groupInfoPanel.Width,
                    Font = GameService.Content.GetFont(ContentService.FontFace.Menomonia, ContentService.FontSize.Size12, ContentService.FontStyle.Regular),
                };
                y += requirementsLabel.Height;
            }
            groupInfoPanel.Height = y + 10;
            var buttonPanel = new Panel
            {
                Parent = groupPanel,
                Left = groupPanel.Width - (100 + 20),
                Width = 100,
                Height = groupInfoPanel.Height,
            };
            var applyButton = new StandardButton
            {
                Parent = buttonPanel,
                Top = (buttonPanel.Height - 30) / 2,
                Width = 100,
                Height = 30,
                Text = "Apply",
            };
        }

        private void BuildGroupManagementPanel(Panel parent)
        {
            var groupManagementPanel = new Panel()
            {
                Parent = parent,
                Height = parent.Height,
                Width = parent.Width - 10,
            };
            var createGroupPanel = BuildCreateGroupPanel(groupManagementPanel);
            createGroupPanel.Show();
            var groupDetailPanel = BuildGroupDetailPanel(groupManagementPanel, _viewModel.Groups.First());
        }

        private Panel BuildCreateGroupPanel(Panel parent)
        {
            var paddingPanel = new Panel
            {
                Parent = parent,
                Width = parent.Width,
                Height = parent.Height,
                Title = "Create Group",
            };

            var createGroupPanel = new Panel
            {
                Parent = paddingPanel,
                Top = 10,
                Left = 10,
                Width = parent.Width - 20,
                Height = parent.Height - 20,
                Visible = false,
            };

            int y = 0;

            var descriptionBox = new TextBox
            {
                Parent = createGroupPanel,
                Top = y,
                Height = 30,
                Width = createGroupPanel.Width,
                PlaceholderText = "Group Description"
            };
            y += descriptionBox.Height + 10;

            var requirementsPanel = new Panel
            {
                Parent = createGroupPanel,
                Top = y,
                Height = 30,
                Width = createGroupPanel.Width,
            };
            y += requirementsPanel.Height + 10;

            var requirementsLabel = new Label
            {
                Parent = requirementsPanel,
                Text = "Required KP:",
                Height = 30,
                Width = 70,
                Font = GameService.Content.GetFont(ContentService.FontFace.Menomonia, ContentService.FontSize.Size12, ContentService.FontStyle.Regular),
            };

            var requirementsNumber = new TextBox
            {
                Parent = requirementsPanel,
                Width = 50,
                Height = 30,
                Text = "",
                PlaceholderText = "0",
                Left = requirementsPanel.Width - (50 + 90 + 10),
            };

            var requirementsDropdown = new Dropdown
            {
                Parent = requirementsPanel,
                Width = 90,
                Height = 30,
                Left = requirementsPanel.Width - (90),
            };
            requirementsDropdown.Items.Add("");
            requirementsDropdown.Items.Add("UFE");
            requirementsDropdown.Items.Add("BSKP");
            requirementsDropdown.Items.Add("LI");

            var createButtonPanel = new Panel
            {
                Parent = createGroupPanel,
                Top = y,
                Width = createGroupPanel.Width,
                Height = 30,
            };
            var createButton = new StandardButton
            {
                Parent = createButtonPanel,
                Width = 100,
                Height = 30,
                Left = (createButtonPanel.Width - 120) / 2,
                Text = "Create"
            };
            createButton.Click += async (sender, e) =>
            {
                var kpId = StringToKpId(requirementsDropdown.SelectedItem);
                var minKp = 0u;
                uint.TryParse(requirementsNumber.Text, out minKp);
                try
                {
                    var group = await _client.CreateGroup(descriptionBox.Text, minKp, kpId);
                }
                catch (Exception ex)
                {
                    ScreenNotification.ShowNotification(ex.Message);
                }
            };

            // This panel overlays the create group panel since only
            // one of the two panels should be visible at a time.
            var manageButtonPanel = new Panel
            {
                Parent = createGroupPanel,
                Top = y,
                Width = createGroupPanel.Width,
                Height = createButtonPanel.Height,
                Visible = false,
            };
            var updateButton = new StandardButton
            {
                Parent = manageButtonPanel,
                Width = 100,
                Height = 30,
                Left = (manageButtonPanel.Width - (100 + 10 + 100)) / 2,
                Text = "Update"
            };
            var cancelButton = new StandardButton
            {
                Parent = manageButtonPanel,
                Width = 100,
                Height = 30,
                Left = 100 + 10 + (manageButtonPanel.Width - 100) / 2,
                Text = "Cancel"
            };
            y += createButtonPanel.Height + 40;

            var applicationsLabel = new Label
            {
                Parent = createGroupPanel,
                Text = "Applications",
                Top = y,
                AutoSizeHeight = true,
                Width = createGroupPanel.Width,
                Font = GameService.Content.GetFont(ContentService.FontFace.Menomonia, ContentService.FontSize.Size16, ContentService.FontStyle.Regular),
            };
            y += applicationsLabel.Height + 10;

            var applicationsList = new FlowPanel
            {
                Parent = createGroupPanel,
                Top = y,
                Height = createGroupPanel.Height - y,
                Width = createGroupPanel.Width,
                FlowDirection = ControlFlowDirection.TopToBottom,
                ControlPadding = new Vector2(10, 5),
                ShowBorder = true,
            };
            BuildApplicantPanel(applicationsList);

            return createGroupPanel;
        }

        private Panel BuildApplicantPanel(FlowPanel parent)
        {
            var applicantPanel = new Panel
            {
                Parent = parent,
                HeightSizingMode = SizingMode.AutoSize,
                Width = parent.Width - 20,
            };
            var applicantName = new Label
            {
                Parent = applicantPanel,
                Text = "Applicant Name",
                Height = 30,
                Width = 150,
                Font = GameService.Content.GetFont(ContentService.FontFace.Menomonia, ContentService.FontSize.Size16, ContentService.FontStyle.Regular),
            };
            return applicantPanel;
        }

        private Panel BuildGroupDetailPanel(Panel parent, Proto.Group group)
        {
            var groupDetailPanel = new FlowPanel
            {
                Parent = parent,
                Size = parent.Size,
                FlowDirection = ControlFlowDirection.TopToBottom,
                ControlPadding = new Vector2(10, 10),
                OuterControlPadding = new Vector2(10, 0),
                Visible = false,
            };
            // Display group info
            var titleLabel = new Label
            {
                Parent = groupDetailPanel,
                Text = group.Title,
                Size = new Point(groupDetailPanel.Width - 20, 30),
            };

            var requirementsLabel = new Label
            {
                Parent = groupDetailPanel,
                Text = KpString(group.KillProofId, group.KillProofMinimum),
                Size = new Point(groupDetailPanel.Width - 20, 30),
            };

            // Add apply buttons for each role
            var buttonPanel = new FlowPanel
            {
                Parent = groupDetailPanel,
                Size = new Point(groupDetailPanel.Width - 20, 30),
                FlowDirection = ControlFlowDirection.LeftToRight
            };
            return groupDetailPanel;
        }

        private static string KpString(Proto.KillProofId id, uint min)
        {
            if (min == 0 || id == Proto.KillProofId.KpUnknown) return "";
            return min + " " + KpIdToString(id);
        }

        private static string KpIdToString(Proto.KillProofId id)
        {
            switch (id)
            {
                case Proto.KillProofId.KpUfe:
                    return "UFE";
                case Proto.KillProofId.KpBskp:
                    return "BSKP";
                case Proto.KillProofId.KpLi:
                    return "LI";
                default:
                    return "";
            }
        }

        private static Proto.KillProofId StringToKpId(string id)
        {
            switch (id)
            {
                case "UFE":
                    return Proto.KillProofId.KpUfe;
                case "BSKP":
                    return Proto.KillProofId.KpBskp;
                case "LI":
                    return Proto.KillProofId.KpLi;
                default:
                    return Proto.KillProofId.KpUnknown;
            }
        }
    }
}