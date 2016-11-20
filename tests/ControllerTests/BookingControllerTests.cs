﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using tandembooking.Migrations;
using TandemBooking.Controllers;
using TandemBooking.Services;
using TandemBooking.Tests.TestData;
using TandemBooking.Tests.TestSetup;
using TandemBooking.ViewModels.Booking;
using Xunit;

namespace TandemBooking.Tests.ControllerTests
{
    [Collection("Integration Tests")]
    public class BookingTests : IntegrationTestBase
    {
        public BookingTests(IntegrationTestFixture fixture) : base(fixture)
        {
            _pilots = new PilotsFixture(Context);
        }

        private readonly PilotsFixture _pilots;

        [Fact]
        public async Task CreateSuccessfulSimpleBookingWithNoAvailablePilot()
        {
            var input = new BookingViewModel
            {
                Date = new DateTime(2016, 11, 13),
                Name = "My Name",
                PhoneNumber = "11111111",
                Email = "passenger@example.com",
                Comment = "Blah"
            };

            var ctrl = GetService<BookingController>();
            var result = await ctrl.Index(input);

            //Assert booking is created
            Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(1, Context.Bookings.Count());

            var booking = Context.Bookings
                .Include(b => b.AssignedPilot)
                .Include(b => b.AdditionalBookings)
                .Include(b => b.BookedPilots)
                .Include(b => b.BookingEvents)
                .First();

            Assert.Equal(null, booking.AssignedPilot); // no pilots available
            Assert.Equal("passenger@example.com", booking.PassengerEmail);
            Assert.Equal("4711111111", booking.PassengerPhone);
            Assert.Equal("Blah", booking.Comment);

            //Assert sms is sent
            var nexmoService = (MockNexmoService) GetService<INexmoService>();

            //we should have sent two text messages: one to passenger, one to booking coordinator
            Assert.Equal(2, nexmoService.Messages.Count);

            var passengerMessage = nexmoService.Messages.Single(m => m.Recipient == "4711111111");
            Assert.Contains("We will try to find a pilot", passengerMessage.Body);

            var coordinatorMessage = nexmoService.Messages.Single(m => m.Recipient == "4798463072");
            Assert.Contains("Please find a pilot", coordinatorMessage.Body);
        }

        [Fact]
        public async Task CreateSuccessfulSimpleBookingWithAvailablePilot()
        {
            Context.AddAvailabilityFixture(new DateTime(2016, 11, 1), _pilots.Frode);

            var input = new BookingViewModel
            {
                Date = new DateTime(2016, 11, 1),
                Name = "My Name",
                PhoneNumber = "11111111",
                Email = "passenger@example.com",
                Comment = "Blah"
            };

            var ctrl = GetService<BookingController>();
            var result = await ctrl.Index(input);

            //Assert booking is created
            Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(1, Context.Bookings.Count());

            var booking = Context.Bookings
                .Include(b => b.AssignedPilot)
                .Include(b => b.AdditionalBookings)
                .Include(b => b.BookedPilots)
                .Include(b => b.BookingEvents)
                .First();

            Assert.Equal(_pilots.Frode, booking.AssignedPilot);
            Assert.All(booking.BookedPilots, b =>
            {
                Assert.Equal(_pilots.Frode, b.Pilot);
                Assert.False(b.Canceled);
            });
            Assert.Equal("passenger@example.com", booking.PassengerEmail);
            Assert.Equal("4711111111", booking.PassengerPhone);
            Assert.Equal("Blah", booking.Comment);

            //Assert sms is sent
            var nexmoService = (MockNexmoService)GetService<INexmoService>();

            //we should have sent three text messages: one to passenger, one to booking coordinator, one to pilot
            Assert.Equal(3, nexmoService.Messages.Count);

            var passengerMessage = nexmoService.Messages.Single(m => m.Recipient == "4711111111");
            Assert.Contains("You will be contacted by Frode Fester", passengerMessage.Body);

            var coordinatorMessage = nexmoService.Messages.Single(m => m.Recipient == "4798463072");
            Assert.Contains("assigned to Frode Fester", coordinatorMessage.Body);

            var pilotMessage = nexmoService.Messages.Single(m => m.Recipient == _pilots.Frode.PhoneNumber);
            Assert.Contains("You have a new flight", pilotMessage.Body);
        }

        [Fact]
        public async Task CreateBookingwithAdditionalPassengers()
        {
            //two available pilots
            Context.AddAvailabilityFixture(new DateTime(2016, 11, 1), _pilots.Frode);
            Context.AddAvailabilityFixture(new DateTime(2016, 11, 1), _pilots.Erik);

            //Erik has one booking already, so Frode will get primary booking
            Context.AddBookingFixture(new DateTime(2016, 10, 30), _pilots.Erik);

            //three passengers
            var input = new BookingViewModel
            {
                Date = new DateTime(2016, 11, 1),
                Name = "My Name",
                PhoneNumber = "11111111",
                Email = "passenger@example.com",
                Comment = "Blah",
                AdditionalPassengers = new []
                {
                    "Additional1",
                    "Additional2"
                }
            };

            var ctrl = GetService<BookingController>();
            var result = await ctrl.Index(input);

            //Assert bookings are created
            Assert.IsType<RedirectToActionResult>(result);
            var redirectResult = (RedirectToActionResult) result;
            var bookingId = (Guid) redirectResult.RouteValues["bookingId"];

            var bookings = Context.Bookings
                .Include(b => b.AssignedPilot)
                .Include(b => b.AdditionalBookings)
                .Include(b => b.BookedPilots)
                .Include(b => b.BookingEvents)
                .ToList();

            var mainBooking = bookings.Single(b => b.Id == bookingId);
            Assert.Equal(2, bookings.Count(b => b.PrimaryBooking == mainBooking));

            var otherBooking1 = bookings.Single(b => b.PrimaryBooking == mainBooking && b.AssignedPilot == _pilots.Erik);
            var otherBooking2 = bookings.Single(b => b.PrimaryBooking == mainBooking && b.AssignedPilot == null);

            //main booking
            Assert.Null(mainBooking.PrimaryBooking);
            Assert.Equal(2, mainBooking.AdditionalBookings.Count);
            Assert.Equal(_pilots.Frode, mainBooking.AssignedPilot);
            Assert.All(mainBooking.BookedPilots, b =>
            {
                Assert.Equal(_pilots.Frode, b.Pilot);
                Assert.False(b.Canceled);
            });
            Assert.Equal("My Name", mainBooking.PassengerName);
            Assert.Equal("passenger@example.com", mainBooking.PassengerEmail);
            Assert.Equal("4711111111", mainBooking.PassengerPhone);
            Assert.Equal("Blah", mainBooking.Comment);

            //other booking (with pilot)
            Assert.Equal(0, otherBooking1.AdditionalBookings.Count);
            Assert.Equal(_pilots.Erik, otherBooking1.AssignedPilot);
            Assert.All(otherBooking1.BookedPilots, b =>
            {
                Assert.Equal(_pilots.Erik, b.Pilot);
                Assert.False(b.Canceled);
            });
            Assert.Equal("Additional1", otherBooking1.PassengerName);
            Assert.Equal("passenger@example.com", otherBooking1.PassengerEmail);
            Assert.Equal("4711111111", otherBooking1.PassengerPhone);
            Assert.StartsWith("Blah", otherBooking1.Comment);

            //other booking (without pilot)
            Assert.Equal(0, otherBooking2.AdditionalBookings.Count);
            Assert.Equal(null, otherBooking2.AssignedPilot);
            Assert.Equal(0, otherBooking2.BookedPilots.Count);
            Assert.Equal("Additional2", otherBooking2.PassengerName);
            Assert.Equal("passenger@example.com", otherBooking2.PassengerEmail);
            Assert.Equal("4711111111", otherBooking2.PassengerPhone);
            Assert.StartsWith("Blah", otherBooking2.Comment);

            //Assert sms is sent
            var nexmoService = (MockNexmoService)GetService<INexmoService>();

            //we should have sent three text messages: 
            // - one to passenger
            // - one to each of the two assigned pilots
            // - three to booking coordinator
            Assert.Equal(6, nexmoService.Messages.Count);

            var passengerMessage = nexmoService.Messages.Single(m => m.Recipient == "4711111111");
            Assert.Contains("We will try to find 3 pilots", passengerMessage.Body);

            var coordinatorMessages = nexmoService.Messages.Where(m => m.Recipient == "4798463072").ToList();
            Assert.Contains(coordinatorMessages, m => m.Body.Contains("assigned to Frode Fester"));
            Assert.Contains(coordinatorMessages, m => m.Body.Contains("assigned to Erik Røthe Klette"));
            Assert.Contains(coordinatorMessages, m => m.Body.Contains("Please find a pilot on 01.11.2016"));

            var pilotMessage = nexmoService.Messages.Single(m => m.Recipient == _pilots.Frode.PhoneNumber);
            Assert.Contains("You have a new flight", pilotMessage.Body);

            var pilotMessage2 = nexmoService.Messages.Single(m => m.Recipient == _pilots.Erik.PhoneNumber);
            Assert.Contains("You have a new flight", pilotMessage2.Body);

        }
    }
}