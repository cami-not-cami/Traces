using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Linq;
using Traces.Models;

namespace Traces.Services
{
    public class PdfService
    {
        public PdfService()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public byte[] GenerateTripPdf(CreateTripViewModel trip)
        {
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(10).FontColor(Colors.Grey.Darken4));

                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Text("TRACES | Trip Itinerary").Bold().FontSize(12).FontColor(Colors.Indigo.Medium);
                        row.ConstantItem(100).AlignRight().Text(DateTime.Now.ToString("yyyy-MM-dd")).FontSize(9).FontColor(Colors.Grey.Medium);
                    });

                    page.Content().PaddingVertical(20).Column(column =>
                    {
                        column.Spacing(15);

                        // Trip Cover / Summary Card
                        column.Item().Background(Colors.Grey.Lighten5).Border(1).BorderColor(Colors.Grey.Lighten3).Padding(15).Column(cover =>
                        {
                            cover.Spacing(8);
                            cover.Item().Text(trip.Title ?? "My Trip").Bold().FontSize(22).FontColor(Colors.Indigo.Darken2);
                            
                            if (!string.IsNullOrEmpty(trip.Description))
                            {
                                cover.Item().Text(trip.Description).Italic().FontSize(10).FontColor(Colors.Grey.Medium);
                            }

                            cover.Item().Row(details =>
                            {
                                var dateStr = "No dates set";
                                if (trip.StartDate.HasValue || trip.EndDate.HasValue)
                                {
                                    dateStr = $"{(trip.StartDate.HasValue ? trip.StartDate.Value.ToString("MMM dd, yyyy") : "Start")} - {(trip.EndDate.HasValue ? trip.EndDate.Value.ToString("MMM dd, yyyy") : "End")}";
                                }
                                details.RelativeItem().Text($"Dates: {dateStr}").FontSize(9).FontColor(Colors.Grey.Medium);
                                
                                if (trip.Budget.HasValue && trip.Budget.Value > 0)
                                {
                                    details.ConstantItem(150).AlignRight().Text($"Budget: {trip.Budget.Value:N2} €").Bold().FontSize(10).FontColor(Colors.Green.Darken2);
                                }
                            });
                        });

                        // Unscheduled Places to Visit
                        if (trip.PlacesToVisit != null && trip.PlacesToVisit.Any())
                        {
                            column.Item().Text("Places to Visit (Unscheduled)").Bold().FontSize(14).FontColor(Colors.Grey.Darken3);
                            column.Item().Column(places =>
                            {
                                places.Spacing(5);
                                foreach (var place in trip.PlacesToVisit)
                                {
                                    places.Item().Row(pRow =>
                                    {
                                        pRow.ConstantItem(15).Text("-").FontSize(10).Bold();
                                        pRow.RelativeItem().Text(place.Name).FontSize(10);
                                        if (!string.IsNullOrEmpty(place.PrimaryCategory))
                                        {
                                            pRow.ConstantItem(100).AlignRight().Text($"({place.PrimaryCategory})").FontSize(9).FontColor(Colors.Grey.Medium);
                                        }
                                    });
                                }
                            });
                            
                            // Divider
                            column.Item().BorderBottom(1).BorderColor(Colors.Grey.Lighten3);
                        }

                        // Daily Itinerary
                        column.Item().Text("Daily Itinerary").Bold().FontSize(16).FontColor(Colors.Indigo.Medium);
                        
                        if (trip.Days == null || !trip.Days.Any())
                        {
                            column.Item().Text("No itinerary scheduled yet.").Italic().FontColor(Colors.Grey.Medium);
                        }
                        else
                        {
                            foreach (var day in trip.Days)
                            {
                                column.Item().Column(dayCol =>
                                {
                                    dayCol.Spacing(8);
                                    
                                    // Day Header
                                    dayCol.Item().Row(dayHeader =>
                                    {
                                        dayHeader.RelativeItem().Text(day.DayLabel).Bold().FontSize(13).FontColor(Colors.Indigo.Darken1);
                                        if (day.Date.HasValue)
                                        {
                                            dayHeader.RelativeItem().AlignRight().Text(day.Date.Value.ToString("MMMM dd, yyyy")).FontSize(10).FontColor(Colors.Grey.Medium);
                                        }
                                    });
                                    
                                    if (day.TimelineItems == null || !day.TimelineItems.Any())
                                    {
                                        dayCol.Item().PaddingLeft(15).Text("No activities planned for this day.").Italic().FontSize(9).FontColor(Colors.Grey.Medium);
                                    }
                                    else
                                    {
                                        foreach (var item in day.TimelineItems)
                                        {
                                            if (item.Type == "Activity" && item.Activity != null)
                                            {
                                                var act = item.Activity;
                                                dayCol.Item().PaddingLeft(15).BorderLeft(2).BorderColor(Colors.Indigo.Lighten3).PaddingLeft(10).Column(actCol =>
                                                {
                                                    actCol.Spacing(3);
                                                    actCol.Item().Row(actHeader =>
                                                    {
                                                        var timeStr = "";
                                                        if (act.StartTime.HasValue)
                                                        {
                                                            timeStr = act.StartTime.Value.ToString("hh:mm tt");
                                                            if (act.EndTime.HasValue)
                                                            {
                                                                timeStr += $" - {act.EndTime.Value.ToString("hh:mm tt")}";
                                                            }
                                                            timeStr += " | ";
                                                        }
                                                        
                                                        actHeader.RelativeItem().Text(t =>
                                                        {
                                                            if (!string.IsNullOrEmpty(timeStr))
                                                            {
                                                                t.Span(timeStr).FontSize(9).FontColor(Colors.Grey.Medium);
                                                            }
                                                            t.Span(act.Place?.Name ?? "Unnamed Place").Bold().FontSize(11).FontColor(Colors.Grey.Darken3);
                                                        });
                                                        
                                                        if (!string.IsNullOrEmpty(act.Status))
                                                        {
                                                            actHeader.ConstantItem(80).AlignRight().Text(act.Status.ToUpper()).Bold().FontSize(8).FontColor(Colors.Green.Darken2);
                                                        }
                                                    });
                                                    
                                                    if (!string.IsNullOrEmpty(act.Place?.FormattedAddress))
                                                    {
                                                        actCol.Item().Text(act.Place.FormattedAddress).FontSize(9).FontColor(Colors.Grey.Medium);
                                                    }
                                                    
                                                    if (act.RouteToNext != null)
                                                    {
                                                        var mode = act.RouteToNext.TravelMode == "WALK" ? "Walk" : "Drive";
                                                        var dist = act.RouteToNext.DistanceMeters / 1000.0;
                                                        var dur = act.RouteToNext.DurationSeconds / 60;
                                                        actCol.Item().PaddingVertical(2).Background(Colors.Grey.Lighten5).PaddingHorizontal(5).Text($"[Route] {mode}: {dist:N1} km ({dur} mins)").FontSize(8).FontColor(Colors.Grey.Medium);
                                                    }
                                                });
                                            }
                                            else if (item.Type == "Note" && item.Note != null)
                                            {
                                                dayCol.Item().PaddingLeft(15).BorderLeft(2).BorderColor(Colors.Grey.Lighten2).PaddingLeft(10).Background(Colors.Grey.Lighten5).Padding(5).Column(noteCol =>
                                                {
                                                    noteCol.Item().Text(item.Note.Content).FontSize(9).FontColor(Colors.Grey.Darken1);
                                                });
                                            }
                                            else if (item.Type == "Checklist" && item.Checklist != null)
                                            {
                                                var ch = item.Checklist;
                                                dayCol.Item().PaddingLeft(15).BorderLeft(2).BorderColor(Colors.Grey.Lighten2).PaddingLeft(10).Column(chCol =>
                                                {
                                                    chCol.Spacing(4);
                                                    chCol.Item().Text(ch.Title).Bold().FontSize(10).FontColor(Colors.Grey.Darken2);
                                                    foreach (var chItem in ch.ChecklistItems.OrderBy(ci => ci.OrderIndex))
                                                    {
                                                        chCol.Item().Row(chRow =>
                                                        {
                                                            chRow.ConstantItem(15).Text(chItem.IsCompleted ? "[X]" : "[ ]").FontSize(9).FontColor(chItem.IsCompleted ? Colors.Green.Medium : Colors.Grey.Medium);
                                                            chRow.RelativeItem().Text(chItem.Content).FontSize(9).FontColor(chItem.IsCompleted ? Colors.Grey.Medium : Colors.Grey.Darken1);
                                                        });
                                                    }
                                                });
                                            }
                                        }
                                    }
                                });
                                
                                // Divider after each day
                                column.Item().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten4);
                            }
                        }
                    });

                    page.Footer().Row(row =>
                    {
                        row.RelativeItem().Text("Generated by Traces - Your Offline Travel Companion").FontSize(8).FontColor(Colors.Grey.Medium);
                        row.ConstantItem(100).AlignRight().DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Grey.Medium)).Text(x =>
                        {
                            x.Span("Page ");
                            x.CurrentPageNumber();
                            x.Span(" of ");
                            x.TotalPages();
                        });
                    });
                });
            });

            return document.GeneratePdf();
        }
    }
}
