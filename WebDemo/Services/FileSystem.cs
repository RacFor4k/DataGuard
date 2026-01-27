using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace WebDemo.Services
{
    static public class FileSystem
    {
        private const string _storage_path = "storage\\";
        static private readonly SHA256 sha = SHA256.Create();
        static public string? NewFile(string path, string fileName)
        {
            Directory.CreateDirectory(_storage_path);
            string full_path = _storage_path+Base64Url.EncodeToString(sha.ComputeHash(Encoding.UTF8.GetBytes(path+ fileName)));
            if (File.Exists(full_path))
                return null;
            File.Create(full_path).DisposeAsync();
            return full_path;
        }
        static public async Task<bool> WriteChunk(string path, string fileName, Stream data)
        {
            string full_path = _storage_path + Base64Url.EncodeToString(sha.ComputeHash(Encoding.UTF8.GetBytes(path+ fileName)));
            if(!File.Exists(full_path)) return false;
            using (var file = File.Open(full_path, FileMode.Append, FileAccess.Write, FileShare.Read))
            {
                byte[] buffer = new byte[1024 * 1024 * 5];
                await data.ReadAsync(buffer);
                await file.WriteAsync(buffer, 0, buffer.Length);
            }
            return true;
        }
    }
}
