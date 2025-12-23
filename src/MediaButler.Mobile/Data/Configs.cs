using System.ComponentModel.DataAnnotations;

namespace MediaButler.Mobile.Data
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
