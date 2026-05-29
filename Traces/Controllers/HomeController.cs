using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using Traces.Models;

namespace Traces.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly Settings _settings;
        private readonly TracesDbContext _context;
        private readonly string _googleApiKey;
        public HomeController(ILogger<HomeController> logger, IOptions<Settings> settings, TracesDbContext context)
        {
            _logger = logger;
            _context = context;
            //remove this if you wont use it
            _googleApiKey = settings.Value.ApiKey;
        }
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }
        public IActionResult Trip()
        {
            return View();
        }
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
