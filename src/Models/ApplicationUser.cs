using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;
using TandemBooking.Services;

namespace TandemBooking.Models
{
    // Add profile data for application users by adding properties to the ApplicationUser class
    public class ApplicationUser : IdentityUser
    {
        public string Name { get; set; }
        public bool IsPilot { get; set; }
        public bool IsAdmin { get; set; }
        public bool IsPaymentAdmin { get; set; }
        public bool EmailNotification { get; set; }
        public bool SmsNotification { get; set; }
        public int? MinPassengerWeight { get; set; }
        public int? MaxPassengerWeight { get; set; }

        public Guid? VippsPaymentAccountId { get; set; }
        public PaymentAccount VippsPaymentAccount { get; set; }

        public Guid? IZettlePaymentAccountId { get; set; }
        public PaymentAccount IZettlePaymentAccount { get; set; }

        public ICollection<BookedPilot> Bookings { get; set; }
        public ICollection<PilotAvailability> Availabilities { get; set; }

        public bool InWeightRange(int? passengerWeight)
        {
            return (MinPassengerWeight ?? int.MinValue) <= (passengerWeight ?? int.MaxValue)
                && (passengerWeight ?? int.MinValue) <= (MaxPassengerWeight ?? int.MaxValue);
        }

        public string FormatWeightRange()
        {
            var ret = "";
            if (MinPassengerWeight != null && MaxPassengerWeight != null)
            {
                ret = $"{MinPassengerWeight.AsWeight()} - {MaxPassengerWeight.AsWeight()}";
            }
            else if (MinPassengerWeight != null)
            {
                ret = $"> {MinPassengerWeight.AsWeight()}";
            }
            else if (MaxPassengerWeight != null)
            {
                ret = $"< {MaxPassengerWeight.AsWeight()}";
            }
            if (ret.Length > 0)
            {
                ret += ", ";
            }
            return ret;
        }
    }

    
}