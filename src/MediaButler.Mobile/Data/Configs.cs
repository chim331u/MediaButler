using FC_APP.Data;
using System.ComponentModel.DataAnnotations;

namespace FC_APP.Data
{
    public class Configs : BaseEntity
    {
        [Key]
        public int Id { get; set; }
        public string Key { get; set; }
        public string Value { get; set; }
        public bool IsDev { get; set; }

    }
}
