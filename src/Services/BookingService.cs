﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Claims;
using System.Threading.Tasks;
using TandemBooking.Models;
using Microsoft.EntityFrameworkCore;

namespace TandemBooking.Services
{
    public class AvailablePilot
    {
        public ApplicationUser Pilot { get; set; }
        public int Priority { get; set; } 
        public bool Available { get; set; }
    }

    public class BookingService
    {
        private readonly TandemBookingContext _context;
        private readonly UserManager _userManager;
        private readonly BookingServiceDb _bookingServiceDb;

        public BookingService(TandemBookingContext context, UserManager userManager, BookingServiceDb bookingServiceDb)
        {
            _context = context;
            _userManager = userManager;
            _bookingServiceDb = bookingServiceDb;
        }

        public async Task<List<AvailablePilot>> FindAvailablePilotsAsync(DateTime date, Location location, bool includeUnavailable = false)
        {
            return await FindAvailablePilotsAsync(date, location?.Id, includeUnavailable);
        }

        public async Task<List<AvailablePilot>> FindAvailablePilotsAsync(DateTime date, Guid? locationId, bool includeUnavailable = false)
        {
            var result = await _bookingServiceDb.GetAvailablePilotsAsync(date, locationId);
            if (includeUnavailable)
            {
                return result;
            }
            else
            {
                return result
                    .Where(a => a.Available)
                    .ToList();
            }
        }

        public async Task<ApplicationUser> AssignNewPilotAsync(Booking booking)
        {
            return (await AssignNewPilotAsync(new List<Booking>() {booking})).FirstOrDefault();
        }

        public async Task<List<ApplicationUser>> AssignNewPilotAsync(IList<Booking> bookings)
        {
            if (bookings.Count == 0)
            {
                throw new Exception("No bookings to assign pilots to");
            }
            var date = bookings.First().BookingDate;
            if (bookings.Any(b => b.BookingDate != date))
            {
                throw new Exception("All bookings must be on the same date");
            }

            var location = bookings.First().BookingLocation;
            if (bookings.Any(b => b.BookingLocation?.Id != location?.Id))
            {
                throw new Exception("All bookings must be on the same location");
            }


            var spentPilots = bookings.SelectMany(b =>
                        b.BookedPilots?.Select(bp => bp.Pilot).ToList() ?? new List<ApplicationUser>()
            );

            var availablePilots = (await FindAvailablePilotsAsync(date, location))
                .Where(ap => !spentPilots.Contains(ap.Pilot))
                .ToList();

            var assignedPilots = new List<ApplicationUser>();
            foreach (var booking in bookings)
            {
                //pilots who can fly this passenger
                var applicablePilots = availablePilots
                    .Where(pa => pa.Pilot.InWeightRange(booking.PassengerWeight))
                    ;

                //find pilots with the lowest priority (lower is better)
                var prioritizedPilots = applicablePilots
                    .GroupBy(pa => pa.Priority)
                    .OrderBy(grp => grp.Key)
                    .FirstOrDefault()
                    ?.ToList() ?? new List<AvailablePilot>()
                    ;

                //select randomly from lowest priority pilots
                var selectedPilot = prioritizedPilots.Count > 0
                    ? prioritizedPilots[new Random().Next(prioritizedPilots.Count - 1)].Pilot
                    : null;

                AssignNewPilot(booking, selectedPilot);

                if (selectedPilot != null)
                {
                    assignedPilots.Add(selectedPilot);

                    //don't select this pilot for another booking in this group
                    availablePilots.RemoveAll(ap => ap.Pilot == selectedPilot);
                }
            }

            return assignedPilots;
        }

        public void AssignNewPilot(Booking booking, ApplicationUser pilot)
        {
            //set all other booked pilots to canceled
            foreach (var bookedPilot in _context.BookedPilots.Where(b => b.Booking.Id == booking.Id))
            {
                bookedPilot.Canceled = true;
            }

            //set as currently assigned pilot
            booking.AssignedPilot = pilot;

            if (pilot != null)
            {
                //add to list of pilots assigned to this booking
                if (booking.BookedPilots == null)
                {
                    booking.BookedPilots = new List<BookedPilot>();
                }
                booking.BookedPilots.Add(new BookedPilot()
                {
                    AssignedDate = DateTime.UtcNow,
                    Pilot = pilot,
                });
            }
        }

        public void AddEvent(Booking booking, ClaimsPrincipal user, string message)
        {
            var userId = user == null ? null : _userManager.GetUserId(user);
            booking.BookingEvents.Add(new BookingEvent()
            {
                User = _context.Users.FirstOrDefault(u => u.Id == userId),
                EventDate = DateTime.UtcNow,
                EventMessage = message,
            });
        }
    }
}
