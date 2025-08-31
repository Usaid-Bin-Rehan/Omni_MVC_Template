using System.ComponentModel.DataAnnotations;

namespace Omni_MVC_2.Utilities.RepositoryUtilities
{
    public abstract class Base<T>
    {
        public Base() { }
        public Base(T pId, string pCreatedBy, DateTime pCreatedDate, bool pIsActive)
        {
            Id = pId;
            CreatedBy = pCreatedBy;
            CreatedDate = pCreatedDate;
            UpdatedBy = pCreatedBy;
            UpdatedDate = pCreatedDate;
            IsActive = pIsActive;
        }

        [Key]
        public virtual required T Id { get; set; }

        [Required(ErrorMessage = $"{nameof(CreatedBy)} is required")]
        public virtual required string CreatedBy { get; set; }

        [Required(ErrorMessage = $"{nameof(UpdatedBy)} is required")]
        public virtual required string UpdatedBy { get; set; }

        [Required(ErrorMessage = $"{nameof(CreatedDate)} is required")]
        public virtual DateTime CreatedDate { get; set; }

        [Required(ErrorMessage = $"{nameof(UpdatedDate)} is required")]
        public virtual DateTime UpdatedDate { get; set; }

        [Required(ErrorMessage = $"{nameof(IsActive)} is required")]
        public virtual bool IsActive { get; set; }

        [Required(ErrorMessage = $"{nameof(IsArchived)} is required")]
        public virtual bool IsArchived { get; set; }

        public void SetSoftDelete()
        {
            IsActive = false;
        }

        public void CreateRecordStatus(string pCreatedBy)
        {
            CreatedBy = pCreatedBy;
            CreatedDate = DateTime.UtcNow;
            UpdatedBy = pCreatedBy;
            UpdatedDate = DateTime.UtcNow;
        }

        public void UpdateRecordStatus(string pUpdatedBy)
        {
            UpdatedBy = pUpdatedBy;
            UpdatedDate = DateTime.UtcNow;
        }
    }
}