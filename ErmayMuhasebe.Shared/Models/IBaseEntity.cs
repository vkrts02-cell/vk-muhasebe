using System;

namespace ErmayMuhasebe.Models
{
    public interface IBaseEntity
    {
        int Id { get; set; }
        long Version { get; set; }
        DateTime UpdatedAt { get; set; }
        string Uuid { get; set; }
        bool IsDeleted { get; set; }
    }
}
