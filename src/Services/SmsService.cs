﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Entity;
using TandemBooking.Models;

namespace TandemBooking.Services
{
    public class SmsService
    {
        private readonly TandemBookingContext _context;
        private readonly NexmoService _nexmoService;

        public SmsService(NexmoService nexmoService, TandemBookingContext context)
        {
            _nexmoService = nexmoService;
            _context = context;
        }

        public async Task Send(string recipient, string messageText, Booking booking)
        {
            var message = new SentSmsMessage()
            {
                Booking = booking,
                RecipientNumber = recipient,
                MessageText = messageText,
                SentDate = DateTime.UtcNow,
                SmsMessageParts = new List<SentSmsMessagePart>(),
            };
            _context.Add(message);
            await _context.SaveChangesAsync();

            //send message
            var result = await _nexmoService.SendSms("VossHPK", message.RecipientNumber, message.MessageText);

            //save status
            foreach (var nexmoPart in result.Messages)
            {
                message.SmsMessageParts.Add(new SentSmsMessagePart()
                {
                    GatewayMessageId = nexmoPart.MessageId,
                    StatusCode = nexmoPart.Status,
                    StatusText = nexmoPart.ErrorText,
                });
            }

            await _context.SaveChangesAsync();
        }

        public async Task HandleDeliveryReport(NexmoDeliveryReport deliveryReport)
        {
           var messagePart = await _context.SentSmsMessageParts.FirstOrDefaultAsync(p => p.GatewayMessageId == deliveryReport.MessageId);
            if (messagePart != null)
            {
                messagePart.DeliveryReportDate = DateTime.UtcNow;
                messagePart.DeliveryReportStatus = deliveryReport.Status;
                messagePart.DeliveryReportErrorCode = deliveryReport.ErrCode;
                await _context.SaveChangesAsync();
            }
        }
    }
}