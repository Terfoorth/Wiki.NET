using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;

namespace Wiki_Blaze.Data.Entities
{
    public class WikiPage
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Ein Titel ist erforderlich.")]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        // Speichert den Inhalt des DxRichEdit (oft als Byte[] für OpenXML/Docx oder HTML string)
        // Wir verwenden hier byte[], um volle Kompatibilität mit DxRichEdit SaveDocumentAsync zu gewährleisten.
        public byte[]? Content { get; set; }

        // Reiner Text für Vorschaukarten und Svuche
        public string? PreviewText { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastModified { get; set; } = DateTime.UtcNow;

        public WikiPageStatus Status { get; set; }

        public WikiEntryType EntryType { get; set; } = WikiEntryType.Standard;

        public bool IsEditLocked { get; set; }

        public string? FormSchemaJson { get; set; }

        // Verknüpfung zur Kategorie
        [Required(ErrorMessage = "Eine Kategorie muss ausgewählt werden.")]
        public int CategoryId { get; set; }

        [ForeignKey("CategoryId")]
        public virtual WikiCategory? Category { get; set; }

        // Optional: Verknüpfung zum User (Identity)
        public string? AuthorId { get; set; }

        [NotMapped]
        public string? AuthorDisplayName { get; set; }

        public string? OwnerId { get; set; }

        [NotMapped]
        public string? OwnerDisplayName { get; set; }

        public WikiPageVisibility Visibility { get; set; }

        public int? TemplateGroupId { get; set; }

        [ForeignKey("TemplateGroupId")]
        public WikiTemplateGroup? TemplateGroup { get; set; }

        public ICollection<WikiPageAttributeValue> AttributeValues { get; set; } = new List<WikiPageAttributeValue>();
        public ICollection<WikiComment> Comments { get; set; } = new List<WikiComment>();
        public ICollection<WikiAssignment> Assignments { get; set; } = new List<WikiAssignment>();
        public ICollection<WikiChangeLog> ChangeLogs { get; set; } = new List<WikiChangeLog>();

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }


    public enum WikiPageVisibility
    {
        Private = 0,
        Team = 1,
        Public = 2
    }

    public enum WikiEntryType
    {
        Standard = 0,
        Form = 1
    }
}
