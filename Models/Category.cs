using System.ComponentModel.DataAnnotations;

namespace ContactMVC.Models
{
    public class Category
    {
        // primary key
        public int Id { get; set; }

        // foreign key
        [Required]
        public string? AppUserId { get; set; }

        [Required]
        [Display(Name = "Category Name")]
        public string? Name { get; set; }

        // navigation properties
        public virtual AppUser? AppUser { get; set; }

        public virtual ICollection<Contact> Contacts { get; set; } = new HashSet<Contact>();
    }
}
