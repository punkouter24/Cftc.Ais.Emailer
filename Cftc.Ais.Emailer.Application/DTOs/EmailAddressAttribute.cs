using System;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Cftc.Ais.Emailer.Application.DTOs
{
    public class EmailAddressAttribute : ValidationAttribute
    {
        private static readonly Regex EmailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            {
                return new ValidationResult("Email address is required.");
            }

            if (!EmailRegex.IsMatch(value.ToString()))
            {
                return new ValidationResult("Invalid email address format.");
            }

            return ValidationResult.Success;
        }
    }

  
}