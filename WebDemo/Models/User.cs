namespace WebDemo.Models
{
    public class User
    {
        public int Id { get; set; }
        public string UserName { get; set; }
        public string EncodedKey { get; set; }
        public string HashedKey {get; set;} 
    }

    public class ILogin
    {
        public int Id { get; set; }
        public string HashedKey { get; set; }
    }

    public class ISignup  {
        public string UserName { get; set; }
        public string EncodedKey { get; set; }
        public string HashedKey { get; set; }

    }
}
