using FluentValidation;
using Omni_MVC_2.Models;
using Omni_MVC_2.Utilities.ValidatorUtilities;

namespace Omni_MVC_2.Validators
{
    public class UserProfileInputValidator : ModelValidator<UserProfileInputVM>
    {
        public UserProfileInputValidator()
        {
            RuleFor(x => x.UserName)
                .NotEmpty().WithMessage("Username is required")
                .Length(3, 20).WithMessage("Username must be between 3 and 20 characters");

            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email is required")
                .EmailAddress().WithMessage("Invalid email format");

            RuleFor(x => x.Age)
                .InclusiveBetween(18, 100).WithMessage("Age must be between 18 and 100");
        }
    }
}
