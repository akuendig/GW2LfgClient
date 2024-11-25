using Blish_HUD.Controls;

namespace Gw2Lfg {
    static class Notifications {
        public static void ShowError(string message) {
            ScreenNotification.ShowNotification(message, ScreenNotification.NotificationType.Error);
        }

        public static void ShowInfo(string message) {
            ScreenNotification.ShowNotification(message, ScreenNotification.NotificationType.Info);
        }
    }
}