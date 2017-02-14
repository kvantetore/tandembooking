using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TandemBooking.Models;
using Microsoft.EntityFrameworkCore;
using TandemBooking.Services;

namespace TandemBooking.Controllers
{
    [Authorize(Policy = "IsAdmin")]
    [Authorize(Policy = "IsPilot")]
    public class ReportController : Controller
    {
        private readonly TandemBookingContext _context;
        private readonly UserManager _userManager;

        public ReportController(TandemBookingContext context, UserManager userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public IActionResult CompletedBookings(string pilotId = null)
        {
            var fromDate = new DateTime(DateTime.Today.Year, 1, 1);
            var toDate = DateTime.Today;

            var bookings = _context.Bookings
                .Include(b => b.AssignedPilot)
                .AsNoTracking()
                .Where(b => !b.Canceled && b.BookingDate >= fromDate && b.BookingDate < toDate);

            //Pilots should be able to see their own flights
            if (!User.IsAdmin())
            {
                pilotId = _userManager.GetUserId(User);
            }
                
            if (pilotId != null)
            {
                bookings = bookings.Where(b => b.AssignedPilot.Id == pilotId);
            }

            var orderedBookings = bookings
                .OrderBy(b => b.BookingDate)
                .ToList();

            return View(orderedBookings);
        }

        [Authorize(Policy = "IsAdmin")]
        public IActionResult BookingsByPilot()
        {
            var fromDate = new DateTime(DateTime.Today.Year, 1, 1);
            var toDate = DateTime.Today;

            var bookings = _context.Bookings
                .Include(b => b.AssignedPilot)
                .AsNoTracking()
                .Where(b => !b.Canceled && b.AssignedPilot != null && b.BookingDate >= fromDate && b.BookingDate < toDate)
                .GroupBy(b => b.AssignedPilot)
                .Select(grp => new BookingsByPilotViewModel
                {
                    PilotId = grp.Key.Id,
                    PilotName = grp.Key.Name,
                    Flights = grp.Count()
                })
                .ToList()
                .OrderByDescending(b => b.Flights)
                .ToList();

            return View(bookings);
        }
    }

    public class BookingsByPilotViewModel
    {
        public string PilotId { get; set; }
        public string PilotName { get; set; }
        public int Flights { get; set; }
    }
}