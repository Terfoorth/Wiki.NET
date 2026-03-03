using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Wiki_Blaze.Data;

namespace Wiki_Blaze.Data.Entities
{
    public class WikiAssignment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int WikiPageId { get; set; }

        [Required]
        [MaxLength(450)]
        public string AssigneeId { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public WikiAssignmentStatus Status { get; set; } = WikiAssignmentStatus.Open;

        [ForeignKey("WikiPageId")]
        public WikiPage? WikiPage { get; set; }

        [ForeignKey("AssigneeId")]
        public ApplicationUser? Assignee { get; set; }
    }

    public enum WikiAssignmentStatus
    {
        Open = 0,
        InProgress = 1,
        Done = 2,
        Blocked = 3
    }
}
