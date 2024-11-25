using Blish_HUD.Controls;
using Microsoft.Xna.Framework;

namespace Gw2Lfg.Views.Components
{
    public class ApplicationPanel : Panel
    {
        private const int PADDING = 10;
        public Proto.GroupApplication Application { get; set; }

        public ApplicationPanel(Proto.GroupApplication application)
        {
            Application = application;
            Height = 50;
            ShowBorder = true;
        }
    }
}
