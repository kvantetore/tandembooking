﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TandemBooking.Models;

namespace TandemBooking.ViewModels.BookingAdmin
{
    public class CompleteBookingViewModel
    {
        public TandemBooking.Models.Booking Booking { get; set; }
        public int PassengerFee { get; set; }
        public FlightType FlightType { get; set; }
        public PaymentType PaymentType { get; set; }
        public string BoatDriver { get; set; }
        public string IZettleAccount { get; set; }
        public string VippsAccount { get; set; }
    }
}
