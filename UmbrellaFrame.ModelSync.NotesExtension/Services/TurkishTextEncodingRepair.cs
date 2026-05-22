namespace UmbrellaFrame.ModelSync.NotesExtension.Services
{
    public static class TurkishTextEncodingRepair
    {
        public static string RepairMojibake(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            return value
                .Replace("Ã¼", "ü")
                .Replace("Ãœ", "Ü")
                .Replace("Ä±", "ı")
                .Replace("Ä°", "İ")
                .Replace("ÅŸ", "ş")
                .Replace("Åž", "Ş")
                .Replace("Ã§", "ç")
                .Replace("Ã‡", "Ç")
                .Replace("Ã¶", "ö")
                .Replace("Ã–", "Ö")
                .Replace("ÄŸ", "ğ")
                .Replace("Äž", "Ğ");
        }
    }
}
