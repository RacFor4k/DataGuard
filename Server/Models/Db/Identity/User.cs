namespace Server.Models.Db.Identity
{
    public class User
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Surname { get; set; }
        public string Email { get; set; }
        public string PublicKey { get; set; }
        public string EncyptedToken { get; set; }
        public DateTime CreationTime { get; set; }
    }
}