namespace UmbrellaFrame.ModelSync.NotesExtension.Models
{
    public sealed class NotesUser
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Source { get; set; } = "visualstudio";
    }
}
