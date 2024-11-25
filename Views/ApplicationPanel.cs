using Blish_HUD.Controls;
using Microsoft.Xna.Framework;

namespace Gw2Lfg
{
    public class ApplicationPanel : Panel
    {
        public Proto.GroupApplication Application { get; set; }

        public ApplicationPanel(Proto.GroupApplication application)
        {
            Application = application;
            Height = 60;
            ShowBorder = true;
        }
    }
}