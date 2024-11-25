#nullable enable

using Blish_HUD.Controls;

namespace Gw2Lfg
{
    public class ApplicationListRowPanel : Panel
    {
        public Proto.GroupApplication Application { get; set; }

        public ApplicationListRowPanel(Proto.GroupApplication application)
        {
            Application = application;
            Height = 60;
            ShowBorder = true;
        }
    }
}