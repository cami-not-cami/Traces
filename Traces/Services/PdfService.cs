using QuestPDF.Infrastructure;

namespace Traces.Services
{
    public class PdfService
    {
        public PdfService()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }
    }
}
