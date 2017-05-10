﻿using System;
using System.ComponentModel.DataAnnotations;

namespace TandemBooking.ViewModels.BookingAdmin
{
    public class EditBookingViewModel
    {
        public Guid Id { get; set; }

        [Required]
        public DateTime BookingDate { get; set; }
        public string PassengerName { get; set; }
        public string PassengerEmail { get; set; }
        public string PassengerPhone { get; set; }
    }
}