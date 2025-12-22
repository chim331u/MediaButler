using System.ComponentModel.DataAnnotations;

namespace FC_APP.Data
{
    public class FilesDetailDto
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; }
        public double FileSize { get; set; }
        public string FileCategory { get; set; }
        public bool IsToCategorize { get; set; }
        public bool IsNew { get; set; }
        public bool IsNotToMove { get; set; }
    }
}
