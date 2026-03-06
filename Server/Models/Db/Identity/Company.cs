
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Server.Models.Db.Identity
{
    public enum PricingPlan
    {
        Team,
        Business,
        Enterprise
    }

    [Flags]
    public enum AdditionalModules
    {
        ThreatsProtection,
        AdvancedSearch,
        AdvancedAudit,
        AiAsistantPro,
        WhiteTag,
        AdditionalAPIs,
    }
    public enum CompanyStatus
    {
        Active,
        Closed,
        Archived,
        Initial,
    }
    public class Company
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int EmployeesCount { get; set; }
        public AdditionalModules AdditionalModules { get; set; }
        public int ExtendedStorageModules { get; set; } 
        public int UsedStorage {  get; set; }
        public int AvalibleStorage { get; set; }
        public CompanyStatus Status { get; set; }
        public DateTime DateTime { get; set; }
    }
}
