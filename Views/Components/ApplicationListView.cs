using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace Gw2Lfg.Components
{
    public class ApplicationListView : FlowPanel
    {
        private const int PADDING = 10;
        private readonly Dictionary<string, ApplicationPanel> _applicationPanels = [];
        private readonly Proto.Group _group;

        public ApplicationListView(Proto.Group group)
        {
            _group = group;
            FlowDirection = ControlFlowDirection.TopToBottom;
            ControlPadding = new Vector2(0, 5);
            ShowBorder = true;
            HeightSizingMode = SizingMode.Fill;
            WidthSizingMode = SizingMode.Fill;
        }

        public void UpdateApplications(IEnumerable<Proto.GroupApplication> applications)
        {
            foreach (var panel in _applicationPanels.Values)
            {
                panel.Dispose();
            }
            _applicationPanels.Clear();

            foreach (var application in applications)
            {
                var panel = new ApplicationPanel(application)
                {
                    Parent = this,
                    Width = Width - PADDING
                };
                _applicationPanels[application.Id] = panel;
            }

            SortApplications();
        }

        private void SortApplications()
        {
            SortChildren<ApplicationPanel>((a, b) =>
            {
                return -HasEnoughKillProof(a.Application.KillProof, _group)
                    .CompareTo(HasEnoughKillProof(b.Application.KillProof, _group));
            });
        }

        private static bool HasEnoughKillProof(Proto.KillProof kp, Proto.Group group)
        {
            if (group.KillProofMinimum == 0 || group.KillProofId == Proto.KillProofId.KpUnknown)
            {
                return true;
            }

            return group.KillProofId switch
            {
                Proto.KillProofId.KpLi => kp.Li >= group.KillProofMinimum,
                Proto.KillProofId.KpUfe => kp.Ufe >= group.KillProofMinimum,
                Proto.KillProofId.KpBskp => kp.Bskp >= group.KillProofMinimum,
                _ => false
            };
        }
    }
}
