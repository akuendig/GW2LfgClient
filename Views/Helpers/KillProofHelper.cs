namespace Gw2Lfg.Views.Helpers
{
    public static class KillProofHelper
    {
        public static Proto.KillProofId ParseKillProofId(string value) => value switch
        {
            "LI" => Proto.KillProofId.KpLi,
            "UFE" => Proto.KillProofId.KpUfe,
            "BSKP" => Proto.KillProofId.KpBskp,
            _ => Proto.KillProofId.KpUnknown
        };

        public static string FormatKillProofId(Proto.KillProofId id) => id switch
        {
            Proto.KillProofId.KpLi => "LI",
            Proto.KillProofId.KpUfe => "UFE",
            Proto.KillProofId.KpBskp => "BSKP",
            _ => ""
        };

        public static string FormatKillProofRequirement(Proto.Group group)
        {
            if (group.KillProofMinimum == 0 || group.KillProofId == Proto.KillProofId.KpUnknown)
            {
                return "";
            }
            return $"{group.KillProofMinimum} {FormatKillProofId(group.KillProofId)}";
        }

        public static string FormatKillProofDetails(Proto.KillProof kp)
        {
            if (kp == null)
            {
                return "No KillProof.me data available";
            }
            return $"LI: {kp.Li}     UFE: {kp.Ufe}     BSKP: {kp.Bskp} \n" +
                   $"W1: {kp.W1}     W2:  {kp.W2}\n" +
                   $"W3: {kp.W3}     W4:  {kp.W4}\n" +
                   $"W5: {kp.W5}     W6:  {kp.W6}\n" +
                   $"W7: {kp.W7}     W8:  {kp.W8}";
        }

        public static bool HasEnoughKillProof(Proto.KillProof kp, Proto.Group? group)
        {
            if (group == null || group.KillProofMinimum == 0 || group.KillProofId == Proto.KillProofId.KpUnknown)
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
