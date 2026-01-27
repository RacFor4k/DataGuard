using System.Text.Json.Serialization;

namespace WebDemo.Models
{
    
    public class Folder
    {
        [JsonIgnore]
        public int Id { get; set; }
        [JsonIgnore]
        public int OwnerId { get; set; }
        public string Path { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Tag { get; set; } = string.Empty;
    }
}
