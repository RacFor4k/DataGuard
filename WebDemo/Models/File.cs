using System.Text.Json.Serialization;

namespace WebDemo.Models
{
    public class File
    {
        [JsonIgnore]
        public int Id { get; set; }
        [JsonIgnore]
        public int OwnerId { get; set; }
        [JsonIgnore]
        public string Origin { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public int Size { get; set; }
    }

    public class NewFile
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public int Size { get; set; }
    }

    public class UploadFile 
    {
        public string Path { get; set; }
        public string FileName { get; set; }
        public int Index { get; set; }
        public IFormFile Chunk { get; set; } // Файл внутри модели
    }
}
